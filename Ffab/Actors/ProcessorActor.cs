using System;
using System.Diagnostics;
using Akka.Actor;
using Serilog;

namespace Ffab.Actors
{
    /// <summary>
    /// Actor that synchronously processes file.
    /// </summary>
    public class ProcessorActor : ReceiveActor
    {
        /// <summary>
        /// Requests the processor to begin.
        /// </summary>
        public class Request
        {
            /// <summary>
            /// The job id.
            /// </summary>
            public long JobId { get; set; }
            
            /// <summary>
            /// The actor to send a response to.
            /// </summary>
            public IActorRef Target { get; set; }
            
            /// <summary>
            /// The file path.
            /// </summary>
            public string InputPath { get; set; }
            
            /// <summary>
            /// The folder to output into.
            /// </summary>
            public string OutputDir { get; set; }
        }

        /// <summary>
        /// Response message sent to the target after processing.
        /// </summary>
        public class Response
        {
            /// <summary>
            /// The id of the job.
            /// </summary>
            public long JobId { get; set; }
            
            /// <summary>
            /// True iff everything completed successfully.
            /// </summary>
            public bool Success { get; set; }
            
            /// <summary>
            /// If success is false, this will be set to the error that occurred.
            /// </summary>
            public string Error { get; set; }
            
            /// <summary>
            /// The directory into which the files were output. 
            /// </summary>
            public string OutputDir { get; set; }
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        public ProcessorActor()
        {
            Receive<Request>(msg =>
            {
                var inputName = msg.InputPath;
                var targetDir = msg.OutputDir;
                var target = msg.Target;

                var arguments = $"-i \"{inputName}\" -filter_complex \"[0:v]split=3[v1][v2][v3]; [v1]copy[v1out]; [v2]scale=w=1280:h=720[v2out]; [v3]scale=w=640:h=360[v3out]\" -map [v1out] -c:v:0 libx264 -x264-params \"nal-hrd=cbr:force-cfr=1\" -b:v:0 5M -maxrate:v:0 5M -minrate:v:0 5M -bufsize:v:0 10M -preset slow -g 48 -sc_threshold 0 -keyint_min 48 -map [v2out] -c:v:1 libx264 -x264-params \"nal-hrd=cbr:force-cfr=1\" -b:v:1 3M -maxrate:v:1 3M -minrate:v:1 3M -bufsize:v:1 3M -preset slow -g 48 -sc_threshold 0 -keyint_min 48 -map [v3out] -c:v:2 libx264 -x264-params \"nal-hrd=cbr:force-cfr=1\" -b:v:2 1M -maxrate:v:2 1M -minrate:v:2 1M -bufsize:v:2 1M -preset slow -g 48 -sc_threshold 0 -keyint_min 48 -map a:0 -c:a:0 aac -b:a:0 96k -ac 2 -map a:0 -c:a:1 aac -b:a:1 96k -ac 2 -map a:0 -c:a:2 aac -b:a:2 48k -ac 2 -f hls -hls_time 2 -hls_playlist_type vod -hls_flags independent_segments -hls_segment_type mpegts -hls_segment_filename {targetDir}/%v/data%02d.ts -master_pl_name manifest_master.m3u8 -var_stream_map \"v:0,a:0 v:1,a:1 v:2,a:2\" {targetDir}/manifest_%v.m3u8";

                // process synchronously
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        FileName = "ffmpeg",
                        Arguments = arguments,
                    }
                };

                try
                {
                    process.Start();
                }
                catch (Exception exception)
                {
                    Log.Error("Fatal: could not start ffmpeg: {@JobId}.", msg.JobId);
                    
                    target.Tell(new Response
                    {
                        Success = false,
                        Error = exception.ToString(),
                    });

                    return;
                }

                process.BeginOutputReadLine();
                process.WaitForExit();

                // done
                target.Tell(new Response
                {
                    Success = true,
                    OutputDir = targetDir,
                    JobId = msg.JobId,
                });
            });
        }
        
        /// <inheritdoc />
        protected override void PostStop()
        {
            base.PostStop();
            
            Log.Warning($"Stopping processor: {Self.Path}.");
        }
    }
}