using System.Threading;
using System.Threading.Tasks;
using Akka.Actor;
using Ffab.Model;
using Microsoft.AspNetCore.Mvc;
using Serilog;

namespace Ffab.Controller
{
    /// <summary>
    /// WebAPI controller.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class JobController : ControllerBase
    {
        /// <summary>
        /// The <c>AppActor</c>.
        /// </summary>
        private readonly IActorRef _app;
        
        /// <summary>
        /// Job id counter.
        /// </summary>
        private static long _jobId = 0;

        /// <summary>
        /// Constructor.
        /// </summary>
        public JobController(IActorRef app)
        {
            _app = app;
        }

        /// <summary>
        /// Handles post requests to start a job.
        /// </summary>
        [HttpPost]
        public async Task<ActionResult> Post(JobInfo job)
        {
            var jobId = Interlocked.Increment(ref _jobId);
            
            Log.Information("Creating job {@JobId}.", jobId);
            
            // send to Akka
            _app.Tell(new HttpHostActor.NewJobMessage
            {
                JobId = jobId,
                Url = job.Url,
            });
            
            return Created($"/job/{jobId}", new { JobId = jobId });
        }
    }
}