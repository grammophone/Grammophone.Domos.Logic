# Consumer Migration Notes

Existing Domos consumers moving to the query-abstraction branch should usually need small changes.

Replace EF query-extension imports:

```csharp
using Grammophone.DataAccess.QueryExtensions;
```

Do not import `System.Data.Entity` or `Microsoft.EntityFrameworkCore` only to get `Include`, `ToListAsync`, `SingleAsync`, `AnyAsync` or similar methods.

Rewrite EF6 collection includes:

```csharp
query.Include(x => x.Children).ThenInclude(c => c.Parent)
```

Use provider adapters as the injected Domos domain container contracts. Keep native EF contexts behind those adapters.

If application code accepts `IDbSet<T>`, change it to `IEntitySet<T>` unless it truly needs provider-specific APIs.
