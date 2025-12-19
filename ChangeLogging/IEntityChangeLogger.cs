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
	/// <typeparam name="D">
	/// The type of domain container, derived from <see cref="IUsersDomainContainer{U}"/>.
	/// </typeparam>
	public interface IEntityChangeLogger<U, D>
		where U : User
		where D : IUsersDomainContainer<U>
	{
		/// <summary>
		/// Log a change of an entity.
		/// </summary>
		/// <param name="actingUser">The user changing the entity.</param>
		/// <param name="utcTime">The date and time, in UTC.</param>
		/// <param name="changeType">The type of change.</param>
		/// <param name="entity">The entity being changed.</param>
		/// <param name="domainContainer">The domain container where the entity resides.</param>
		/// <remarks>
		/// Care must be taken to make implementations efficient. Also, the <paramref name="domainContainer"/> should be used only for passive and fast operations,
		/// like checking the modification state of entity properties. Please do not use it for database or other state changing operations.
		/// </remarks>
		void LogChange(U actingUser, DateTime utcTime, EntityChangeType changeType, object entity, D domainContainer);
	}
}
