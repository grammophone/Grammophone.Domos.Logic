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
		/// E-mail a message.
		/// </summary>
		/// <typeparam name="M">The type of the model in the message.</typeparam>
		/// <param name="channelMessage">The message to send to the channel.</param>
		public async Task SendAsync<M>(IChannelMessage<M, T> channelMessage)
		{
			if (channelMessage == null) throw new ArgumentNullException(nameof(channelMessage));

			var destinationIdentities = GetDestinationIdentities(channelMessage.Destination);

			if (!destinationIdentities.Any()) return;

			using (var bodyWriter = new System.IO.StringWriter())
			{
				renderProvider.Render(
					GetFullTemplateKey(channelMessage.TemplateKey),
					bodyWriter,
					channelMessage.Model,
					channelMessage.DynamicProperties?.ToDictionary(d => d.Key, e => e.Value));

				string messageBody = bodyWriter.ToString();

				await SendEmailMessageAsync(channelMessage.Subject, channelMessage.Source, destinationIdentities, messageBody);
			}
		}

		/// <summary>
		/// E-mail a message.
		/// </summary>
		/// <param name="channelMessage">The message to send to the channel.</param>
		public async Task SendAsync(IChannelMessage<T> channelMessage)
		{
			if (channelMessage == null) throw new ArgumentNullException(nameof(channelMessage));

			var destinationIdentities = GetDestinationIdentities(channelMessage.Destination);

			if (!destinationIdentities.Any()) return;

			using (var bodyWriter = new System.IO.StringWriter())
			{
				renderProvider.Render(
					GetFullTemplateKey(channelMessage.TemplateKey),
					bodyWriter,
					channelMessage.DynamicProperties?.ToDictionary(d => d.Key, e => e.Value));

				string messageBody = bodyWriter.ToString();

				await SendEmailMessageAsync(channelMessage.Subject, channelMessage.Source, destinationIdentities, messageBody);
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
