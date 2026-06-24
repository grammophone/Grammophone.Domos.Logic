# Logic Sessions And Managers

`LogicSession<U, D>` is the main runtime object for a Domos logic layer. It holds the domain container, current user, acting user, access resolver, environment, entity listener and manager instances.

The type parameter `U` is the application user type, derived from `User`. The type parameter `D` is the domain container contract, derived from a Domos data-access interface such as `IUsersDomainContainer<U>` or `IDomosDomainContainer<...>`.

## Acting On Behalf Of A User

A logic session always acts on behalf of a user. Constructors either select a user from the configured domain container or receive enough context to resolve the current user from the hosting environment.

The session has two related user concepts:

- The current session user is the identity on whose behalf the session was created.
- The acting user is the user currently used for authorization and audit decisions.

Normally these are the same. Temporary impersonation scopes can change the acting user for a bounded block of code.

## Entity Listener

`LogicSession` installs an entity listener into the domain container. The listener enforces entity access and updates tracking information when entities are added, changed, read or deleted.

The listener performs checks through `AccessResolver<U>` unless access checks are suppressed by an elevated access scope. It also participates in change logging when change loggers are registered in the environment.

## Elevated Access

Some framework operations need to perform internal work after the outer operation has already been authorized. For those cases, `LogicSession.GetElevatedAccessScope()` creates an `ElevatedAccessScope`.

While the scope is active, entity access checks are suppressed. This is intended for trusted infrastructure code such as workflow actions that need to write transition records, files, accounting entries or notifications as the result of an already-authorized action.

Use elevated scopes narrowly and dispose them deterministically:

```csharp
using (GetElevatedAccessScope())
{
	// Internal operation already justified by a prior access decision.
}
```

## Impersonation

`LogicSession.GetImpersonationScope(...)` temporarily changes the acting user. This is different from elevated access. Impersonation still enforces security, but it enforces it as another user.

In a music-domain administration tool, a support operator might impersonate a `MusicUser` to reproduce a catalog visibility issue. The session remains auditable because the scope records the impersonated and overridden users.

## Managers

`Manager<U, D, S>` is the base class for logic managers. Managers receive the owning session and use its domain container, access resolver and environment. Specialized managers add files, workflow, funds transfer, messaging, reporting or application-specific behavior.

Concrete sessions normally expose manager acquisition methods:

```csharp
public class MusicSession : LogicSession<MusicUser, IMusicDomainContainer>
{
	public RecordLabelCatalogManager GetRecordLabelCatalogManager(RecordLabel label)
	{
		return GetManager(
			() => new RecordLabelCatalogManager(this, label),
			label.ID);
	}
}
```

The `GetManager` methods enforce manager access. The `TryGetManager` variants return `null` instead of throwing when access is denied.

## Manager Access And Segregation

Manager access can be system-wide or segregation-scoped. A manager that operates on all labels can be granted through a role. A manager that operates only within one `RecordLabel` can be granted through a `RecordLabelAdministrator` disposition against that label.

For example, `RecordLabelCatalogManager` can be exposed only when the acting user has a disposition permission for the selected label. The manager can then operate on `Album`, `Artist` and `Track` entities in that label.

## Environment

`LogicSessionEnvironment<U, D>` caches shared services resolved from configuration:

- The domain-container factory.
- The permissions setup provider.
- The access resolver.
- Logging and task services.
- Optional template rendering, storage providers, file configuration, channels and change loggers.

The environment is cached by configuration section name so repeated sessions do not rebuild the same infrastructure.
