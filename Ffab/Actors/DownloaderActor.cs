using System.IO;
using System.Net;
using Akka.Actor;

namespace Ffab.Actors
{
    public class DownloaderActor : ReceiveActor
    {
        public class Request
        {
            public IActorRef Target { get; set; }
            public string Url { get; set; }
            public string BaseDir { get; set; }
        }

        public class Response
        {
            public string OutputPath { get; set; }
        }
        
        private readonly byte[] _buffer;

        public DownloaderActor(int bufferSize)
        {
            _buffer = new byte[bufferSize];

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
                    length = rs.Read(_buffer, 0, _buffer.Length);
                    fs.Write(_buffer, 0, length);
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