# Deployment Changelog Review

When investigating service degradation, the deployment changelog provides a record of all changes released to a service.

## Inspecting the deployment history

Open the deployment management portal and locate the changelog for the affected service. Inspect the deployment history to identify any changes released in the recent maintenance window. Each entry includes the deployment identifier, release timestamp, the deploying engineer, and the set of changed components.

## Identifying rollback candidates

A deployment entry that coincides with the onset of degradation is a candidate for investigation. If a recent deployment is identified as the probable cause, initiate a rollback to the previous stable version using the deployment management tool. Specify the rollback version or tag and confirm the rollback completes successfully before re-checking service health.
