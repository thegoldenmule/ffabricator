using System.IO;
using Akka.Actor;
using Akka.Routing;
using Serilog;

namespace Ffab.Actors
{
    public class AppActor : ReceiveActor
    {
        public AppActor(AppConfiguration config)
        {
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
            Receive<JobSagaActor.StartJobResponse>(message =>
            {
                Log.Information("Completed job {@JobId}.", message.JobId);
                
                // clean up actor
                Context.Stop(message.Self);
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