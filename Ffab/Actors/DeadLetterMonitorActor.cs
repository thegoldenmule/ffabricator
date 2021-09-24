using Akka.Actor;
using Akka.Event;
using Serilog;

namespace Ffab.Actors
{
    /// <summary>
    /// Actor that logs out dead letters.
    /// </summary>
    public class DeadLetterMonitorActor : ReceiveActor
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        public DeadLetterMonitorActor()
        {
            Receive<DeadLetter>(dl =>
            {
                Log.Warning($"DeadLetter captured: {dl.Message}, sender: {dl.Sender}, recipient: {dl.Recipient}");
            });
        }
    }
}