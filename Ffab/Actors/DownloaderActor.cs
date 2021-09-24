using System.IO;
using System.Net;
using Akka.Actor;

namespace Ffab.Actors
{
    /// <summary>
    /// Actor that synchronously downloads a file.
    /// </summary>
    public class DownloaderActor : ReceiveActor
    {
        /// <summary>
        /// Message to request a download.
        /// </summary>
        public class Request
        {
            /// <summary>
            /// The actor to send a response to.
            /// </summary>
            public IActorRef Target { get; set; }
            
            /// <summary>
            /// The url to download.
            /// </summary>
            public string Url { get; set; }
            
            /// <summary>
            /// The base directory to download into.
            /// </summary>
            public string BaseDir { get; set; }
        }
        
        /// <summary>
        /// Message sent when download is complete.
        /// </summary>
        public class Response
        {
            /// <summary>
            /// The output path.
            /// </summary>
            public string OutputPath { get; set; }
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        public DownloaderActor(int bufferSizeInBytes)
        {
            // download buffer
            var buffer = new byte[bufferSizeInBytes];

            Receive<Request>(msg =>
            {
                var baseDir = msg.BaseDir;
                var url = msg.Url;
                var target = msg.Target;

                // prepare target filepath
                string filepath;
                do
                {
                    filepath = Path.Combine(baseDir, Path.GetRandomFileName());
                } while (File.Exists(filepath));

                // create
                using var fs = File.Create(filepath);

                // start the request
                var req = WebRequest.Create(url);
                var res = req.GetResponse();

                if (req.ContentLength > 0)
                {
                    res.ContentLength = req.ContentLength;
                }

                // start reading
                using var rs = res.GetResponseStream();

                // copy to file using buffer
                int length;
                do
                {
                    length = rs.Read(buffer, 0, buffer.Length);
                    fs.Write(buffer, 0, length);
                } while (length > 0);

                // close the file stream
                fs.Close();

                // finish
                target.Tell(new Response
                {
                    OutputPath = filepath
                });
            });
        }
    };
}