# Grammophone.Domos.Logic

`Grammophone.Domos.Logic` defines the secured business-logic layer of the Domos integrated session system.

It builds on Domos domain containers and access checking to provide logic sessions, manager acquisition, entity security, workflow execution, funds transfer processing, file storage coordination, channels and change logging.

## Main Features

- `LogicSession<U, D>` coordinates the current user, domain container, environment, access resolver and entity listeners.
- `Manager<U, D, S>` is the base for secured logic managers.
- `WorkflowManager` executes secured state transitions and configured workflow actions.
- `FundsTransferManager` imports/exports funds request and response files and records funds transfer events.
- `FilesManager` coordinates metadata entities with external storage providers.
- Channel and change-logging abstractions support notifications and audit trails.

## Documentation

- [Overview](documentation/overview.md)
- [Logic sessions and managers](documentation/logic-sessions-and-managers.md)
- [Security and permissions](documentation/security-and-permissions.md)
- [Workflow execution](documentation/workflow-execution.md)
- [Funds transfer logic](documentation/funds-transfer-logic.md)
- [Files, channels and change logging](documentation/files-channels-change-logging.md)
- [Consumer migration notes](documentation/consumer-migration-notes.md)
