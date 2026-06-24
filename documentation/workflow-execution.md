# Workflow Execution

`WorkflowManager` executes state paths over stateful entities.

The manager caches state paths and states, checks whether the acting user can execute a path, creates state transition entities and invokes configured workflow actions. Actions can run before or after the transition and can receive action arguments and context values.

`WorkflowAction` is the base for secured workflow actions. It supports argument validation, elevated access helpers and integration with logic sessions.

## Stateful Objects

The domain object must implement `IStateful<U, ST>`, where `ST` derives from `StateTransition<U>`. The workflow manager works over the stateful object and the configured `StatePath`.

In the music-domain example, `Album` can be stateful and `AlbumStateTransition` records publication workflow history. A `MusicWorkflowManager` can expose operations such as submit, approve, reject and archive by calling `ExecuteStatePathAsync` with the relevant path.

## State Path Resolution

`WorkflowManager` can execute a path by code name, ID or loaded `StatePath` object. It can also search for the first path leading from the current state to a desired next state.

Paths are validated against the workflow graph and transition type. For best performance, loaded paths should include previous state, next state and graph metadata.

## Security

Workflow security is separate from entity and manager security. Before executing a path, `WorkflowManager` asks the session access resolver whether the acting user may execute that `StatePath` against the stateful object.

This means a user can be allowed to edit an album draft but not approve it for release. A `RecordLabelAdministrator` disposition can be granted `ApproveForRelease` only for albums in its record label.

## Workflow Actions And Arguments

State paths can specify pre-actions and post-actions. Actions receive a typed context including the session, stateful object, path, transition and argument dictionary.

Dynamic arguments are described by `ParameterSpecification` and are validated before execution. Presentation layers use `ActionExecutionModel` and `StatePathExecutionModel` to bind these dynamic workflow arguments from form data.

In a music publishing workflow, `ApproveForRelease` might require a release date argument. A workflow action can validate the date, mark the album as release-ready, and queue a notification.

## Elevated Actions

Workflow actions often need to write infrastructure records after path access has already been authorized. `WorkflowAction` exposes elevated access helpers for this purpose.

Use these scopes only for internal side effects of an authorized workflow path. They should not be used to bypass user-facing security decisions.
