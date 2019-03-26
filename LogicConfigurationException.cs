using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace Grammophone.Domos.Logic
{
	/// <summary>
	/// Thrown when a required element is missing from the configuration of.
	/// </summary>
	[Serializable]
	public class LogicConfigurationException : LogicException
	{
		#region Construction

		/// <summary>
		/// Create.
		/// </summary>
		/// <param name="configurationSectionName">The name of the faulting configuration section.</param>
		/// <param name="message">The message of the exception</param>
		public LogicConfigurationException(string configurationSectionName, string message)
			: base(message)
		{
			if (configurationSectionName == null) throw new ArgumentNullException(nameof(configurationSectionName));

			this.ConfigurationSectionName = configurationSectionName;
		}

		/// <summary>
		/// Create.
		/// </summary>
		/// <param name="configurationSectionName">The name of the faulting configuration section.</param>
		/// <param name="message">The message of the exception</param>
		/// <param name="inner">The inner exception causing this exception.</param>
		public LogicConfigurationException(string configurationSectionName, string message, Exception inner)
			: base(message, inner)
		{
			if (configurationSectionName == null) throw new ArgumentNullException(nameof(configurationSectionName));

			this.ConfigurationSectionName = configurationSectionName;
		}

		/// <summary>
		/// Used for serialization
		/// </summary>
		protected LogicConfigurationException(SerializationInfo info, StreamingContext context)
			: base(info, context)
		{
			info.AddValue(nameof(this.ConfigurationSectionName), this.ConfigurationSectionName);
		}

		/// <summary>
		/// Deserialize the exception.
		/// </summary>
		public override void GetObjectData(SerializationInfo info, StreamingContext context)
		{
			base.GetObjectData(info, context);

			this.ConfigurationSectionName = info.GetString(nameof(this.ConfigurationSectionName));
		}

		#endregion

		#region Public properties

		/// <summary>
		/// The name of the faulting configuration section.
		/// </summary>
		public string ConfigurationSectionName { get; private set; }

		#endregion
	}
}
