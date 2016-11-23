using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Grammophone.Domos.DataAccess;
using Grammophone.Domos.Domain;
using Grammophone.Domos.Domain.Files;

namespace Grammophone.Domos.Logic
{
	/// <summary>
	/// Public domain view of a <see cref="IUsersDomainContainer{U}"/>.
	/// </summary>
	/// <typeparam name="U">
	/// The type of the user, derived from <see cref="User"/>.
	/// </typeparam>
	/// <typeparam name="D">
	/// The type of domain container, derived from <see cref="IUsersDomainContainer{U}"/>.
	/// </typeparam>
	/// <remarks>
	/// Public domain instances can be handed off by the session, either as a property,
	/// where the session owns the (secured) domain container, or via a Create method,
	/// where the public domain should own the domain container, created preferrably
	/// using <see cref="Session{U, D}.CreateSecuredDomainContainer"/>.
	/// </remarks>
	public abstract class UsersPublicDomain<U, D> : PublicDomain<D>
		where U : User
		where D : IUsersDomainContainer<U>
	{
		#region Construction

		/// <summary>
		/// Create.
		/// </summary>
		/// <param name="domainContainer">
		/// The domain container to wrap, preferrably with enabled entity access security.
		/// See	<see cref="Session{U, D}.CreateSecuredDomainContainer"/>.
		/// </param>
		/// <param name="ownsDomainContainer">
		/// If true, the public domain instance owns the <paramref name="domainContainer"/>
		/// and the <see cref="PublicDomain{D}.Dispose"/> method will dispose the 
		/// underlying <paramref name="domainContainer"/>.
		/// </param>
		protected UsersPublicDomain(D domainContainer, bool ownsDomainContainer)
			: base(domainContainer, ownsDomainContainer)
		{
		}

		#endregion

		#region Public properties

		/// <summary>
		/// The file content types supported by the system.
		/// </summary>
		public IQueryable<ContentType> ContentTypes => this.DomainContainer.ContentTypes;

		#endregion
	}
}
