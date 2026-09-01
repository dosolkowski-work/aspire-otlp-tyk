var builder = DistributedApplication.CreateBuilder(args);

var redisPassword = builder.AddParameter("redis-password", "redisLocal", secret: true);
var redis = builder.AddRedis("tyk-cache").WithPassword(redisPassword).WithRedisInsight();

/*var otelCollector = builder
    .AddOpenTelemetryCollector("otel-collector")
    .WithConfig("otel-collector.yaml")
    .WithOtlpExporter();*/

const string tykService = "tyk-gateway";
const string tykOtelFormat = "grpc";
string tykOtelUseTls = bool.FalseString.ToLowerInvariant(); // Using the http launch profile
var tykPassword = builder.AddParameter("tyk-password", "tykLocal", secret: true);
builder
    .AddContainer(tykService, "tykio/tyk-gateway", "v5.14.0")
    // Tried to get HTTPS working which might require custom certificate integration, but no luck with the following
    /*.WithCertificateTrustConfiguration(context =>
    {
        //context.EnvironmentVariables["TYK_GW_OPENTELEMETRY_TLS_CAFILE"] = context.CertificateBundlePath;
        context.EnvironmentVariables["TYK_GW_OPENTELEMETRY_TRACES_TLS_CAFILE"] = context.CertificateBundlePath;
        context.EnvironmentVariables["TYK_GW_OPENTELEMETRY_METRICS_TLS_CAFILE"] = context.CertificateBundlePath;

        return Task.CompletedTask;
    })*/
    .WithOtlpExporter()
    .WithEnvironment(context =>
    {
        if (context.EnvironmentVariables["OTEL_EXPORTER_OTLP_ENDPOINT"] is EndpointReference endpoint)
        {
            // Note that just hard-coding this to "localhost:21024" (matching ASPIRE_DASHBOARD_OTLP_ENDPOINT_URL in launchSettings)
            // does not work either.
            var reference = ReferenceExpression.Create($"{endpoint.Property(EndpointProperty.HostAndPort)}");
            //context.EnvironmentVariables["TYK_GW_OPENTELEMETRY_ENDPOINT"] = reference;
            context.EnvironmentVariables["TYK_GW_OPENTELEMETRY_TRACES_ENDPOINT"] = reference;
            context.EnvironmentVariables["TYK_GW_OPENTELEMETRY_METRICS_ENDPOINT"] = reference;
        }

        if (context.EnvironmentVariables["OTEL_EXPORTER_OTLP_HEADERS"] is string headers)
        {
            // This shouldn't be here because we set Dashboard:Otlp:AuthMode to Unsecured, but it is anyway, and even
            // though it is here, we got 403 errors from trying to send telemetry via http.
            string tykHeaders = headers.Replace("=", ":", StringComparison.Ordinal);
            //context.EnvironmentVariables["TYK_GW_OPENTELEMETRY_HEADERS"] = tykHeaders;
            context.EnvironmentVariables["TYK_GW_OPENTELEMETRY_TRACES_HEADERS"] = tykHeaders;
            context.EnvironmentVariables["TYK_GW_OPENTELEMETRY_METRICS_HEADERS"] = tykHeaders;
        }
    })
    .WithEnvironment("TYK_GW_LOGLEVEL", "debug")
    .WithEnvironment("TYK_GW_LISTENPORT", "8080")
    .WithEnvironment("TYK_GW_CONTROLAPIPORT", "8081")
    .WithEnvironment("TYK_GW_LOGFORMAT", "json")
    .WithEnvironment("TYK_GW_ACCESSLOGS_ENABLED", "true")
    // General OTel
    /*
    .WithEnvironment("TYK_GW_OPENTELEMETRY_ENABLED", "true")
    .WithEnvironment("TYK_GW_OPENTELEMETRY_EXPORTER", tykOtelFormat)
    .WithEnvironment("TYK_GW_OPENTELEMETRY_CONNECTIONTIMEOUT", "10")
    .WithEnvironment("TYK_GW_OPENTELEMETRY_RESOURCENAME", tykService)
    .WithEnvironment("TYK_GW_OPENTELEMETRY_TLS_ENABLE", tykOtelUseTls)
    .WithEnvironment("TYK_GW_OPENTELEMETRY_CONTEXTPROPAGATION", "tracecontext")
    .WithEnvironment("TYK_GW_OPENTELEMETRY_SAMPLING_TYPE", "AlwaysOn")
    */
    // OTel Traces
    .WithEnvironment("TYK_GW_OPENTELEMETRY_TRACES_ENABLED", "true")
    .WithEnvironment("TYK_GW_OPENTELEMETRY_TRACES_EXPORTER", tykOtelFormat)
    .WithEnvironment("TYK_GW_OPENTELEMETRY_TRACES_CONNECTIONTIMEOUT", "10")
    .WithEnvironment("TYK_GW_OPENTELEMETRY_TRACES_RESOURCENAME", tykService)
    .WithEnvironment("TYK_GW_OPENTELEMETRY_TRACES_TLS_ENABLE", tykOtelUseTls)
    .WithEnvironment("TYK_GW_OPENTELEMETRY_TRACES_CONTEXTPROPAGATION", "tracecontext")
    .WithEnvironment("TYK_GW_OPENTELEMETRY_TRACES_SAMPLING_TYPE", "AlwaysOn")
    // OTel Metrics
    .WithEnvironment("TYK_GW_OPENTELEMETRY_METRICS_ENABLED", "true")
    .WithEnvironment("TYK_GW_OPENTELEMETRY_METRICS_EXPORTER", tykOtelFormat)
    .WithEnvironment("TYK_GW_OPENTELEMETRY_METRICS_CONNECTIONTIMEOUT", "10")
    .WithEnvironment("TYK_GW_OPENTELEMETRY_METRICS_RESOURCENAME", tykService)
    .WithEnvironment("TYK_GW_OPENTELEMETRY_METRICS_TLS_ENABLE", tykOtelUseTls)
    .WithEnvironment("TYK_GW_OPENTELEMETRY_METRICS_EXPORTINTERVAL", "15")
    .WithEnvironment("TYK_GW_SECRET", tykPassword)
    .WithEnvironment("TYK_GW_STORAGE_USESSL", "true")
    .WithEnvironment("TYK_GW_STORAGE_HOST", redis.Resource.Host)
    .WithEnvironment("TYK_GW_STORAGE_PORT", redis.Resource.Port)
    .WithEnvironment("TYK_GW_STORAGE_PASSWORD", redisPassword)
    .WithBindMount("TykApps", "/opt/tyk-gateway/apps")
    .WithHttpEndpoint(8080, 8080, "data")
    .WithHttpEndpoint(8081, 8081, "control")
    .WithExternalHttpEndpoints()
    .WithReference(redis)
    .WaitFor(redis);

//.WaitFor(otelCollector);

await builder.Build().RunAsync().ConfigureAwait(false);
