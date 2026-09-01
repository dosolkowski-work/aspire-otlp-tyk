var builder = DistributedApplication.CreateBuilder(args);

var redisPassword = builder.AddParameter("redis-password", "redisLocal", secret: true);
var redis = builder.AddRedis("tyk-cache").WithPassword(redisPassword).WithRedisInsight();

/*var otelCollector = builder
    .AddOpenTelemetryCollector("otel-collector")
    .WithConfig("otel-collector.yaml")
    .WithOtlpExporter();*/

const string tykService = "tyk-gateway";
const string tykOtelFormat = "grpc";
var tykPassword = builder.AddParameter("tyk-password", "tykLocal", secret: true);
builder
    .AddContainer(tykService, "tykio/tyk-gateway", "v5.14.0")
    .WithCertificateTrustConfiguration(context =>
    {
        context.EnvironmentVariables["TYK_GW_OPENTELEMETRY_TRACES_TLS_ENABLE"] = "true";
        context.EnvironmentVariables["TYK_GW_OPENTELEMETRY_TRACES_TLS_CAFILE"] = context.CertificateBundlePath;

        return Task.CompletedTask;
    })
    .WithOtlpExporter(OtlpProtocol.Grpc)
    .WithEnvironment(context =>
    {
        if (context.EnvironmentVariables["OTEL_EXPORTER_OTLP_ENDPOINT"] is EndpointReference endpoint)
        {
            var reference = ReferenceExpression.Create($"{endpoint.Property(EndpointProperty.HostAndPort)}");
            context.EnvironmentVariables["TYK_GW_OPENTELEMETRY_TRACES_ENDPOINT"] = reference;
        }

        if (context.EnvironmentVariables["OTEL_EXPORTER_OTLP_HEADERS"] is string headers)
        {
            string tykHeaders = headers.Replace("=", ":", StringComparison.Ordinal);
            context.EnvironmentVariables["TYK_GW_OPENTELEMETRY_TRACES_HEADERS"] = tykHeaders;
        }
    })
    .WithEnvironment("TYK_GW_LOGLEVEL", "debug")
    .WithEnvironment("TYK_GW_LISTENPORT", "8080")
    .WithEnvironment("TYK_GW_CONTROLAPIPORT", "8081")
    .WithEnvironment("TYK_GW_LOGFORMAT", "json")
    .WithEnvironment("TYK_GW_ACCESSLOGS_ENABLED", "true")
    .WithEnvironment("TYK_GW_OPENTELEMETRY_TRACES_ENABLED", "true")
    .WithEnvironment("TYK_GW_OPENTELEMETRY_TRACES_EXPORTER", tykOtelFormat)
    .WithEnvironment("TYK_GW_OPENTELEMETRY_TRACES_CONNECTIONTIMEOUT", "10")
    .WithEnvironment("TYK_GW_OPENTELEMETRY_TRACES_RESOURCENAME", tykService)
    .WithEnvironment("TYK_GW_OPENTELEMETRY_TRACES_CONTEXTPROPAGATION", "tracecontext")
    .WithEnvironment("TYK_GW_OPENTELEMETRY_TRACES_SAMPLING_TYPE", "AlwaysOn")
    .WithEnvironment("TYK_GW_OPENTELEMETRY_METRICS_ENABLED", "true")
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
