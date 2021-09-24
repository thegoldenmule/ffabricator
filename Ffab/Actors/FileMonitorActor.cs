using System;
using System.Collections.Generic;
using System.IO;
using Akka.Actor;

namespace Ffab.Actors
{
    public class FileMonitorActor : ReceiveActor
    {
        public class Start
        {
            public IActorRef Target { get; set; }
            public string BaseDir { get; set; }
            public double TickIntervalSecs { get; set; } = 1.0;
            public double FileWaitSecs { get; set; } = 2.0;
        }

        public class Stop
        {
            //
        }

        public class FileReady
        {
            public string InputFile { get; set; }
        }

        private class Tick
        {
            //
        }

        private readonly HashSet<string> _dispatched = new();
        private ICancelable _cancelable;

        public FileMonitorActor()
        {
            Become(NotStarted);
        }
        
        protected override void PostStop()
        {
            base.PostStop();
            
            _cancelable?.Cancel();
        }
        
        private void NotStarted()
        {
            Receive<Start>(msg => Become(Started(
                msg.Target,
                msg.BaseDir,
                msg.TickIntervalSecs,
                msg.FileWaitSecs)));
        }

        private Action Started(IActorRef target, string baseDir, double tickIntervalSecs, double fileWaitSecs) => () =>
        {
            Receive<Stop>(_ =>
            {
                _cancelable?.Cancel();
                _cancelable = null;
            });
            Receive<Tick>(_ => Walk(target, baseDir, fileWaitSecs));

            _cancelable = Context.System.Scheduler.ScheduleTellRepeatedlyCancelable(
                TimeSpan.FromSeconds(tickIntervalSecs),
                TimeSpan.FromSeconds(tickIntervalSecs),
                Self,
                new Tick(),
                Self);
        };

        private void Walk(IActorRef target, string dir, double fileWaitSecs)
        {
            var now = DateTime.Now;
            var files = Directory.GetFiles(dir);
            var dirs = Directory.GetDirectories(dir);

            foreach (var file in files)
            {
                if (file.Contains(".DS_Store"))
                {
                    continue;
                }

                if (_dispatched.Contains(file))
                {
                    continue;
                }
                
                if (now.Subtract(File.GetLastWriteTime(file)).TotalSeconds > fileWaitSecs)
                {
                    _dispatched.Add(file);
                    
                    target.Tell(new FileReady
                    {
                        InputFile = file,
                    });
                }
            }
            
            foreach (var subDir in dirs)
            {
                Walk(target, subDir, fileWaitSecs);
            }
        }
    }
}