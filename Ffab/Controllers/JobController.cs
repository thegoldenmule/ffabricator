using System.Threading;
using System.Threading.Tasks;
using Akka.Actor;
using Ffab.Model;
using Microsoft.AspNetCore.Mvc;
using Serilog;

namespace Ffab.Controller
{
    [ApiController]
    [Route("api/[controller]")]
    public class JobController : ControllerBase
    {
        private readonly IActorRef _app;
        private static long _jobId = 0;

        public JobController(IActorRef app)
        {
            _app = app;
        }

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