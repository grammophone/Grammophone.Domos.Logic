# Security And Permissions

Domos security is specified as permissions and assignments. The default provider reads these from XAML through `XamlPermissionsSetupProvider`, but applications may supply another `IPermissionsSetupProvider`.

## Permission Contents

A permission can grant three kinds of access:

- `EntityAccess` grants create, read, write and delete rights over entity types, including own-right variants for `IOwnedEntity<TUser>`.
- `ManagerAccess` grants access to logic manager types exposed by a `LogicSession`.
- `StatePathAccess` grants the right to execute workflow paths by `StatePath.CodeName`.

Permissions are assigned to roles and disposition types. Roles are system-wide. Disposition types are scoped to segregations.

## XAML Setup

The XAML root is `PermissionsSetup`. It contains `PermissionAssignments`, and each assignment contains a `Permission` plus references to roles and disposition types.

For a music-domain application, a simplified setup can look like this:

```xml
<PermissionsSetup xmlns="clr-namespace:Grammophone.Domos.AccessChecking.Configuration;assembly=Grammophone.Domos.AccessChecking"
				  xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
				  xmlns:music="clr-namespace:Music.Domain;assembly=Music.Domain"
				  xmlns:logic="clr-namespace:Music.Logic;assembly=Music.Logic">
	<PermissionsSetup.PermissionAssignments>
		<PermissionAssignment>
			<PermissionAssignment.Permission>
				<Permission CodeName="ManageRecordLabelCatalog">
					<Permission.EntityAccesses>
						<EntityAccess EntityType="{x:Type music:Album}" CanRead="True" CanWrite="True" CanCreate="True" />
						<EntityAccess EntityType="{x:Type music:Track}" CanRead="True" CanWrite="True" CanCreate="True" />
						<EntityAccess EntityType="{x:Type music:Artist}" CanRead="True" CanWrite="True" />
					</Permission.EntityAccesses>
					<Permission.ManagerAccesses>
						<ManagerAccess ManagerType="{x:Type logic:RecordLabelCatalogManager}" />
					</Permission.ManagerAccesses>
					<Permission.StatePathAccesses>
						<StatePathAccess StatePathCodeName="ApproveForRelease" />
					</Permission.StatePathAccesses>
				</Permission>
			</PermissionAssignment.Permission>
			<PermissionAssignment.DispositionTypes>
				<Reference CodeName="RecordLabelAdministrator" />
			</PermissionAssignment.DispositionTypes>
		</PermissionAssignment>
	</PermissionsSetup.PermissionAssignments>
</PermissionsSetup>
```

In this example, the permission is assigned to the `RecordLabelAdministrator` disposition type. A user must have that disposition against the relevant `RecordLabel` for these rights to apply inside that label.

## Entity Access

The `LogicSession` entity listener enforces entity access when entities are read, added, changed or deleted. It checks role permissions first and disposition permissions when the entity is segregated.

Own-rights such as `CanReadOwn` and `CanWriteOwn` apply to entities that implement `IOwnedEntity<TUser>`.

## Manager Access

Concrete session methods should obtain managers through `GetManager` or `TryGetManager`. These methods call the access resolver before returning the manager.

If the manager is scoped to a segregation, pass the segregation ID or entity to `GetManager`. In the music example, `MusicSession.GetRecordLabelCatalogManager(label)` should pass `label.ID` so a `RecordLabelAdministrator` disposition can be checked for that label.

## Workflow Access

Workflow path access is checked by `WorkflowManager` before a state transition is executed. State path permissions are identified by `StatePath.CodeName`.

This allows applications to grant fine-grained rights such as editing an album without allowing approval of the `ApproveForRelease` path.
