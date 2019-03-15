using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Grammophone.Domos.Logic
{
	/// <summary>
	/// Contract for implementations chanelling notifications.
	/// </summary>
	/// <typeparam name="T">The type of notification topics in the system.</typeparam>
	public interface INotificationChannel<T>
	{
		/// <summary>
		/// Send a notification to the channel.
		/// </summary>
		/// <typeparam name="M">The type of the model.</typeparam>
		/// <param name="subject">The subject of the notification.</param>
		/// <param name="templateKey">The key of the template.</param>
		/// <param name="source">The source specifying the sender or system generating the notification.</param>
		/// <param name="destination">The destination of the notification.</param>
		/// <param name="model">The model of the notification.</param>
		/// <param name="topic">The topic which the notification serves.</param>
		/// <param name="utcEffectiveDate">The generation date of the notification, in UTC.</param>
		/// <param name="dynamicProperties">Optional dynamic properties.</param>
		Task SendAsync<M>(
			string subject,
			string templateKey,
			INotificationIdentity source,
			object destination,
			M model,
			T topic,
			DateTime utcEffectiveDate,
			IReadOnlyDictionary<string, object> dynamicProperties = null);

		/// <summary>
		/// Send a notification to the channel.
		/// </summary>
		/// <param name="subject">The subject of the notification.</param>
		/// <param name="templateKey">The key of the template.</param>
		/// <param name="source">The source specifying the sender or system generating the notification.</param>
		/// <param name="destination">The destination of the notification.</param>
		/// <param name="topic">The topic which the notification serves.</param>
		/// <param name="utcEffectiveDate">The generation date of the notification, in UTC.</param>
		/// <param name="dynamicProperties">The dynamic properties.</param>
		Task SendAsync(
			string subject,
			string templateKey,
			INotificationIdentity source,
			object destination,
			T topic,
			DateTime utcEffectiveDate,
			IReadOnlyDictionary<string, object> dynamicProperties);
	}
}
