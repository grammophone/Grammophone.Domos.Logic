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
		/// <param name="entityEntry">The entry got the entity being changed. Use it to access the entity and its changes.</param>
		/// <remarks>
		/// Care must be taken to make implementations efficient.
		/// </remarks>
		void LogChange(U actingUser, DateTime utcTime, EntityChangeType changeType, IEntityEntry<object> entityEntry);
	}
}
