using System.Net;
using Akka.Actor;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Ffab
{
    public class Startup
    {
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
    
    public class HttpHostActor : ReceiveActor
    {
        private IActorRef _target;

        public class StartMessage
        {
            public IActorRef Target { get; set; }
        }
        
        public class NewJobMessage
        {
            public long JobId { get; set; }
            public string Url { get; set; }
        }

        public HttpHostActor()
        {
            Receive<StartMessage>(msg =>
            {
                _target = msg.Target;
                
                Become(Starting);
            });
        }

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