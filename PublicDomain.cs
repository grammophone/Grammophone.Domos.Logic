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
	/// using <see cref="LogicSession{U, D}.CreateSecuredDomainContainer"/>.
	/// </remarks>
	public abstract class PublicDomain<D> : IContextOwner, IDisposable
		where D : IDomainContainer
	{
		#region Construction

		/// <summary>
		/// Create.
		/// </summary>
		/// <param name="domainContainer">
		/// The domain container to wrap, preferrably with enabled entity access security.
		/// See	<see cref="LogicSession{U, D}.CreateSecuredDomainContainer"/>.
		/// </param>
		/// <param name="ownsDomainContainer">
		/// If true, the public domain instance owns the <paramref name="domainContainer"/>
		/// and the <see cref="Dispose"/> method will dispose the 
		/// underlying <paramref name="domainContainer"/>.
		/// </param>
		protected PublicDomain(D domainContainer, bool ownsDomainContainer)
		{
			if (domainContainer == null) throw new ArgumentNullException(nameof(domainContainer));

			this.DomainContainer = domainContainer;
			this.OwnsDomainContainer = ownsDomainContainer;
		}

		#endregion

		#region Public properties

		// Queryables should be exposed as public properties. Proposed example:

		// IQueryable<MyEntity> MyEntities => this.DomainContainer.MyEntities;

		// ..or maybe add filtering:

		// IQueryable<MyEntity> MyEntities => from e in this.DomainContainer.MyEntities
		//                                    where e.IsPublic
		//                                    select e;

		/// <summary>
		/// The underlying context of the container.
		/// </summary>
		object IContextOwner.UnderlyingContext => DomainContainer.UnderlyingContext;

		#endregion

		#region Protected properties

		/// <summary>
		/// The domain container to wrap, preferrably with enabled entity access security.
		/// See	<see cref="LogicSession{U, D}.CreateSecuredDomainContainer"/>.
		/// </summary>
		protected D DomainContainer { get; private set; }

		/// If true, the public domain instance owns the <see cref="DomainContainer"/>
		/// and the <see cref="Dispose"/> method will dispose the 
		/// underlying <see cref="DomainContainer"/>.
		protected bool OwnsDomainContainer { get; private set; }

		#endregion

		#region Public methods

		/// <summary>
		/// Dispose the instance.
		/// </summary>
		public void Dispose()
		{
			if (this.OwnsDomainContainer) this.DomainContainer.Dispose();
		}

		#endregion
	}
}
