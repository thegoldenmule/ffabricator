using System;
using System.Collections.Generic;
using System.IO;
using Akka.Actor;

namespace Ffab.Actors
{
    /// <summary>
    /// Monitors a folder for file changes.
    /// </summary>
    public class FileMonitorActor : ReceiveActor
    {
        /// <summary>
        /// Message that starts the monitor.
        /// </summary>
        public class Start
        {
            /// <summary>
            /// The actor to send updates to.
            /// </summary>
            public IActorRef Target { get; set; }
            
            /// <summary>
            /// The base directory to watch.
            /// </summary>
            public string BaseDir { get; set; }
            
            /// <summary>
            /// How often to provide updates.
            /// </summary>
            public double TickIntervalSecs { get; set; } = 1.0;
            
            /// <summary>
            /// How long a file needs to exist before we send a message.
            /// </summary>
            public double FileWaitSecs { get; set; } = 2.0;
        }

        /// <summary>
        /// Stops the monitor.
        /// </summary>
        public class Stop
        {
            //
        }

        /// <summary>
        /// Sent when a file is ready.
        /// </summary>
        public class FileReady
        {
            /// <summary>
            /// The file that has changed..
            /// </summary>
            public string File { get; set; }
        }

        /// <summary>
        /// Internal message.
        /// </summary>
        private class Tick
        {
            //
        }

        /// <summary>
        /// Tracks which files we have dispatched events concerning.
        /// </summary>
        private readonly HashSet<string> _dispatched = new();
        
        /// <summary>
        /// Cancels the tick.
        /// </summary>
        private ICancelable _cancelable;

        /// <summary>
        /// Constructor.
        /// </summary>
        public FileMonitorActor()
        {
            Become(NotStarted);
        }
        
        /// <inheritdoc />
        protected override void PostStop()
        {
            base.PostStop();
            
            _cancelable?.Cancel();
        }
        
        /// <summary>
        /// State for when we are waiting for a start.
        /// </summary>
        private void NotStarted()
        {
            Receive<Start>(msg => Become(Started(
                msg.Target,
                msg.BaseDir,
                msg.TickIntervalSecs,
                msg.FileWaitSecs)));
        }

        /// <summary>
        /// State for when we are watching files.
        /// </summary>
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

        /// <summary>
        /// Walks a directory and dispatches file events.
        /// </summary>
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
                        File = file,
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