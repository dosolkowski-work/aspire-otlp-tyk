var builder = DistributedApplication.CreateBuilder(args);

var redisPassword = builder.AddParameter("redis-password", "redisLocal", secret: true);
var redis = builder.AddRedis("tyk-cache").WithPassword(redisPassword).WithRedisInsight();

var otelCollector = builder
    .AddOpenTelemetryCollector("otel-collector")
    .WithConfig("otel-collector.yaml")
    .WithOtlpExporter();

const string tykService = "tyk-gateway";
var tykPassword = builder.AddParameter("tyk-password", "tykLocal", secret: true);
builder
    .AddContainer(tykService, "tykio/tyk-gateway", "v5.14.0")
    .WithCertificateTrustConfiguration(context =>
    {
        context.EnvironmentVariables["TYK_GW_OPENTELEMETRY_TRACES_TLS_CAFILE"] = context.CertificateBundlePath;

        return Task.CompletedTask;
    })
    .WithOtlpExporter()
    .WithEnvironment(context =>
    {
        if (context.EnvironmentVariables["OTEL_EXPORTER_OTLP_ENDPOINT"] is EndpointReference endpoint)
        {
            // Propagate Aspire OTLP endpoint to Tyk in alternate format with only host and port
            var hostAndPortReference = ReferenceExpression.Create($"{endpoint.Property(EndpointProperty.HostAndPort)}");
            context.EnvironmentVariables["TYK_GW_OPENTELEMETRY_TRACES_ENDPOINT"] = hostAndPortReference;

            // Translate scheme to a flag that indicates whether to use TLS
            var isHttpsReference = ReferenceExpression.CreateConditional(
                endpoint.Property(EndpointProperty.Scheme),
                "https",
                ReferenceExpression.Create($"true"),
                ReferenceExpression.Create($"false"));
            context.EnvironmentVariables["TYK_GW_OPENTELEMETRY_TRACES_TLS_ENABLE"] = isHttpsReference;
        }

        if (context.EnvironmentVariables["OTEL_EXPORTER_OTLP_HEADERS"] is string headers)
        {
            // Translate custom headers to Tyk format (Aspire provides an API key for authentication)
            string tykHeaders = headers.Replace("=", ":", StringComparison.Ordinal);
            context.EnvironmentVariables["TYK_GW_OPENTELEMETRY_TRACES_HEADERS"] = tykHeaders;
        }

        if (context.EnvironmentVariables["OTEL_EXPORTER_OTLP_PROTOCOL"] is string protocol)
        {
            // Propagate protocol as Tyk's "exporter"
            context.EnvironmentVariables["TYK_GW_OPENTELEMETRY_TRACES_EXPORTER"] = protocol;
        }
    })
    // General configuration
    .WithEnvironment("TYK_GW_LOGFORMAT", "json")
    .WithEnvironment("TYK_GW_LOGLEVEL", "debug")
    .WithEnvironment("TYK_GW_LISTENPORT", "8080")
    .WithEnvironment("TYK_GW_CONTROLAPIPORT", "8081")
    .WithEnvironment("TYK_GW_SECRET", tykPassword)
    // Access logs just write to the console and are not related to OpenTelemetry
    .WithEnvironment("TYK_GW_ACCESSLOGS_ENABLED", "true")
    // OTel Traces
    .WithEnvironment("TYK_GW_OPENTELEMETRY_TRACES_ENABLED", "true")
    .WithEnvironment("TYK_GW_OPENTELEMETRY_TRACES_CONNECTIONTIMEOUT", "100000") // Longer timeout is necessary for Aspire! Tyk's default results in timeout errors!
    .WithEnvironment("TYK_GW_OPENTELEMETRY_TRACES_RESOURCENAME", tykService)
    .WithEnvironment("TYK_GW_OPENTELEMETRY_TRACES_CONTEXTPROPAGATION", "tracecontext")
    .WithEnvironment("TYK_GW_OPENTELEMETRY_TRACES_SAMPLING_TYPE", "AlwaysOn")
    // OTel Metrics
    .WithEnvironment("TYK_GW_OPENTELEMETRY_METRICS_ENABLED", "true")
    .WithEnvironment("TYK_GW_OPENTELEMETRY_METRICS_EXPORTINTERVAL", "15")
    // Redis
    .WithEnvironment("TYK_GW_STORAGE_USESSL", "true")
    .WithEnvironment("TYK_GW_STORAGE_HOST", redis.Resource.Host)
    .WithEnvironment("TYK_GW_STORAGE_PORT", redis.Resource.Port)
    .WithEnvironment("TYK_GW_STORAGE_PASSWORD", redisPassword)
    // Docker
    .WithBindMount("TykApps", "/opt/tyk-gateway/apps")
    // Aspire
    .WithHttpEndpoint(8080, 8080, "data")
    .WithHttpEndpoint(8081, 8081, "control")
    .WithExternalHttpEndpoints()
    .WithReference(redis)
    .WaitFor(redis);

await builder.Build().RunAsync().ConfigureAwait(false);
