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
    /// <summary>
    /// Command line options class.
    /// </summary>
    public class Options
    {
        /// <summary>
        /// Path to the akka config, in hocon.
        /// </summary>
        [Option('a', "akka-config", Required = true, HelpText = "Path to akka config.")]
        public string AkkaConfig { get; set; }
    }
    
    class Program
    {
        /// <summary>
        /// Entry point.
        /// </summary>
        static void Main(string[] args)
        {
            Parser.Default.ParseArguments<Options>(args)
                .WithParsed(Start)
                .WithNotParsed(Quit);
        }

        /// <summary>
        /// Starts the application, with options.
        /// </summary>
        private static void Start(Options options)
        {
            ConfigureLogging();

            var config = new AppConfiguration();

            var akkaConfig = LoadAkkaConfig(options, config);
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

        /// <summary>
        /// Quits the application.
        /// </summary>
        private static void Quit(IEnumerable<Error> errors)
        {
            //
        }
        
        /// <summary>
        /// Sets up logging.
        /// </summary>
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
        
        /// <summary>
        /// Loads the akka config from disk.
        /// </summary>
        private static Config LoadAkkaConfig(Options options, AppConfiguration config)
        {
            return ConfigurationFactory.ParseString(File
                .ReadAllText(options.AkkaConfig)
                .Replace("{{NumProcessors}}", config.NumProcessors.ToString())
                .Replace("{{NumUploaders}}", config.NumUploaders.ToString())
                .Replace("{{NumDownloaders}}", config.NumDownloaders.ToString()));
        }
    }
}
