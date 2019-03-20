using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Grammophone.Domos.Logic
{
	/// <summary>
	/// Contract for sending messages to channels.
	/// </summary>
	public interface IChannelsQueuer
	{
		/// <summary>
		/// Queue a message to all available channels.
		/// </summary>
		/// <typeparam name="M">The type of the model in the message.</typeparam>
		/// <typeparam name="T">The type of topic in the message.</typeparam>
		/// <param name="channelMessage">The message to send to the available channels.</param>
		/// <returns>Returns a task whose completion is the successful queuing of the <paramref name="channelMessage"/>.</returns>
		Task QueueToChannelsAsync<M, T>(ChannelMessage<M, T> channelMessage);
	}
}
