using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Grammophone.Logging;
using Grammophone.Tasks;

namespace Grammophone.Domos.Logic.Channels
{
	/// <summary>
	/// Task queuer of channel actions.
	/// </summary>
	public class LogicChannelsTaskQueuer : ChannelsTaskQueuer<object>
	{
		#region Private fields

		/// <summary>
		/// The reporitory used to obtain <see cref="ILogger"/> instances to record an exception.
		/// </summary>
		protected readonly LoggersRepository loggersRepository;

		/// <summary>
		/// The logger name to use by default to obtain an <see cref="ILogger"/> from <see cref="loggersRepository"/>.
		/// </summary>
		protected readonly string defaultLoggerName;

		#endregion

		#region Construction

		/// <summary>
		/// Create.
		/// </summary>
		/// <param name="loggerProvider">
		/// The provider of <see cref="ILogger"/> instances to record an exception.
		/// </param>
		/// <param name="defaultLoggerName">
		/// The logger name to use by default to obtain an <see cref="ILogger"/>.
		/// </param>
		public LogicChannelsTaskQueuer(ILoggerProvider loggerProvider, string defaultLoggerName)
		{
			if (loggerProvider == null) throw new ArgumentNullException(nameof(loggerProvider));
			if (defaultLoggerName == null) throw new ArgumentNullException(nameof(defaultLoggerName));

			this.loggersRepository = new LoggersRepository(loggerProvider);
			this.defaultLoggerName = defaultLoggerName;
		}

		#endregion

		#region Protected methods

		/// <summary>
		/// The default implementation uses the <see cref="ILogger"/> returned y <see cref="GetLogger"/>
		/// </summary>
		/// <param name="channel">The channel in which the exception occured.</param>
		/// <param name="exception">The exception during channel dispatching.</param>
		protected override void HandleException(object channel, Exception exception)
		{
			var logger = GetLogger(channel, exception);

			logger.Log(LogLevel.Error, exception, $"Error sending to channel of type '{channel.GetType().FullName}': {exception.Message}");
		}

		/// <summary>
		/// The default implementation uses <see cref="loggersRepository"/> to get the logger
		/// having a name matching <see cref="defaultLoggerName"/>.
		/// </summary>
		/// <param name="channel">The channel in which the exception occured.</param>
		/// <param name="exception">The exception during channel dispatching.</param>
		/// <returns>Returns a logger for recording an exception.</returns>
		protected virtual ILogger GetLogger(object channel, Exception exception)
		{
			return loggersRepository.GetLogger(defaultLoggerName);
		}

		#endregion
	}
}
