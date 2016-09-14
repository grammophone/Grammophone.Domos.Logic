using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Grammophone.Domos.Logic
{
	/// <summary>
	/// A logic class which supports logging.
	/// </summary>
	public abstract class Loggable
	{
		#region Private fields

		/// <summary>
		/// Backing field for the <see cref="Logger"/> property.
		/// </summary>
		private NLog.Logger logger;

		#endregion

		#region Construction

		/// <summary>
		/// Create.
		/// </summary>
		public Loggable()
		{
			string loggerName = GetLoggerName();

			logger = NLog.LogManager.GetLogger(loggerName);
		}

		#endregion

		#region Protected properties

		/// <summary>
		/// The logger associated with the class.
		/// Uses the name specified in <see cref="GetLoggerName"/>.
		/// </summary>
		protected NLog.Logger Logger
		{
			get
			{
				if (logger == null)
				{
					logger = NLog.LogManager.GetLogger(GetLoggerName());
				}

				return logger;
			}
		}

		#endregion

		#region Protected methods

		/// <summary>
		/// Specifies the name of the logger to use.
		/// The default implementation returns the full class name.
		/// </summary>
		protected virtual string GetLoggerName()
		{
			return this.GetType().FullName;
		}

		#endregion
	}
}
