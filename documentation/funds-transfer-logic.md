# Funds Transfer Logic

`FundsTransferManager` coordinates request-file export and response-file import for funds transfer systems.

It works with file converters, funds request/response models, batch messages and accounting sessions. It can enroll pending requests into batches, export request files, accept response files or lines, record response events and create result summaries.

`CompositeFundsTransferManager` aggregates multiple funds transfer managers and delegates response digestion to the manager responsible for a given line or file.

Workflow-specific funds transfer managers can execute workflow state paths as transfer responses are processed.
