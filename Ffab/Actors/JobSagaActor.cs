using System;
using System.Collections.Generic;
using System.IO;
using Akka.Actor;
using Serilog;

namespace Ffab.Actors
{
    /// <summary>
    /// Manages a job's progression.
    /// </summary>
    public class JobSagaActor : ReceiveActor
    {
        /// <summary>
        /// Sent to start a job.
        /// </summary>
        public class StartJobRequest
        {
            /// <summary>
            /// The actor to send the response to.
            /// </summary>
            public IActorRef Target { get; set; }
            
            /// <summary>
            /// Id of the job.
            /// </summary>
            public long JobId { get; set; }
            
            /// <summary>
            /// The url to download.
            /// </summary>
            public string Url { get; set; }
            
            /// <summary>
            /// The bytes to download.
            /// </summary>
            public string DownloadBaseDir { get; set; }
            
            /// <summary>
            /// The directory into which we should output processed files.
            /// </summary>
            public string OutputBaseDir { get; set; }
        }
        
        /// <summary>
        /// Send from this actor when a job is complete.
        /// </summary>
        public class StartJobResponse
        {
            /// <summary>
            /// True iff the job was successful.
            /// </summary>
            public bool Success { get; set; }
            
            /// <summary>
            /// Any errors, if Success is false.
            /// </summary>
            public string Error { get; set; }
            
            /// <summary>
            /// the job id this is referring to.
            /// </summary>
            public long JobId { get; set; }
        }

        /// <summary>
        /// Used to tick the actor, internally.
        /// </summary>
        private class Tick
        {
            //
        }
        
        /// <summary>
        /// Collection of incomplete files.
        /// </summary>
        private readonly HashSet<string> _dirtyFiles = new();
        
        /// <summary>
        /// Actor references.
        /// </summary>
        private readonly IActorRef _downloaders;
        private readonly IActorRef _uploaders;
        private readonly IActorRef _processors;
        private readonly IActorRef _monitor;

        /// <summary>
        /// All cancelable things.
        /// </summary>
        private readonly HashSet<ICancelable> _cancelables = new();

        /// <summary>
        /// Base directory for processed files.
        /// </summary>
        private string _outputBaseDir;
        
        /// <summary>
        /// Base directory for downloaded files.
        /// </summary>
        private string _downloadBaseDir;

        /// <summary>
        /// Constructor.
        /// </summary>
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

        /// <inheritdoc />
        protected override void PostStop()
        {
            base.PostStop();

            foreach (var cancelable in _cancelables)
            {
                try
                {
                    cancelable.Cancel();
                }
                catch
                {
                    //
                }
            }
        }

        /// <summary>
        /// State when the actor is waiting ot start.
        /// </summary>
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

        /// <summary>
        /// Called to start the job.
        /// </summary>
        private Action Started(IActorRef target, long jobId, string url, string downloadBaseDir, string outputBaseDir) => () =>
        {
            Log.Information("Starting job {@JobId}.", jobId);

            _downloadBaseDir = downloadBaseDir;
            _outputBaseDir = outputBaseDir;
            
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
                _processors.Tell(new ProcessorActor.Request
                {
                    Target = Self,
                    JobId = jobId,
                    InputPath = msg.OutputPath,
                    OutputDir = targetDir,
                });
            });

            // listen for processing complete
            Receive<ProcessorActor.Response>(msg =>
            {
                if (!msg.Success)
                {
                    target.Tell(new StartJobResponse
                    {
                        Success = false,
                        Error = msg.Error,
                        JobId = jobId
                    });

                    return;
                }
                
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

        /// <summary>
        /// Waits for all uploads to complete before signalling the end of the job.
        /// </summary>
        private Action WaitingForUploads(IActorRef target, long jobId) => () =>
        {
            ListenForFileEvents();

            Receive<Tick>(_ =>
            {
                var numDirtyFiles = _dirtyFiles.Count;
                if (numDirtyFiles == 0)
                {
                    try
                    {
                        Directory.Delete(_downloadBaseDir, true);
                    }
                    catch (Exception exception)
                    {
                        Log.Error(
                            "Could not delete downloaded files at {@DownloadPath} for {@JobId}: {@Error}.",
                            _downloadBaseDir, jobId, exception.ToString());
                    }
                    
                    try
                    {
                        Directory.Delete(_outputBaseDir, true);
                    }
                    catch (Exception exception)
                    {
                        Log.Error(
                            "Could not delete processed files at {@ProcessPath} for {@JobId}: {@Error}.",
                            _downloadBaseDir, jobId, exception.ToString());
                    }

                    target.Tell(new StartJobResponse
                    {
                        JobId = jobId,
                        Success = true,
                    });
                }
            });
            
            _cancelables.Add(
                Context.System.Scheduler.ScheduleTellRepeatedlyCancelable(
                    TimeSpan.FromSeconds(2),
                    TimeSpan.FromSeconds(2),
                    Self,
                    new Tick(),
                    Self));
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
                    InputFile = msg.File,
                });
                
                _dirtyFiles.Add(msg.File);
            });
            
            // listen for upload complete
            Receive<UploaderActor.Response>(msg =>
            {
                if (!msg.Success)
                {
                    Log.Warning("Could not upload file. TODO: schedule retry!");
                    
                    _cancelables.Add(
                        Context.System.Scheduler.ScheduleTellOnceCancelable(
                            TimeSpan.FromSeconds(2),
                            _uploaders,
                            new UploaderActor.Request
                            {
                                
                            },
                            Self));
                    return;
                }

                // file is no longer dirty
                _dirtyFiles.Remove(msg.InputFile);
            });
        }
    }
}