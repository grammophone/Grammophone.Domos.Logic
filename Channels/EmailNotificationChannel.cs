using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Grammophone.Email;
using Grammophone.TemplateRendering;

namespace Grammophone.Domos.Logic.Channels
{
	/// <summary>
	/// Abstract notification channel for e-mail.
	/// Override <see cref="GetDestinationIdentities(object)"/> to 
	/// extract e-mail recepients from a destination object.
	/// </summary>
	/// <typeparam name="T">The type of the topic; not used in this implementation.</typeparam>
	public abstract class EmailChannel<T> : IChannel<T>
	{
		#region Private fields

		private readonly EmailSettings emailSettings;

		private readonly IRenderProvider renderProvider;

		private readonly string templateKeyPrefix;

		#endregion

		#region Construction

		/// <summary>
		/// Create.
		/// </summary>
		/// <param name="emailSettings">The settings to use in order to instanciate an e-mail client.</param>
		/// <param name="renderProvider">The render provider to use in order to provide the body of the mesage.</param>
		/// <param name="templateKeyPrefix">Any template key prefix to add when invoking <paramref name="renderProvider"/> to produce the body of the message.</param>
		public EmailChannel(
			EmailSettings emailSettings,
			IRenderProvider renderProvider,
			string templateKeyPrefix)
		{
			if (emailSettings == null) throw new ArgumentNullException(nameof(emailSettings));
			if (renderProvider == null) throw new ArgumentNullException(nameof(renderProvider));
			if (templateKeyPrefix == null) throw new ArgumentNullException(nameof(templateKeyPrefix));

			this.emailSettings = emailSettings;
			this.renderProvider = renderProvider;
			this.templateKeyPrefix = templateKeyPrefix;
		}

		/// <summary>
		/// Create.
		/// </summary>
		/// <param name="emailSettings">The settings to use in order to instanciate an e-mail client.</param>
		/// <param name="renderProvider">The render provider to use in order to provide the body of the mesage.</param>
		public EmailChannel(
			EmailSettings emailSettings,
			IRenderProvider renderProvider)
			: this(emailSettings, renderProvider, String.Empty)
		{
		}

		#endregion

		#region INotificationChannel<T> implementation

		/// <summary>
		/// E-mail a notification.
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
		public async Task SendAsync<M>(
			string subject,
			string templateKey,
			INotificationIdentity source,
			object destination,
			M model,
			T topic,
			DateTime utcEffectiveDate,
			IReadOnlyDictionary<string, object> dynamicProperties = null)
		{
			if (source == null) throw new ArgumentNullException(nameof(source));

			var destinationIdentities = GetDestinationIdentities(destination);

			if (!destinationIdentities.Any()) return;

			if (subject == null) throw new ArgumentNullException(nameof(subject));
			if (templateKey == null) throw new ArgumentNullException(nameof(templateKey));
			if (model == null) throw new ArgumentNullException(nameof(model));

			using (var bodyWriter = new System.IO.StringWriter())
			{
				renderProvider.Render(GetFullTemplateKey(templateKey), bodyWriter, model, dynamicProperties?.ToDictionary(d => d.Key, e => e.Value));

				string messageBody = bodyWriter.ToString();

				await SendEmailMessageAsync(subject, source, destinationIdentities, messageBody);
			}
		}

		/// <summary>
		/// E-mail a notification.
		/// </summary>
		/// <param name="subject">The subject of the notification.</param>
		/// <param name="templateKey">The key of the template.</param>
		/// <param name="source">The source specifying the sender or system generating the notification.</param>
		/// <param name="destination">The destination of the notification.</param>
		/// <param name="topic">The topic which the notification serves.</param>
		/// <param name="utcEffectiveDate">The generation date of the notification, in UTC.</param>
		/// <param name="dynamicProperties">The dynamic properties.</param>
		public async Task SendAsync(
			string subject,
			string templateKey,
			INotificationIdentity source,
			object destination,
			T topic,
			DateTime utcEffectiveDate,
			IReadOnlyDictionary<string, object> dynamicProperties)
		{
			if (source == null) throw new ArgumentNullException(nameof(source));

			var destinationIdentities = GetDestinationIdentities(destination);

			if (!destinationIdentities.Any()) return;

			if (subject == null) throw new ArgumentNullException(nameof(subject));
			if (templateKey == null) throw new ArgumentNullException(nameof(templateKey));

			using (var bodyWriter = new System.IO.StringWriter())
			{
				renderProvider.Render(GetFullTemplateKey(templateKey), bodyWriter, dynamicProperties?.ToDictionary(d => d.Key, e => e.Value));

				string messageBody = bodyWriter.ToString();

				await SendEmailMessageAsync(subject, source, destinationIdentities, messageBody);
			}
		}

		#endregion

		#region Protected methods

		/// <summary>
		/// Override to extract e-mail recepients from a destination object.
		/// </summary>
		protected abstract IEnumerable<INotificationIdentity> GetDestinationIdentities(object destination);

		#endregion

		#region Private methods

		private string GetFullTemplateKey(string templateKey) => $"{templateKeyPrefix}{templateKey}";

		private System.Net.Mail.MailAddress GetMailAddress(INotificationIdentity notificationIdentity)
			=> new System.Net.Mail.MailAddress(notificationIdentity.Email, notificationIdentity.Name, Encoding.UTF8);

		private async Task SendEmailMessageAsync(
			string subject,
			INotificationIdentity source,
			IEnumerable<INotificationIdentity> destinationIdentities,
			string messageBody)
		{
			var sender = GetMailAddress(source);

			var message = new System.Net.Mail.MailMessage()
			{
				HeadersEncoding = Encoding.UTF8,
				From = sender,
				Sender = sender,
				Subject = subject,
				SubjectEncoding = Encoding.UTF8,
				BodyEncoding = Encoding.UTF8,
				IsBodyHtml = true,
				Body = messageBody,
			};

			using (message)
			{
				foreach (var destinationIdentity in destinationIdentities)
				{
					message.To.Add(GetMailAddress(destinationIdentity));
				}

				using (var emailClient = new EmailClient(emailSettings))
				{
					await emailClient.SendEmailAsync(message);
				}
			}
		}

		#endregion
	}
}
