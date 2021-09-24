using System.Net;
using Akka.Actor;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Ffab
{
    /// <summary>
    /// Startup object for WebAPI.
    /// </summary>
    public class Startup
    {
        /// <summary>
        /// Configures the services on the collection.
        /// </summary>
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddHealthChecks();
            services.AddControllers();
            services.AddCors(options =>
                options.AddPolicy("All", builder => builder
                    .AllowAnyOrigin()
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .AllowCredentials()));
        }
        
        /// <summary>
        /// Configures the app.
        /// </summary>
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            app.UseRouting();
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapHealthChecks("/health");
                endpoints.MapControllers();
            });
        }
    }
    
    /// <summary>
    /// This actor manages the http service.
    /// </summary>
    public class HttpHostActor : ReceiveActor
    {
        /// <summary>
        /// The actor to send messages to.
        /// </summary>
        private IActorRef _target;

        /// <summary>
        /// Starts the http service.
        /// </summary>
        public class StartMessage
        {
            /// <summary>
            /// The actor to send messages to.
            /// </summary>
            public IActorRef Target { get; set; }
        }
        
        /// <summary>
        /// Send from this actor when there is a new job.
        /// </summary>
        public class NewJobMessage
        {
            /// <summary>
            /// Id of the job.
            /// </summary>
            public long JobId { get; set; }
            
            /// <summary>
            /// The url to download.
            /// </summary>
            public string Url { get; set; }
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        public HttpHostActor()
        {
            Receive<StartMessage>(msg =>
            {
                _target = msg.Target;
                
                Become(Starting);
            });
        }

        /// <summary>
        /// Stops listening to all events and starts the server.
        /// </summary>
        private void Starting()
        {
            Host
                .CreateDefaultBuilder()
                .ConfigureServices(services =>
                {
                    services.AddSingleton(_target);
                })
                .ConfigureWebHostDefaults(builder =>
                {
                    builder.ConfigureKestrel(options => options.Listen(IPAddress.Any, 10104));
                    builder.UseStartup<Startup>();
                })
                .Build()
                .RunAsync();
        }
    }
}