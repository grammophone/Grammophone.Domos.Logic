using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Grammophone.Setup;

namespace Grammophone.Domos.Logic.Channels
{
	/// <summary>
	/// Implementation of <see cref="IChannelsDispatcher{T}"/> using the Task Parallel Library.
	/// </summary>
	/// <typeparam name="T">The type of the topic in the messages.</typeparam>
	public class TaskChannelsDispatcher<T> : IChannelsDispatcher<T>
	{
		#region Private fields

		private readonly LogicChannelsTaskQueuer taskQueuer;

		#endregion

		#region Construction

		/// <summary>
		/// Create.
		/// </summary>
		/// <param name="taskQueuer">The task queuer of channel actions.</param>
		public TaskChannelsDispatcher(LogicChannelsTaskQueuer taskQueuer)
		{
			if (taskQueuer == null) throw new ArgumentNullException(nameof(taskQueuer));

			this.taskQueuer = taskQueuer;
		}

		#endregion

		#region IChannelsQueuer<T> implementation

		/// <summary>
		/// Queue a message to all available channels.
		/// </summary>
		/// <param name="settings">The settings of the session environment.</param>
		/// <param name="channelMessage">The message to send to the available channels.</param>
		/// <returns>Returns a task whose completion is the successful queuing of the <paramref name="channelMessage"/>.</returns>
		public Task QueueMessageToChannelsAsync(Settings settings, IChannelMessage<T> channelMessage)
		{
			if (settings == null) throw new ArgumentNullException(nameof(settings));

			var channels = settings.ResolveAll<IChannel<T>>();

			foreach (var channel in channels)
			{
				var channelTask = taskQueuer.QueueAsyncAction(channel, async () =>
				{
					await channel.SendMessageAsync(channelMessage);
				});
			}

			return Task.CompletedTask;
		}

		/// <summary>
		/// Queue a message to all available channels.
		/// </summary>
		/// <typeparam name="M">The type of the model in the message.</typeparam>
		/// <param name="settings">The settings of the session environment.</param>
		/// <param name="channelMessage">The message to send to the available channels.</param>
		/// <returns>Returns a task whose completion is the successful queuing of the <paramref name="channelMessage"/>.</returns>
		public Task QueueMessageToChannelsAsync<M>(Settings settings, IChannelMessage<M, T> channelMessage)
		{
			if (settings == null) throw new ArgumentNullException(nameof(settings));

			var channels = settings.ResolveAll<IChannel<T>>();

			foreach (var channel in channels)
			{
				var channelTask = taskQueuer.QueueAsyncAction(channel, async () =>
				{
					await channel.SendMessageAsync(channelMessage);
				});
			}

			return Task.CompletedTask;
		}

		/// <summary>
		/// Returns a task whose completion marks that all messages to all channels have been sent
		/// by invoking the respecting <see cref="IChannel{T}"/> methods.
		/// </summary>
		public Task WhenAllMessagesForwarded() => taskQueuer.WhenAll();

		#endregion
	}
}
