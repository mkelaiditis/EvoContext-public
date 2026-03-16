# Restarting the Service

When other troubleshooting steps have been completed and the service still does not respond, a service restart may be required.

## Recommended restart procedure

Confirm the service is not actively processing critical operations. Once safe, stop the service gracefully using the platform management tool so that in-flight operations complete and resources are released before the process terminates.

After stopping the service, wait briefly to allow the operating system to release file handles and ports. Start the service again using the standard management tool command and monitor the logs to confirm initialization completed successfully.
