# Troubleshooting

## No Workflows Show Up In The Dashboard

Check:

- `workflowDefinitionsPath` points to the correct folder
- the folder contains `*.json` files
- the workflow JSON is valid
- the workflow has `id`, `name` and at least one step
- the backend was restarted after code changes
- the frontend was hard refreshed

## Workflow Appears But Cannot Be Started

Check:

- the workflow `id` is stable and explicitly set
- the backend and frontend are both running the latest code
- the workflow returned by `GET /api/workflows` matches what the dashboard shows

## Workflow Shows `0 Steps` Or No Name

This usually means:

- an invalid workflow file was loaded
- property names did not deserialize as expected
- the JSON structure is wrong

The backend now filters out invalid workflows, so these entries should no longer appear once the latest code is running.

## Private GitHub Release Download Fails

Check:

- the repository is correct
- `releaseTag` matches the GitHub release tag exactly
- `assetName` matches exactly
- a valid PAT is available

Recommended GitHub fine-grained PAT permission:

- `Contents: Read`

## Azure DevOps Artifact Download Fails

Check:

- `organization` and `project` are correct
- either `pipelineId` + `runId` or `buildId` is provided
- `artifactName` (or legacy `assetName`) matches the artifact name exactly
- the PAT has permission to read builds or pipeline artifacts

## `patchJson` Fails

Check:

- `filePath` exists
- the file contains valid JSON
- the operation path starts with `$.`
- the target property or array exists for the selected operation

## `restartWindowsService` Fails

Check:

- the service name is correct
- the backend process has sufficient rights
- the service can be restarted manually on the machine
