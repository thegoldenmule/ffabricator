using System;
using System.Collections.Generic;
using System.IO;
using Akka.Actor;
using Akka.Configuration;
using Akka.Event;
using CommandLine;
using Ffab.Actors;
using Serilog;
using Error = CommandLine.Error;

namespace Ffab
{
    public class Options
    {
        [Option('a', "akka-config", Required = true, HelpText = "Path to akka config.")]
        public bool AkkaConfig { get; set; }
    }
    
    class Program
    {
        static void Main(string[] args)
        {
            Parser.Default.ParseArguments<Options>(args)
                .WithParsed(Start)
                .WithNotParsed(Quit);
        }

        private static void Start(Options obj)
        {
            ConfigureLogging();

            var config = new AppConfiguration();

            var akkaConfig = LoadAkkaConfig(config);
            var bootstrap = BootstrapSetup.Create().WithConfig(akkaConfig);

            // create actor system!
            using var system = ActorSystem.Create("ffabricator", bootstrap);
            
            // listen for dead letters
            var monitor = system.ActorOf<DeadLetterMonitorActor>("dead-letters");
            system.EventStream.Subscribe<DeadLetter>(monitor);
            
            // create app
            system.ActorOf(
                Props.Create(() => new AppActor(config)),
                "app");

            system.WhenTerminated.Wait();
        }

        private static Config LoadAkkaConfig(AppConfiguration config)
        {
            return ConfigurationFactory.ParseString(File
                .ReadAllText("config.akka")
                .Replace("{{NumProcessors}}", config.NumProcessors.ToString())
                .Replace("{{NumUploaders}}", config.NumUploaders.ToString())
                .Replace("{{NumDownloaders}}", config.NumDownloaders.ToString()));
        }

        private static void Quit(IEnumerable<Error> errors)
        {
            //
        }
        
        private static void ConfigureLogging()
        {
            var config = new LoggerConfiguration();

            config = config
                .WriteTo.Console()
                .MinimumLevel.Debug()
                .WriteTo.File("Log.txt", rollingInterval: RollingInterval.Day)
                .MinimumLevel.Debug();

            Log.Logger = config.CreateLogger();
            Log.Information("Logging initialized.");
        }
    }
}
