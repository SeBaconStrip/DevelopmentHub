# Overview

## Purpose

The workflow engine is designed to automate repeated development and operations tasks without requiring custom code for each action.

Typical use cases:

- downloading release packages
- downloading private release assets
- downloading Azure DevOps pipeline artifacts
- extracting zip files
- running installers
- patching JSON configuration files
- restarting Windows services

## How It Works

1. The backend loads workflow definitions from a configured folder.
2. The dashboard queries the backend for available workflows.
3. Users run workflows from the `Workflows` dashboard panel.
4. If a workflow defines inputs, the dashboard opens a custom input modal.
5. The backend executes the workflow step by step.
6. Logs are streamed live back to the dashboard.

## Main Components

Backend:

- workflow file loader
- workflow execution service
- step handlers
- execution log streaming via SignalR

Frontend:

- workflows dashboard widget
- input modal for workflow parameters
- execution log modal

## Workflow Storage Model

Workflows are file-based.

That means:

- each workflow is defined in one or more `*.json` files
- the backend reads the files from a configured folder
- new workflows can be added by dropping files into that folder
- the database-backed workflow list is legacy data and should not be used for new definitions
