using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Grammophone.Logging;

namespace Grammophone.Domos.Logic
{
	/// <summary>
	/// A logic class which supports logging.
	/// </summary>
	public abstract class Loggable
	{
		#region Private fields

		/// <summary>
		/// Backing field for <see cref="ClassLogger"/> property.
		/// </summary>
		private ILogger classLogger;

		/// <summary>
		/// Environment to use in order to invoke loggers.
		/// </summary>
		private readonly ILogicSessionEnvironment environment;

		#endregion

		#region Construction

		/// <summary>
		/// Create.
		/// </summary>
		/// <param name="environment">Environment to use in order to invoke loggers.</param>
		public Loggable(ILogicSessionEnvironment environment)
		{
			if (environment == null) throw new ArgumentNullException(nameof(environment));

			this.environment = environment;
		}

		#endregion

		#region Protected properties

		/// <summary>
		/// The logger associated with the class.
		/// Uses the name specified in <see cref="GetClassLoggerName"/>.
		/// </summary>
		protected ILogger ClassLogger
		{
			get
			{
				if (classLogger == null)
				{
					classLogger = this.environment.GetLogger(GetClassLoggerName());
				}

				return classLogger;
			}
		}

		#endregion

		#region Protected methods

		/// <summary>
		/// Specifies the name of the logger to use to obtain <see cref="ClassLogger"/>.
		/// The default implementation returns full_class_name[logic configuration section name].
		/// </summary>
		protected virtual string GetClassLoggerName()
		{
			return $"{environment.ConfigurationSectionName}.{GetType().FullName}";
		}

		/// <summary>
		/// Get the logger registered under a given name.
		/// </summary>
		/// <param name="loggerName">The name under which the logger is registered.</param>
		/// <returns>Returns the requested logger.</returns>
		protected ILogger GetLogger(string loggerName) => environment.GetLogger(loggerName);

		#endregion
	}
}
