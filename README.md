# aspire-otlp-tyk
Trying to get the Tyk gateway to send OpenTelemetry data to Aspire. In the current mode (all-gRPC, due to setting `ASPIRE_DASHBOARD_OTLP_ENDPOINT_URL` in launchSettings.json), nothing appears in the Aspire dashboard, and Tyk records the following error:

```
{"component":"otel-metrics","level":"error","message":"metric export failedfailed to upload metrics: context deadline exceeded: rpc error: code = DeadlineExceeded desc = context deadline exceeded","time":"2026-09-01T15:22:41Z"}
```
