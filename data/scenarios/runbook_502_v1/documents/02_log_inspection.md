# Inspecting Service Logs

Log inspection is the primary diagnostic method when a service fails to start. Begin by locating the most recent log entries produced during the startup attempt.

## Startup log analysis

Pay attention to messages containing "ERROR," "FAILED," or "EXCEPTION." These frequently contain error codes or stack traces identifying the root cause of the startup failure.

Common issues visible in logs include configuration parsing errors and permission problems. Comparing logs from the most recent successful startup with the failed attempt often reveals which change introduced the problem.

Use the relevant log messages to guide the next troubleshooting step.
