using System;
using System.Collections.Generic;
using System.IO;
using Akka.Actor;
using Serilog;

namespace Ffab.Actors
{
    public class JobSagaActor : ReceiveActor
    {
        public class StartJobRequest
        {
            public IActorRef Target { get; set; }
            public long JobId { get; set; }
            public string Url { get; set; }
            public string DownloadBaseDir { get; set; }
            public string OutputBaseDir { get; set; }
        }
        
        public class StartJobResponse
        {
            public bool Success { get; set; }
            public IActorRef Self { get; set; }
            public long JobId { get; set; }
        }

        private class Tick
        {
            //
        }
        
        private HashSet<string> _dirtyFiles = new();
        
        private readonly IActorRef _downloaders;
        private readonly IActorRef _uploaders;
        private readonly IActorRef _processors;
        private readonly IActorRef _monitor;
        
        private ICancelable _tickCancelable;

        public JobSagaActor(
            IActorRef downloaders,
            IActorRef uploaders,
            IActorRef processors)
        {
            _downloaders = downloaders;
            _uploaders = uploaders;
            _processors = processors;
            
            // create file monitor
            _monitor = Context.ActorOf(
                Props.Create(() => new FileMonitorActor()),
                "file-monitor");

            Become(NotStarted);
        }

        protected override void PostStop()
        {
            base.PostStop();
            
            _tickCancelable?.Cancel();
        }

        private void NotStarted()
        {
            // listen to start job
            Receive<StartJobRequest>(msg => Become(Started(
                msg.Target,
                msg.JobId,
                msg.Url,
                msg.DownloadBaseDir,
                msg.OutputBaseDir)));
        }

        private Action Started(IActorRef target, long jobId, string url, string downloadBaseDir, string outputBaseDir) => () =>
        {
            Log.Information("Starting job {@JobId}.", jobId);
            
            // listen for download complete
            Receive<DownloaderActor.Response>(msg =>
            {
                Log.Information("Downloading complete, starting processor for {@JobId}.", jobId);
                
                // derive specific output folder for this download
                var outputName = Path.GetFileNameWithoutExtension(msg.OutputPath);
                var targetDir = Path.Combine(outputBaseDir, outputName);

                Directory.CreateDirectory(targetDir);

                // start the file monitor
                _monitor.Tell(new FileMonitorActor.Start
                {
                    Target = Self,
                    BaseDir = targetDir,
                    FileWaitSecs = 2f,
                    TickIntervalSecs = 2f,
                });
                
                // pass to processor
                _processors.Tell(new ProcessorActor.StartJobRequest
                {
                    Target = Self,
                    InputPath = msg.OutputPath,
                    OutputDir = targetDir,
                });
            });

            // listen for processing complete
            Receive<ProcessorActor.StartJobResponse>(msg =>
            {
                Log.Information("Processing complete! Waiting for uploads to finish for {@JobId}.", jobId);

                // now we wait for uploads to complete
                Become(WaitingForUploads(target, jobId));
            });

            // listen for file and upload events
            ListenForFileEvents();
            
            // start the downloader
            _downloaders.Tell(new DownloaderActor.Request
            {
                Target = Self,
                Url = url,
                BaseDir = downloadBaseDir,
            });
        };

        private Action WaitingForUploads(IActorRef target, long jobId) => () =>
        {
            ListenForFileEvents();

            Receive<Tick>(_ =>
            {
                var numDirtyFiles = _dirtyFiles.Count;
                if (numDirtyFiles == 0)
                {
                    // TODO: delete all
                    
                    target.Tell(new StartJobResponse
                    {
                        Self = Self,
                        JobId = jobId,
                        Success = true,
                    });
                }
            });
            
            _tickCancelable = Context.System.Scheduler.ScheduleTellRepeatedlyCancelable(
                TimeSpan.FromSeconds(2),
                TimeSpan.FromSeconds(2),
                Self,
                new Tick(),
                Self);
        };
        
        /// <summary>
        /// Receives file and upload events.
        /// </summary>
        private void ListenForFileEvents()
        {
            // listen for file ready
            Receive<FileMonitorActor.FileReady>(msg =>
            {
                _uploaders.Tell(new UploaderActor.Request
                {
                    Target = Self,
                    InputFile = msg.InputFile,
                });
                
                _dirtyFiles.Add(msg.InputFile);
            });
            
            // listen for upload complete
            Receive<UploaderActor.Response>(msg =>
            {
                if (!msg.Success)
                {
                    // TODO: schedule retry
                    Log.Warning("Could not upload file. TODO: schedule retry!");
                }

                // file is no longer dirty
                _dirtyFiles.Remove(msg.InputFile);
            });
        }
    }
}