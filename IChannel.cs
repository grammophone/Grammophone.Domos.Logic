using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Grammophone.Domos.Logic
{
	/// <summary>
	/// Contract for implementations chanelling messages.
	/// </summary>
	/// <typeparam name="T">The type of notification topics in the system.</typeparam>
	public interface IChannel<in T>
	{
		/// <summary>
		/// Send a message to the channel.
		/// </summary>
		/// <param name="channelMessage">The message to send via the channel.</param>
		Task SendMessageAsync(IChannelMessage<T> channelMessage);

		/// <summary>
		/// Send a message with a strong-type model to the channel.
		/// </summary>
		/// <typeparam name="M">The type of the model in the message.</typeparam>
		/// <param name="channelMessage">The message to send via the channel.</param>
		Task SendMessageAsync<M>(IChannelMessage<M, T> channelMessage);
	}
}
