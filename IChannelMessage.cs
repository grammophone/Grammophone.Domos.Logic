using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Grammophone.Domos.Logic
{
	/// <summary>
	/// Contract for a message to be consumed by channels.
	/// </summary>
	/// <typeparam name="T">The type of <see cref="Topic"/>.</typeparam>
	public interface IChannelMessage<out T>
	{
		/// <summary>
		/// The subject of the message.
		/// </summary>
		string Subject { get; }

		/// <summary>
		/// The template key to assist in message rendering.
		/// </summary>
		string TemplateKey { get; }

		/// <summary>
		/// The source of the message.
		/// </summary>
		IChannelIdentity Source { get; }

		/// <summary>
		/// The destination message.
		/// </summary>
		IChannelDestination Destination { get; }

		/// <summary>
		/// The topic of the message.
		/// </summary>
		T Topic { get; }

		/// <summary>
		/// The date and time of the message, in UTC.
		/// </summary>
		DateTime Time { get; }

		/// <summary>
		/// Dictionary of key-value pairs.
		/// </summary>
		IReadOnlyDictionary<string, object> DynamicProperties { get; }
	}

	/// <summary>
	/// Contract for a message with a strong-type <see cref="Model"/> to be consumed by channels.
	/// </summary>
	/// <typeparam name="M">The type of the <see cref="Model"/>.</typeparam>
	/// <typeparam name="T">The type of the <see cref="IChannelMessage{T}.Topic"/>.</typeparam>
	public interface IChannelMessage<out M, out T> : IChannelMessage<T>
	{
		/// <summary>
		/// The model to included in the message.
		/// </summary>
		M Model { get; }
	}
}
