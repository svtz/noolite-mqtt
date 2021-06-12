using System;
using System.Reflection;
using System.Runtime.ExceptionServices;
using Serilog;
using Serilog.Events;

namespace NooliteMqttAdapter
{
    public class LoggerBuilder
    {
        private readonly ConfigReader _config;

        public LoggerBuilder(ConfigReader config)
        {
            Guard.DebugAssertArgumentNotNull(config, nameof(config));
            _config = config;
        }
        
        public ILogger BuildLogger()
        {
            ILogger logger = new LoggerConfiguration()
                .MinimumLevel.Is(_config.LogLevel)
                .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} (from {SourceContext}){NewLine}{Exception}")
                .WriteTo.File("logs/log.txt", retainedFileCountLimit: 30, 
                    rollOnFileSizeLimit: true, fileSizeLimitBytes: 10 * 1024 * 1024,
                    rollingInterval: RollingInterval.Day)
                .CreateLogger();

            var context = Assembly.GetEntryAssembly()?.EntryPoint?.ReflectedType;
            if (context != null)
                logger = logger.ForContext(context);
            
            Log.Logger = logger;

            AppDomain.CurrentDomain.UnhandledException += LogUnhandledException;
            AppDomain.CurrentDomain.FirstChanceException += LogFirstChanceException;
            
            logger.Debug("Logging initialized.");
            return logger;
        }

        private static void LogFirstChanceException(object? sender, FirstChanceExceptionEventArgs e)
        {
            if (Log.Logger.IsEnabled(LogEventLevel.Verbose))
            {
                Log.Logger.Verbose(e.Exception, "First chance exception: {message}", e.Exception.Message);
            }
        }

        private static void LogUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Log.Logger.Fatal(e.ExceptionObject as Exception, "Unhandled exception!");
        }
    }
}