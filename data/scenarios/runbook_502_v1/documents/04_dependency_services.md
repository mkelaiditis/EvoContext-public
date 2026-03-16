# Troubleshooting 502 Service Errors

When the service returns a 502 error, the most common cause is an unavailable or unresponsive dependency. Administrators should begin by checking the health of each dependency before any other corrective action.

## Dependency health check

To diagnose a 502 error, verify that all dependent services are operating correctly. Check the health status of each dependency using the platform monitoring dashboard or command-line tools.

If a dependency appears unhealthy, restart it first. After it restarts, check the health status of each dependency again before retrying the primary service.

If all dependencies are healthy and the 502 error persists, proceed with log inspection and configuration validation. Begin by locating the most recent log entries for the affected service to identify any startup or runtime errors.
