#region

using System;
using System.Collections.Generic;
using System.Linq;
using IZ.Core;
using IZ.Logging.SerilogLogging;
using Microsoft.Extensions.Configuration;
using Serilog;
using Serilog.Configuration;
using Serilog.Events;
using Serilog.Formatting;
using Serilog.Sinks.Datadog.Logs;
using Serilog.Sinks.PeriodicBatching;

#endregion

namespace IZ.Observability.DataDog;

public static class DataDogLogging {
#nullable disable
  private static PeriodicBatchingSink DatadogLogSink { get; set; }

  private static LoggerConfiguration CaptureDatadogLogs(
    this LoggerSinkConfiguration loggerConfiguration,
    string apiKey,
    string source = null,
    string service = null,
    string host = null,
    string[] tags = null,
    DatadogConfiguration configuration = null,
    IConfigurationSection configurationSection = null,
    LogEventLevel logLevel = LogEventLevel.Verbose,
    int? batchSizeLimit = null,
    TimeSpan? batchPeriod = null,
    int? queueLimit = null,
    Action<Exception> exceptionHandler = null,
    bool detectTcpDisconnection = false,
    IDatadogClient client = null,
    ITextFormatter formatter = null,
    int? maxMessageSize = null) {
    if (DatadogLogSink != null) return loggerConfiguration.Sink(DatadogLogSink, logLevel);
    if (loggerConfiguration == null)
      throw new ArgumentNullException(nameof(loggerConfiguration));
    if (string.IsNullOrWhiteSpace(apiKey))
      throw new ArgumentNullException(nameof(apiKey));
    var config = ConfigureDatadogConfiguration(configuration, configurationSection);
    DatadogLogSink = (PeriodicBatchingSink) DatadogSink.Create(apiKey, source, service, host, tags, config, batchSizeLimit, batchPeriod, queueLimit, exceptionHandler, detectTcpDisconnection, client, formatter, maxMessageSize);
    return loggerConfiguration.Sink(DatadogLogSink, logLevel);
  }

  private static DatadogConfiguration ConfigureDatadogConfiguration(
    DatadogConfiguration datadogConfiguration,
    IConfigurationSection configurationOption) {
    if (configurationOption == null || !configurationOption.GetChildren().Any())
      return datadogConfiguration ?? new DatadogConfiguration();
    var datadogConfiguration1 = configurationOption.Get<DatadogConfiguration>();
    return new DatadogConfiguration(datadogConfiguration?.Url ?? datadogConfiguration1.Url, datadogConfiguration != null ? datadogConfiguration.Port : datadogConfiguration1.Port, datadogConfiguration != null ? datadogConfiguration.UseSSL : datadogConfiguration1.UseSSL, datadogConfiguration != null ? datadogConfiguration.UseTCP : datadogConfiguration1.UseTCP);
  }

  public static SerilogLogBuilder WriteToDataDog(this SerilogLogBuilder c, ZEnvironment env, params string[] tags) {
    List<string> t = tags.ToList();
    t.AddRange((Environment.GetEnvironmentVariable("DD_TAGS") ?? "").Split(',').Where(s => s.Length > 0));
    t.Add($"env:{env.ToShortString()}");
    c.SerilogConfig.WriteTo.CaptureDatadogLogs(
      "c6a7086c0f65e9219bf1b79095a7467a",
      "csharp",
      ZEnv.ProductName,
      Environment.GetEnvironmentVariable("HOSTNAME") ?? "localhost",
      t.ToArray(),
      new DatadogConfiguration("intake.logs.datadoghq.com", 10516, true, true)
    );
    return c;
  }
}
