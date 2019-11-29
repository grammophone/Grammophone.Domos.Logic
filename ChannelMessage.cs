using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Grammophone.Domos.Logic
{
	/// <summary>
	/// Message to be consumed by channels.
	/// </summary>
	/// <typeparam name="T">The type of the <see cref="Topic"/>.</typeparam>
	[Serializable]
	public class ChannelMessage<T> : IChannelMessage<T>
	{
		/// <summary>
		/// Create.
		/// </summary>
		/// <param name="subject">The subject of the notification.</param>
		/// <param name="templateKey">The key of the template.</param>
		/// <param name="source">The source specifying the sender or system generating the notification.</param>
		/// <param name="destination">The destination of the notification.</param>
		/// <param name="time">The generation date of the notification, in UTC.</param>
		/// <param name="dynamicProperties">The dynamic properties.</param>
		/// <param name="topic">The topic which the notification serves.</param>
		public ChannelMessage(
			string subject,
			string templateKey,
			IChannelIdentity source,
			IChannelDestination destination,
			DateTime time,
			IReadOnlyDictionary<string, object> dynamicProperties,
			T topic = default(T))
		{
			if (subject == null) throw new ArgumentNullException(nameof(subject));
			if (templateKey == null) throw new ArgumentNullException(nameof(templateKey));
			if (source == null) throw new ArgumentNullException(nameof(source));
			if (destination == null) throw new ArgumentNullException(nameof(destination));
			if (time.Kind != DateTimeKind.Utc) throw new ArgumentException("Time is not in UTC.", nameof(time));

			this.Guid = Guid.NewGuid();

			this.Subject = subject;
			this.TemplateKey = templateKey;
			this.Source = source;
			this.Destination = destination;
			this.Topic = topic;
			this.Time = time;
			this.Time = time;
			this.DynamicProperties = dynamicProperties;
		}

		/// <summary>
		/// The unique identifier of the message.
		/// </summary>
		public Guid Guid { get; }

		/// <summary>
		/// The subject of the message.
		/// </summary>
		public string Subject { get; }

		/// <summary>
		/// The template key to assist in message rendering.
		/// </summary>
		public string TemplateKey { get; }

		/// <summary>
		/// The source of the message.
		/// </summary>
		public IChannelIdentity Source { get; }

		/// <summary>
		/// The destination message.
		/// </summary>
		public IChannelDestination Destination { get; }

		/// <summary>
		/// The topic of the message.
		/// </summary>
		public T Topic { get; }

		/// <summary>
		/// The date and time of the message, in UTC.
		/// </summary>
		public DateTime Time { get; }

		/// <summary>
		/// Dictionary of key-value pairs.
		/// </summary>
		public IReadOnlyDictionary<string, object> DynamicProperties { get; }
	}

	/// <summary>
	/// Message with a strong-type <see cref="Model"/> to be consumed by channels.
	/// </summary>
	/// <typeparam name="M">The type of the <see cref="Model"/>.</typeparam>
	/// <typeparam name="T">The type of the <see cref="ChannelMessage{T}.Topic"/>.</typeparam>
	[Serializable]
	public class ChannelMessage<M, T> : ChannelMessage<T>, IChannelMessage<M, T>
	{
		/// <summary>
		/// Create.
		/// </summary>
		/// <param name="subject">The subject of the notification.</param>
		/// <param name="templateKey">The key of the template.</param>
		/// <param name="source">The source specifying the sender or system generating the notification.</param>
		/// <param name="destination">The destination of the notification.</param>
		/// <param name="model">The model of the notification.</param>
		/// <param name="time">The generation date of the notification, in UTC.</param>
		/// <param name="topic">The topic which the notification serves.</param>
		/// <param name="dynamicProperties">Optional dynamic properties.</param>
		public ChannelMessage(
			string subject,
			string templateKey,
			IChannelIdentity source,
			IChannelDestination destination,
			M model,
			DateTime time,
			T topic = default(T),
			IReadOnlyDictionary<string, object> dynamicProperties = null)
			: base(subject, templateKey, source, destination, time, dynamicProperties, topic)
		{
			this.Model = model;
		}

		/// <summary>
		/// The model to included in the message.
		/// </summary>
		public M Model { get;}
	}
}
