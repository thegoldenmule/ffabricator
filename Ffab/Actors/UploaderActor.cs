using System;
using System.IO;
using Akka.Actor;
using Google.Cloud.Storage.V1;

namespace Ffab.Actors
{
    public class UploaderActor : ReceiveActor
    {
        public class Request
        {
            public IActorRef Target { get; set; }
            public string InputFile { get; set; }
        }

        public class Response
        {
            public bool Success { get; set; }
            public string Error { get; set; }
            public string InputFile { get; set; }
        }
        
        public UploaderActor(string bucketName)
        {
            var client = StorageClient.Create();

            Receive<Request>(msg =>
            {
                var target = msg.Target;
                var file = msg.InputFile;
                
                using (var fs = File.OpenRead(file))
                {
                    try
                    {
                        client.UploadObject(
                            bucketName,
                            file,
                            null, fs);
                    }
                    catch (Exception exception)
                    {
                        target.Tell(new Response
                        {
                            Success = false,
                            Error = exception.ToString(),
                            InputFile = file,
                        });

                        return;
                    }
                }
                
                target.Tell(new Response
                {
                    Success = true,
                    InputFile = file,
                });
            });
        }
    }
}