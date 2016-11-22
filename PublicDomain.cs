using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Grammophone.DataAccess;

namespace Grammophone.Domos.Logic
{
	/// <summary>
	/// Abstract base for public domain instances, providing public access to entities.
	/// Suitable for general read access and for turnkey OData services.
	/// </summary>
	/// <typeparam name="D">
	/// The type of domain container, derived from <see cref="IDomainContainer"/>.
	/// </typeparam>
	/// <remarks>
	/// Public domain instances can be handed off by the session, either as a property,
	/// where the session owns the (secured) domain container, or via a Create method,
	/// where the public domain should own the domain container, created preferrably
	/// using <see cref="Session{U, D}.CreateSecuredDomainContainer"/>.
	/// </remarks>
	public abstract class PublicDomain<D> : IContextOwner, IDisposable
		where D : IDomainContainer
	{
		#region Private fields

		/// <summary>
		/// The domain container to wrap, preferrably with enabled entity access security.
		/// See	<see cref="Session{U, D}.CreateSecuredDomainContainer"/>.
		/// </summary>
		protected readonly D domainContainer;

		/// If true, the public domain instance owns the <see cref="domainContainer"/>
		/// and the <see cref="Dispose"/> method will dispose the 
		/// underlying <see cref="domainContainer"/>.
		protected readonly bool ownsDomainContainer;

		#endregion

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
		/// and the <see cref="Dispose"/> method will dispose the 
		/// underlying <paramref name="domainContainer"/>.
		/// </param>
		protected PublicDomain(D domainContainer, bool ownsDomainContainer)
		{
			if (domainContainer == null) throw new ArgumentNullException(nameof(domainContainer));

			this.domainContainer = domainContainer;
			this.ownsDomainContainer = ownsDomainContainer;
		}

		#endregion

		#region Public properties

		// Queryables should be exposed as public properties. Proposed example:

		// IQueryable<MyEntity> MyEntities => domainContainer.MyEntities;

		/// <summary>
		/// The underlying context of the container.
		/// </summary>
		object IContextOwner.UnderlyingContext => domainContainer.UnderlyingContext;

		#endregion

		#region Public methods

		/// <summary>
		/// Dispose the instance.
		/// </summary>
		public void Dispose()
		{
			if (ownsDomainContainer) domainContainer.Dispose();
		}

		#endregion
	}
}
