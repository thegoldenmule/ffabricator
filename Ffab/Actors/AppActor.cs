using System.IO;
using Akka.Actor;
using Akka.Routing;
using Newtonsoft.Json;
using Serilog;

namespace Ffab.Actors
{
    /// <summary>
    /// Main actor, controls all the others.
    /// </summary>
    public class AppActor : ReceiveActor
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        public AppActor(AppConfiguration config)
        {
            Log.Information($"Starting app with configuration: {JsonConvert.SerializeObject(config)}.");
            
            // prepare
            Directory.CreateDirectory(config.DownloadBaseDir);
            Directory.CreateDirectory(config.OutputBaseDir);
            
            // http server actor
            var http = Context.ActorOf(
                Props.Create(() => new HttpHostActor()),
                "http");
            
            // download actors
            var downloaders = Context.ActorOf(
                Props
                    .Create(() => new DownloaderActor(config.DownloadBufferSize))
                    .WithRouter(new SmallestMailboxPool(
                        config.NumDownloaders,
                        null,
                        Pool.DefaultSupervisorStrategy,
                        "downloaders")),
                "downloaders");
            
            // dedicated upload actors
            var uploaders = Context.ActorOf(
                Props
                    .Create(() => new UploaderActor(config.UploadBucketName))
                    .WithRouter(new SmallestMailboxPool(
                        config.NumUploaders,
                        null,
                        Pool.DefaultSupervisorStrategy,
                        "uploaders")),
                "uploaders");
            
            // processor actors
            var processors = Context.ActorOf(
                Props
                    .Create(() => new ProcessorActor())
                    .WithRouter(new SmallestMailboxPool(
                        config.NumProcessors,
                        null,
                        Akka.Actor.SupervisorStrategy.DefaultStrategy,
                        "processors")),
                "processors");
            
            // listen for completed jobs
            Receive<JobSagaActor.StartJobResponse>(msg =>
            {
                if (!msg.Success)
                {
                    Log.Error("Job {@JobId} failed: {@Error}.", msg.JobId, msg.Error);
                }
                else
                {
                    Log.Information("Completed job {@JobId}.", msg.JobId);
                }

                // clean-up sender
                Context.Stop(Context.Sender);
            });
            
            // listen for new jobs from http host and create new sage
            Receive<HttpHostActor.NewJobMessage>(msg =>
            {
                var saga = Context.ActorOf(
                    Props.Create(() => new JobSagaActor(downloaders, uploaders, processors)),
                    $"job-{msg.JobId}");
                
                saga.Tell(new JobSagaActor.StartJobRequest
                {
                    Target = Self,
                    JobId = msg.JobId,
                    Url = msg.Url,
                    DownloadBaseDir = config.DownloadBaseDir,
                    OutputBaseDir = config.OutputBaseDir,
                });
            });
            
            // startup
            http.Tell(new HttpHostActor.StartMessage
            {
                Target = Self,
            });
        }
    }
}