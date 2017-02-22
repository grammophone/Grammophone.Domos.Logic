using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Practices.Unity;
using Microsoft.Practices.Unity.Configuration;

namespace Grammophone.Domos.Logic
{
	/// <summary>
	/// Configures a container using the contents of a configuration section.
	/// Subclass and override method <see cref="Configure(string, IUnityContainer)"/> 
	/// to add programmatic registrations to the container.
	/// </summary>
	public class DefaultConfigurator : Configurator
	{
		/// <summary>
		/// Configure the container by reading a configuration section.
		/// </summary>
		/// <param name="configurationSectionName">The name of the configuration section.</param>
		/// <param name="unityContainer">The container to configure.</param>
		public override void Configure(
			string configurationSectionName,
			IUnityContainer unityContainer)
		{
			if (configurationSectionName == null) throw new ArgumentNullException(nameof(configurationSectionName));
			if (unityContainer == null) throw new ArgumentNullException(nameof(unityContainer));

			var configurationSection = ConfigurationManager.GetSection(configurationSectionName)
				as UnityConfigurationSection;

			if (configurationSection == null)
				throw new LogicException($"The '{configurationSectionName}' configuration section is not defined.");

			unityContainer.LoadConfiguration(configurationSection);
		}
	}
}
