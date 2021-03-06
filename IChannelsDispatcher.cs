﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Grammophone.Setup;

namespace Grammophone.Domos.Logic
{
	/// <summary>
	/// Contract for sending messages to channels.
	/// </summary>
	/// <typeparam name="T">The type of the topic in the messages.</typeparam>
	public interface IChannelsDispatcher<T>
	{
		/// <summary>
		/// Queue a message to all available channels.
		/// </summary>
		/// <param name="channelMessage">The message to send to the available channels.</param>
		/// <returns>Returns a task whose completion is the successful queuing of the <paramref name="channelMessage"/>.</returns>
		Task QueueMessageToChannelsAsync(IChannelMessage<T> channelMessage);

		/// <summary>
		/// Queue a message to all available channels.
		/// </summary>
		/// <typeparam name="M">The type of the model in the message.</typeparam>
		/// <param name="channelMessage">The message to send to the available channels.</param>
		/// <returns>Returns a task whose completion is the successful queuing of the <paramref name="channelMessage"/>.</returns>
		Task QueueMessageToChannelsAsync<M>(IChannelMessage<M, T> channelMessage);

		/// <summary>
		/// Returns a task whose completion marks that all messages to all channels have been sent, either directly or to a persistent
		/// relay or queue mechanism which will eventually forward them.
		/// </summary>
		Task WhenAllMessagesForwarded();
	}
}
