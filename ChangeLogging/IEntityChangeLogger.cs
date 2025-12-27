using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Grammophone.DataAccess;
using Grammophone.Domos.DataAccess;
using Grammophone.Domos.Domain;

namespace Grammophone.Domos.Logic.ChangeLogging
{
	/// <summary>
	/// Interface contract for an entity change logger.
	/// </summary>
	/// <typeparam name="U">
	/// The type of the user, derived from <see cref="User"/>.
	/// </typeparam>
	public interface IEntityChangeLogger<U>
		where U : User
	{
		/// <summary>
		/// Log a change of an entity.
		/// </summary>
		/// <param name="actingUser">The user changing the entity.</param>
		/// <param name="utcTime">The date and time, in UTC.</param>
		/// <param name="changeType">The type of change.</param>
		/// <param name="entityName">The name of the entity.</param>
		/// <param name="entity">The entity being modified.</param>
		/// <param name="propertyStates">The states of the properties of the <paramref name="entity"/>.</param>
		/// <remarks>
		/// Care must be taken to make implementations efficient.
		/// </remarks>
		Task LogChangeAsync(U actingUser, DateTime utcTime, EntityChangeType changeType, string entityName, object entity, IReadOnlyList<PropertyState> propertyStates);
	}
}
