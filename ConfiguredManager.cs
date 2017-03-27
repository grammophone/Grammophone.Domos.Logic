using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Grammophone.Domos.DataAccess;
using Grammophone.Domos.Domain;
using Grammophone.Setup;

namespace Grammophone.Domos.Logic
{
	/// <summary>
	/// Base class for all managers having their own configuration section and handed 
	/// by <see cref="LogicSession{U, D}"/> descendants.
	/// </summary>
	/// <typeparam name="U">
	/// The type of the user in the domain container, derived from <see cref="User"/>.
	/// </typeparam>
	/// <typeparam name="D">
	/// The type of the domain container, derived from <see cref="IUsersDomainContainer{U}"/>.
	/// </typeparam>
	/// <typeparam name="S">
	/// The type of the session, derived from <see cref="LogicSession{U, D}"/>.
	/// </typeparam>
	/// <typeparam name="C">
	/// The type of configurator used to setup the <see cref="ManagerSettings"/> property,
	/// derived from <see cref="Configurator"/>.
	/// </typeparam>
	public abstract class ConfiguredManager<U, D, S, C> : Manager<U, D, S>
		where U : User
		where D : IUsersDomainContainer<U>
		where S : LogicSession<U, D>
		where C : Configurator, new()
	{
		#region Constants

		/// <summary>
		/// The size of the cache of <see cref="settingsFactory"/>.
		/// </summary>
		private const int SettingsCacheSize = 2048;

		#endregion

		#region Private fields

		/// <summary>
		/// Cache of DI conainers by configuration section names.
		/// </summary>
		private static SettingsFactory<C> settingsFactory;

		#endregion

		#region Construction

		/// <summary>
		/// Static initialization.
		/// </summary>
		static ConfiguredManager()
		{
			settingsFactory = new SettingsFactory<C>(SettingsCacheSize);
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

			this.ManagerSettings = settingsFactory.Get(configurationSectionName);
			this.ManagerConfigurationSectionName = configurationSectionName;
		}

		#endregion

		#region Protected properties

		/// <summary>
		/// The settings dedicated to this manager.
		/// </summary>
		protected Settings ManagerSettings { get; private set; }

		/// <summary>
		/// The name of the configuration section associated with the <see cref="ManagerSettings"/>.
		/// </summary>
		protected string ManagerConfigurationSectionName { get; private set; }

		#endregion

		#region Protected methods

		/// <summary>
		/// Flush the cached <see cref="ManagerSettings"/> created for 
		/// some configuration section name.
		/// If the settings existed in the cache, a subsequent new manager
		/// will cause the settings to be reloaded and reconfigured.
		/// </summary>
		/// <param name="configurationSectionName">The name of the Unity configuration section dedicated to the manager.</param>
		/// <returns>Returns true if the settings were found in the cache and removed, else returns false.</returns>
		protected static bool FlushSettings(string configurationSectionName)
			=> settingsFactory.Flush(configurationSectionName);

		#endregion

	}
}
