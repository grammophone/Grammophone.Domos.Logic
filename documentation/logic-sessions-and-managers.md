# Logic Sessions And Managers

`LogicSession<U, D>` is the main runtime object for a Domos logic layer. It holds the domain container, current user, acting user, access resolver, environment, entity listener and manager instances.

The session supports login, impersonation, elevated access scopes, entity access checks and change tracking. It installs an entity listener so entity operations can be checked and enriched with user information.

`Manager<U, D, S>` is the base class for logic managers. Managers receive the owning session and use its domain container and access resolver. Specialized managers add files, workflow, funds transfer or other business logic.

Consumer applications normally derive concrete sessions and managers, then configure manager acquisition and access rules around those concrete types.
