using System;
using System.IO;
using Akka.Actor;
using Google.Cloud.Storage.V1;

namespace Ffab.Actors
{
    /// <summary>
    /// Actor that synchronously uploads a file.
    /// </summary>
    public class UploaderActor : ReceiveActor
    {
        /// <summary>
        /// Requests an upload.
        /// </summary>
        public class Request
        {
            /// <summary>
            /// The actor to send a response to.
            /// </summary>
            public IActorRef Target { get; set; }
            
            /// <summary>
            /// The file to upload.
            /// </summary>
            public string InputFile { get; set; }
        }

        /// <summary>
        /// Response object.
        /// </summary>
        public class Response
        {
            /// <summary>
            /// True iff the upload was successful.
            /// </summary>
            public bool Success { get; set; }
            
            /// <summary>
            /// If success is false, the error that occurred.
            /// </summary>
            public string Error { get; set; }
            
            /// <summary>
            /// The file that was uploaded.
            /// </summary>
            public string InputFile { get; set; }
        }
        
        /// <summary>
        /// Constructor.
        /// </summary>
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