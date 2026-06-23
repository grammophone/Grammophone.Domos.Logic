# Workflow Execution

`WorkflowManager` executes state paths over stateful entities.

The manager caches state paths and states, checks whether the acting user can execute a path, creates state transition entities and invokes configured workflow actions. Actions can run before or after the transition and can receive action arguments and context values.

`WorkflowAction` is the base for secured workflow actions. It supports argument validation, elevated access helpers and integration with logic sessions.

`AccountingAction` and `FundsTransferResponseAction` combine workflow execution with accounting and funds transfer processing.
