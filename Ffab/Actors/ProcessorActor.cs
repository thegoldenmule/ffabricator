using System.Diagnostics;
using Akka.Actor;
using Serilog;

namespace Ffab.Actors
{
    public class ProcessorActor : ReceiveActor
    {
        public class StartJobRequest
        {
            public IActorRef Target { get; set; }
            public string InputPath { get; set; }
            public string OutputDir { get; set; }
        }

        public class StartJobResponse
        {
            public string OutputDir { get; set; }
        }

        public ProcessorActor()
        {
            Receive<StartJobRequest>(msg =>
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
                process.Start();
                process.BeginOutputReadLine();
                process.WaitForExit();

                // done
                target.Tell(new StartJobResponse
                {
                    OutputDir = targetDir,
                });
            });
        }
        
        protected override void PostStop()
        {
            base.PostStop();
            
            Log.Warning($"Stopping processor: {Self.Path}.");
        }
    }
}