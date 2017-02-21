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
	/// Contract for setting up a <see cref="IUnityContainer"/>
	/// from a configuration section.
	/// Subclass and override method <see cref="Configure(string, IUnityContainer)"/> 
	/// to add more registrations to the container.
	/// </summary>
	public class Configurator
	{
		/// <summary>
		/// Configure the container by either reading 
		/// a configuration section or programmatically registering
		/// resources.
		/// </summary>
		/// <param name="configurationSectionName">The name of the configuration section.</param>
		/// <param name="unityContainer">The container to configure.</param>
		public virtual void Configure(
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
