using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Grammophone.Caching;
using Grammophone.Domos.DataAccess;
using Grammophone.Domos.Domain;
using Microsoft.Practices.Unity;

namespace Grammophone.Domos.Logic
{
	/// <summary>
	/// Base class for all managers having their own configuration section and handed 
	/// by <see cref="Session{U, D}"/> descendants.
	/// </summary>
	/// <typeparam name="U">The type of the user in the domain container, derived from <see cref="User"/>.</typeparam>
	/// <typeparam name="D">The type of the domain container, derived from <see cref="IUsersDomainContainer{U}"/>.</typeparam>
	/// <typeparam name="S">The type of the session, derived from <see cref="Session{U, D}"/>.</typeparam>
	public abstract class ConfiguredManager<U, D, S> : Manager<U, D, S>
		where U : User
		where D : IUsersDomainContainer<U>
		where S : Session<U, D>
	{
		#region Constants

		/// <summary>
		/// The size of <see cref="diContainersCache"/>.
		/// </summary>
		private const int DIContainersCacheSize = 4096;

		#endregion

		#region Private fields

		/// <summary>
		/// Cache of DI conainers by configuration section names.
		/// </summary>
		private static MRUCache<string, IUnityContainer> diContainersCache;

		#endregion

		#region Construction

		/// <summary>
		/// Static initialization.
		/// </summary>
		static ConfiguredManager()
		{
			diContainersCache = new MRUCache<string, IUnityContainer>(
				Session<U, D>.CreateDIContainer, 
				DIContainersCacheSize);
		}

		/// <summary>
		/// Create.
		/// </summary>
		/// <param name="session">The owning session.</param>
		/// <param name="configurationSectionName">The name of the Unity configuration section dedicated to the manager.</param>
		protected ConfiguredManager(S session, string configurationSectionName)
			: base(session)
		{
			if (session == null) throw new ArgumentNullException(nameof(session));
			if (configurationSectionName == null) throw new ArgumentNullException(nameof(configurationSectionName));

			this.ManagerDIContainer = diContainersCache.Get(configurationSectionName);
		}

		#endregion

		#region Protected properties

		/// <summary>
		/// The unity container dedicated to this manager.
		/// </summary>
		protected IUnityContainer ManagerDIContainer { get; private set; }

		#endregion
	}
}
