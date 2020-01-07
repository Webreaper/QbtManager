using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace QBTManager.Logging
{
    public static class LogHandler
    {
        public static bool Verbose { get; set; } = false;
        public static bool Trace { get; set; } = false;
        private static Logger logger;
        private static LoggingLevelSwitch logLevel = new LoggingLevelSwitch();
        private const string template = "[{Timestamp:HH:mm:ss.fff}-{ThreadID}-{Level:u3}] {Message:lj}{NewLine}{Exception}";
        public static Logger Logger { get; }

        public static Logger InitLogs()
        {
            logLevel.MinimumLevel = Serilog.Events.LogEventLevel.Information;

            if (Verbose)
                logLevel.MinimumLevel = Serilog.Events.LogEventLevel.Verbose;

            if (Trace)
                logLevel.MinimumLevel = Serilog.Events.LogEventLevel.Debug;

            logger = new LoggerConfiguration()
                .MinimumLevel.ControlledBy(logLevel)
                .WriteTo.Console(outputTemplate: template)
                .WriteTo.File("QbtCleanup-.log", outputTemplate: template,
                               rollingInterval: RollingInterval.Day,
                               fileSizeLimitBytes: 104857600)
                .CreateLogger();

            logger.Information("=== QbtCleanup Log Started ===");
            logger.Information("LogLevel: {0}", logLevel.MinimumLevel);

            return logger;
        }

        public static void EnableDebugLogging(bool enable)
        {
            if (enable)
                logLevel.MinimumLevel = Serilog.Events.LogEventLevel.Debug;
            else
                logLevel.MinimumLevel = Serilog.Events.LogEventLevel.Verbose;

            logger.Information("LogLevel: {0}", logLevel.MinimumLevel);
        }

        public static void LogError(string fmt, params object[] args)
        {
            logger.Error(fmt, args);
        }

        public static void LogWarning(string fmt, params object[] args)
        {
            logger.Warning(fmt, args);
        }

        public static void LogVerbose(string fmt, params object[] args)
        {
            logger.Verbose(fmt, args);
        }

        public static void LogTrace(string fmt, params object[] args)
        {
            logger.Debug(fmt, args);
        }

        public static void Log(string fmt, params object[] args)
        {
            logger.Information(fmt, args);
        }
    }
}
