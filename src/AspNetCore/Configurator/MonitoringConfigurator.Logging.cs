namespace RecShark.AspNetCore.Configurator
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.DependencyInjection.Extensions;
    using Microsoft.Extensions.Hosting;
    using Serilog;
    using Serilog.Core;
    using Serilog.Core.Enrichers;
    using Serilog.Events;

    public static class LoggingConfigurator
    {
        private const string OutputTemplate = "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] [{SourceContext}] {Message}{NewLine}{Exception}";

        private static ILogger apiHealthLogger = null!;

        public static void UseLogging(
            this IApplicationBuilder                app,
            IHostApplicationLifetime                applicationLifetime,
            Action<IDiagnosticContext, HttpContext> enrichDiagnosticContext = null)
        {
            app.UseSerilogRequestLogging(options => { options.EnrichDiagnosticContext = enrichDiagnosticContext; });

            applicationLifetime.ApplicationStarted.Register(OnStarted);
            applicationLifetime.ApplicationStopping.Register(OnStopping);
            applicationLifetime.ApplicationStopped.Register(OnStopped);
        }

        public static void AddLogging(this IServiceCollection services, IConfiguration configuration, Action<LoggerConfiguration> configurator = null)
        {
            var logger = CreateLogger(configuration, configurator);
            services.TryAddSingleton(logger);
            services.AddLogging(loggingBuilder => loggingBuilder.AddSerilog(dispose: true));
        }

        public static ILogger CreateLogger(IConfiguration configuration, Action<LoggerConfiguration> configurator)
        {
            var filename = Path.Combine(AppDomain.CurrentDomain.BaseDirectory ?? "", "logs", "log.txt");

            var host = Dns.GetHostName();
            var serilogConfig = new LoggerConfiguration()
                               .MinimumLevel.Is(LogEventLevel.Information)
                               .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                               .MinimumLevel.Override("System", LogEventLevel.Warning)
                               .ReadFrom.Configuration(configuration)
                               .Enrich.FromLogContext()
                               .Enrich.WithProperty("server-host", host)
                               .Filter.With(new ExcludedPathFilter("/swagger", "/health", "/favicon.ico"))
                               .WriteTo.Console(outputTemplate: OutputTemplate)
                               .WriteTo.File(filename, outputTemplate: OutputTemplate, rollingInterval: RollingInterval.Day);

            configurator?.Invoke(serilogConfig);

            Log.Logger      = serilogConfig.CreateLogger();
            apiHealthLogger = Log.Logger.ForContext(new[] { new PropertyEnricher("SourceContext", "api-health") });
            return Log.Logger;
        }

        private static void OnStarted()
        {
            apiHealthLogger.Information("started");
        }

        private static void OnStopping()
        {
            apiHealthLogger.Information("stopping ...");
        }

        private static void OnStopped()
        {
            apiHealthLogger.Information("stopped");
            Log.CloseAndFlush();
        }

        public class ExcludedPathFilter : ILogEventFilter
        {
            private readonly string[] excludedPaths;

            public ExcludedPathFilter(params string[] excludedPaths)
            {
                this.excludedPaths = excludedPaths;
            }

            public bool IsEnabled(LogEvent logEvent)
            {
                var requestPathValue = logEvent.Properties.GetValueOrDefault("RequestPath") as ScalarValue;
                var requestPath      = requestPathValue?.Value.ToString() ?? "";
                return !this.excludedPaths.Any(x => requestPath.StartsWith(x));
            }
        }
    }
}
