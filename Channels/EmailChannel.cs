﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Grammophone.Email;
using Grammophone.GenericContentModel;
using Grammophone.TemplateRendering;

namespace Grammophone.Domos.Logic.Channels
{
	/// <summary>
	/// Abstract notification channel for e-mail.
	/// Override <see cref="GetDestinationIdentitiesAsync(IChannelDestination, T)"/> to 
	/// extract e-mail recepients from a destination object.
	/// </summary>
	/// <typeparam name="T">The type of the topic; not used in this implementation.</typeparam>
	public abstract class EmailChannel<T> : IChannel<T>
	{
		#region Constants

		/// <summary>
		/// Key for dynamic property to hold the channel message during rendering.
		/// </summary>
		public const string ChannelMessagePropertyKey = "__ChannelMessage";

		/// <summary>
		/// Key for dynamic property to hold the e-mail recepients.
		/// </summary>
		public const string DestinationIdentitiesPropertyKey = "__DestinationIdentities";

		#endregion

		#region Auxilliary types

		private class IdentitiesToMailAddresses
		{
			public IEnumerable<IChannelIdentity> ChannelIdentities { get; set; }

			public System.Net.Mail.MailAddressCollection MailAddresses { get; set; }
		}

		#endregion

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
		public async Task SendMessageAsync<M>(IChannelMessage<M, T> channelMessage)
		{
			if (channelMessage == null) throw new ArgumentNullException(nameof(channelMessage));

			var destinationIdentities = await GetDestinationIdentitiesAsync(channelMessage.Destination, channelMessage.Topic);

			if (!destinationIdentities.Any()) return;

			bool useSingleMessageForMultipleRecepients = UseSingleMessageForMultipleRecepients(channelMessage.Destination, channelMessage.Topic);

			var emailDestinationAddressesCollection = GetEmailDestinationAddressesCollection(destinationIdentities, useSingleMessageForMultipleRecepients);

			var senderIdentity = await GetSenderIdentityAsync(channelMessage.Source, channelMessage.Topic);

			var senderAddress = GetMailAddress(senderIdentity);

			var destinationIdentitiesByEmail = destinationIdentities.ToReadOnlyMultiDictionary(i => i.Email);

			foreach (var emailDestinationAddresses in emailDestinationAddressesCollection)
			{
				using (var bodyWriter = new System.IO.StringWriter())
				{
					var messageDestinationIdentities = from address in emailDestinationAddresses
																						 where destinationIdentitiesByEmail.ContainsKey(address.Address)
																						 from identity in destinationIdentitiesByEmail[address.Address]
																						 select identity;

					renderProvider.Render(
						GetFullTemplateKey(channelMessage.TemplateKey),
						bodyWriter,
						channelMessage.Model,
						GetDynamicProperties(channelMessage, messageDestinationIdentities));

					string messageBody = bodyWriter.ToString();

					string messageID = GetMessageID(channelMessage, messageDestinationIdentities);

					await SendEmailMessageAsync(
						channelMessage.Subject,
						senderAddress,
						emailDestinationAddresses,
						messageBody,
						messageID);
				}
			}
		}

		/// <summary>
		/// E-mail a message.
		/// </summary>
		/// <param name="channelMessage">The message to send to the channel.</param>
		public async Task SendMessageAsync(IChannelMessage<T> channelMessage)
		{
			if (channelMessage == null) throw new ArgumentNullException(nameof(channelMessage));

			var destinationIdentities = await GetDestinationIdentitiesAsync(channelMessage.Destination, channelMessage.Topic);

			if (!destinationIdentities.Any()) return;

			bool useSingleMessageForMultipleRecepients = UseSingleMessageForMultipleRecepients(channelMessage.Destination, channelMessage.Topic);

			var emailDestinationAddressesCollection = GetEmailDestinationAddressesCollection(destinationIdentities, useSingleMessageForMultipleRecepients);

			var senderIdentity = await GetSenderIdentityAsync(channelMessage.Source, channelMessage.Topic);

			var senderAddress = GetMailAddress(senderIdentity);

			var destinationIdentitiesByEmail = destinationIdentities.ToReadOnlyMultiDictionary(i => i.Email);

			foreach (var emailDestinationAddresses in emailDestinationAddressesCollection)
			{
				using (var bodyWriter = new System.IO.StringWriter())
				{
					var messageDestinationIdentities = from address in emailDestinationAddresses
																						 where destinationIdentitiesByEmail.ContainsKey(address.Address)
																						 from identity in destinationIdentitiesByEmail[address.Address]
																						 select identity;

					renderProvider.Render(
						GetFullTemplateKey(channelMessage.TemplateKey),
						bodyWriter,
						GetDynamicProperties(channelMessage, messageDestinationIdentities));

					string messageBody = bodyWriter.ToString();

					string messageID = GetMessageID(channelMessage, messageDestinationIdentities);

					await SendEmailMessageAsync(
						channelMessage.Subject,
						senderAddress,
						emailDestinationAddresses,
						messageBody,
						messageID);
				}
			}
		}

		#endregion

		#region Protected methods

		/// <summary>
		/// Override to extract e-mail recepients from a destination object and a topic.
		/// </summary>
		protected abstract Task<IEnumerable<IChannelIdentity>> GetDestinationIdentitiesAsync(IChannelDestination destination, T topic);

		/// <summary>
		/// Override to extract e-mail sender from a source object and a topic.
		/// </summary>
		protected abstract Task<IChannelIdentity> GetSenderIdentityAsync(IChannelIdentity source, T topic);

		/// <summary>
		/// If true, send a single e-mail message with multiple recepients, else send one e-mail message per destination.
		/// </summary>
		protected abstract bool UseSingleMessageForMultipleRecepients(IChannelDestination destination, T topic);

		#endregion

		#region Private methods

		private string GetFullTemplateKey(string templateKey) => $"{templateKeyPrefix}{templateKey}";

		private System.Net.Mail.MailAddress GetMailAddress(IChannelIdentity notificationIdentity)
			=> new System.Net.Mail.MailAddress(notificationIdentity.Email, notificationIdentity.Name, Encoding.UTF8);

		private IEnumerable<System.Net.Mail.MailAddressCollection> GetEmailDestinationAddressesCollection(
			IEnumerable<IChannelIdentity> destinationIdentities,
			bool useSingleMessageForMultipleRecepients)
		{
			if (useSingleMessageForMultipleRecepients)
			{
				var mailAddressCollection = new System.Net.Mail.MailAddressCollection();

				foreach (var destinationIdentity in destinationIdentities)
				{
					mailAddressCollection.Add(GetMailAddress(destinationIdentity));
				}

				return new System.Net.Mail.MailAddressCollection[] { mailAddressCollection };
			}
			else
			{
				var mailAddressesSet = new List<System.Net.Mail.MailAddressCollection>(destinationIdentities.Count());

				foreach (var destinationIdentity in destinationIdentities)
				{
					var mailAddressCollection = new System.Net.Mail.MailAddressCollection();

					mailAddressCollection.Add(GetMailAddress(destinationIdentity));

					mailAddressesSet.Add(mailAddressCollection);
				}

				return mailAddressesSet;
			}
		}

		private IEnumerable<IdentitiesToMailAddresses> GetIdentitiesToMailAddressesAssociations(
			IEnumerable<IChannelIdentity> destinationIdentities,
			bool useSingleMessageForMultipleRecepients)
		{
			if (useSingleMessageForMultipleRecepients)
			{
				var mailAddressCollection = new System.Net.Mail.MailAddressCollection();

				foreach (var destinationIdentity in destinationIdentities)
				{
					mailAddressCollection.Add(GetMailAddress(destinationIdentity));
				}

				return new IdentitiesToMailAddresses[]
				{
					new IdentitiesToMailAddresses { ChannelIdentities = destinationIdentities, MailAddresses = mailAddressCollection }
				};
			}
			else
			{
				var identitiesToMailAddressesSet = new List<IdentitiesToMailAddresses>(destinationIdentities.Count());

				foreach (var destinationIdentity in destinationIdentities)
				{
					var mailAddressCollection = new System.Net.Mail.MailAddressCollection();

					mailAddressCollection.Add(GetMailAddress(destinationIdentity));

					identitiesToMailAddressesSet.Add(new IdentitiesToMailAddresses
					{
						MailAddresses = mailAddressCollection,
						ChannelIdentities = new IChannelIdentity[] { destinationIdentity }
					});
				}

				return identitiesToMailAddressesSet;
			}
		}

		private async Task SendEmailMessageAsync(
			string subject,
			System.Net.Mail.MailAddress senderAddress,
			IEnumerable<System.Net.Mail.MailAddress> destinationAddresses,
			string messageBody,
			string messageID)
		{
			var message = new System.Net.Mail.MailMessage()
			{
				HeadersEncoding = Encoding.UTF8,
				From = senderAddress,
				Sender = senderAddress,
				Subject = subject,
				SubjectEncoding = Encoding.UTF8,
				BodyEncoding = Encoding.UTF8,
				IsBodyHtml = true,
				Body = messageBody,
			};

			// RFC 2822 format of message-id
			// get the domain from app settings.
			var domain = System.Configuration.ConfigurationManager.AppSettings["domain"];
			if (!string.IsNullOrEmpty(domain) && domain.StartsWith("."))
			{
				//remove the '.'
				domain = domain.Substring(1);
			}

			var rfc2822MessageID = string.IsNullOrEmpty(domain) ? messageID : $"{messageID}@{domain}";

			message.Headers.Add("Message-ID", rfc2822MessageID);

			using (message)
			{
				foreach (var destinationAddress in destinationAddresses)
				{
					message.To.Add(destinationAddress);
				}

				using (var emailClient = new EmailClient(emailSettings))
				{
					await emailClient.SendEmailAsync(message);
				}
			}
		}

		private static Dictionary<string, object> GetDynamicProperties(IChannelMessage<T> channelMessage, IEnumerable<IChannelIdentity> destinationIdentities)
		{
			Dictionary<string, object> dynamicProperties;

			if (channelMessage.DynamicProperties != null)
				dynamicProperties = new Dictionary<string, object>(channelMessage.DynamicProperties.ToDictionary(e => e.Key, e => e.Value));
			else
				dynamicProperties = new Dictionary<string, object>(2);

			dynamicProperties[ChannelMessagePropertyKey] = channelMessage;
			dynamicProperties[DestinationIdentitiesPropertyKey] = destinationIdentities.ToArray();

			return dynamicProperties;
		}

		/// <summary>
		/// Builds an e-mail message ID of the format <see cref="ChannelMessage{T}.Guid"/>/<see cref="IChannelIdentity.Guid"/>
		/// if the destination identities have a single member,
		/// else returns the <see cref="ChannelMessage{T}.Guid"/>.
		/// </summary>
		/// <param name="channelMessage">The channel message.</param>
		/// <param name="destinationIdentities">The collection of destination identities.</param>
		/// <returns>
		/// Returns message-guid/destination guid if the destination identities have a single member,
		/// else returns message-gruid.
		/// </returns>
		private string GetMessageID(IChannelMessage<T> channelMessage, IEnumerable<IChannelIdentity> destinationIdentities)
		{
			var messageIdBuilder = new StringBuilder();

			messageIdBuilder.Append(channelMessage.Guid);

			if (destinationIdentities.Count() == 1)
			{
				messageIdBuilder.Append("/");

				var singleDestinationIdentity = destinationIdentities.Single();

				messageIdBuilder.Append(singleDestinationIdentity.Guid);
			}

			return messageIdBuilder.ToString();
		}

		#endregion
	}
}
