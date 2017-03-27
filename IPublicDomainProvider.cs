using Grammophone.DataAccess;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Grammophone.Domos.Logic
{
	/// <summary>
	/// Interface for an object which is capable of supplying public domain access to entities.
	/// </summary>
	/// <typeparam name="D">The type of domain container, derived from <see cref="IDomainContainer"/>.</typeparam>
	/// <typeparam name="PD">The type of public domain, derived from <see cref="PublicDomain{D}"/>.</typeparam>
	public interface IPublicDomainProvider<D, PD>
		where D : IDomainContainer
		where PD : PublicDomain<D>
	{
		/// <summary>
		/// Get the public domain associated with the session.
		/// </summary>
		/// <remarks>
		/// The wrapped domain container has always entity access security enabled.
		/// </remarks>
		PD PublicDomain { get; }

		/// <summary>
		/// Create a new public domain. The caller is responsible for disposing it.
		/// </summary>
		/// <remarks>
		/// The wrapped domain container has always entity access security enabled.
		/// </remarks>
		PD CreatePublicDomain();
	}
}
