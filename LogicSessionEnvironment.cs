using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Grammophone.Caching;
using Grammophone.Configuration;
using Grammophone.Domos.AccessChecking;
using Grammophone.Domos.DataAccess;
using Grammophone.Domos.Domain;
using Grammophone.TemplateRendering;
using Grammophone.Setup;

namespace Grammophone.Domos.Logic
{
	/// <summary>
	/// Binds a session to its configuration environment
	/// and sets up a unity container using configurator 
	/// of type <see cref="DefaultConfigurator"/>.
	/// </summary>
	/// <typeparam name="U">The type of users in the session.</typeparam>
	/// <typeparam name="D">The type of domain container of the session.</typeparam>
	public class LogicSessionEnvironment<U, D> : ILogicSessionEnvironment, IDisposable
		where U : User
		where D : IUsersDomainContainer<U>
	{
		#region Constants

		/// <summary>
		/// Size for <see cref="storageProvidersCache"/>.
		/// </summary>
		private const int StorageProvidersCacheSize = 16;

		/// <summary>
		/// Name of the logger used to record failures while the asynchronous worker for sending e-mails fails. 
		/// </summary>
		private const string EmailQueueLoggerSuffixName = "EmailQueue";

		/// <summary>
		/// Name of the logger used to record failures when <see cref="PostMessageToChannelsAsync{T}(IChannelMessage{T})"/>
		/// or <see cref="PostMessageToChannelsAsync{M, T}(IChannelMessage{M, T})"/> is invoked. 
		/// </summary>
		private const string ChannelPostLoggerSuffixName = "ChannelPost";

		#endregion

		#region Private fields

		private readonly Lazy<Logging.LoggersRepository> lazyLoggerRepository;

		private readonly Lazy<IReadOnlyDictionary<string, int>> lazyContentTypeIDsByMIME;

		private readonly Lazy<IReadOnlyDictionary<string, string>> lazyContentTypesByExtension;

		private readonly Lazy<IReadOnlyDictionary<string, string>> lazyExtensionsByContentType;

		private readonly MRUCache<string, Storage.IStorageProvider> storageProvidersCache;

		private readonly AsyncWorkQueue<System.Net.Mail.MailMessage> mailQueue;

		private readonly string channelPostLoggerName;

		#endregion

		#region Construction

		/// <summary>
		/// Create.
		/// </summary>
		/// <param name="configurationSectionName">The name of the configuration section in which the <see cref="Settings"/> are defined.</param>
		internal LogicSessionEnvironment(string configurationSectionName)
		{
			if (configurationSectionName == null) throw new ArgumentNullException(nameof(configurationSectionName));

			this.ConfigurationSectionName = configurationSectionName;

			this.Settings = LoadSettings(configurationSectionName);

			var permissionsSetupProvider = this.Settings.Resolve<IPermissionsSetupProvider>();

			this.AccessResolver = new AccessResolver<U>(permissionsSetupProvider);

			lazyLoggerRepository = new Lazy<Logging.LoggersRepository>(
				this.CreateLoggerRepository,
				true);

			lazyContentTypeIDsByMIME = new Lazy<IReadOnlyDictionary<string, int>>(
				this.LoadContentTypeIDsByMIME,
				true);

			lazyContentTypesByExtension = new Lazy<IReadOnlyDictionary<string, string>>(
				this.LoadContentTypesByExtension,
				true);

			lazyExtensionsByContentType = new Lazy<IReadOnlyDictionary<string, string>>(
				this.LoadExtensionsByContentType,
				true);

			storageProvidersCache = new MRUCache<string, Storage.IStorageProvider>(
				name => name != String.Empty ? this.Settings.Resolve<Storage.IStorageProvider>(name) : this.Settings.Resolve<Storage.IStorageProvider>(),
				StorageProvidersCacheSize);

			mailQueue = new AsyncWorkQueue<System.Net.Mail.MailMessage>(
				this,
				SendEmailAsync,
				$"{configurationSectionName}.{EmailQueueLoggerSuffixName}");

			channelPostLoggerName = $"{configurationSectionName}.{ChannelPostLoggerSuffixName}";
		}

		#endregion

		#region Public properties

		/// <summary>
		/// The name of the configuration section in which the <see cref="Settings"/> are defined.
		/// </summary>
		public string ConfigurationSectionName { get; }

		/// <summary>
		/// The access resolver using the <see cref="IPermissionsSetupProvider"/>
		/// specified in <see cref="Settings"/>.
		/// </summary>
		public AccessResolver<U> AccessResolver { get; }

		/// <summary>
		/// Dictionary of content type IDs by MIME.
		/// </summary>
		public IReadOnlyDictionary<string, int> ContentTypeIDsByMIME => lazyContentTypeIDsByMIME.Value;

		/// <summary>
		/// Map of MIME content types by file extensions.
		/// The file extensions include the leading dot and are specified in lower case.
		/// </summary>
		public IReadOnlyDictionary<string, string> ContentTypesByExtension => lazyContentTypesByExtension.Value;

		/// <summary>
		/// Map of file extensions by MIME content types.
		/// The file extensions include the leading dot and are specified in lower case.
		/// </summary>
		public IReadOnlyDictionary<string, string> ExtensionsByContentType => lazyExtensionsByContentType.Value;

		/// <summary>
		/// The Dependency Injection container associated with the environment.
		/// </summary>
		public Settings Settings { get; }

		#endregion

		#region Public methods

		#region Logging

		/// <summary>
		/// Get the logger registered under a specified name.
		/// </summary>
		/// <param name="loggerName">The name under which the logger is registered.</param>
		/// <returns>Returns the <see cref="Logging.ILogger"/> requested.</returns>
		public Logging.ILogger GetLogger(string loggerName) => lazyLoggerRepository.Value.GetLogger(loggerName);

		#endregion

		#region Storage

		/// <summary>
		/// Get a registered storage provider.
		/// </summary>
		/// <param name="providerName">The name under which the provider is registered or null for the default.</param>
		/// <returns>Returns the requested storage provider.</returns>
		public Storage.IStorageProvider GetStorageProvider(string providerName = null)
		{
			if (providerName == null) providerName = String.Empty;

			return storageProvidersCache.Get(providerName);
		}

		#endregion

		#region E-mail

		/// <summary>
		/// Create the configured client for sending e-mail.
		/// </summary>
		public Email.EmailClient CreateEmailClient()
		{
			var emailSettings = this.Settings.Resolve<Email.EmailSettings>();

			return new Email.EmailClient(emailSettings);
		}

		/// <summary>
		/// Send an e-mail.
		/// </summary>
		/// <param name="mailMessage">
		/// The message to send. If its Sender property is not set, 
		/// the configured <see cref="Email.EmailSettings.DefaultSenderAddress"/> is used.
		/// </param>
		/// <returns>Returns a task completing the action.</returns>
		public async Task SendEmailAsync(System.Net.Mail.MailMessage mailMessage)
		{
			if (mailMessage == null) throw new ArgumentNullException(nameof(mailMessage));

			using (var emailClient = this.CreateEmailClient())
			{
				await emailClient.SendEmailAsync(mailMessage);
			}
		}

		/// <summary>
		/// Send an e-mail.
		/// </summary>
		/// <param name="recepients">A list of recepients separated by comma or semicolon.</param>
		/// <param name="subject">The subject of the message.</param>
		/// <param name="body">The body of the message.</param>
		/// <param name="isBodyHTML">If true, the format of the body message is HTML.</param>
		/// <param name="sender">
		/// The sender of the message, is specified, else 
		/// the configured <see cref="Email.EmailSettings.DefaultSenderAddress"/> is used.
		/// </param>
		/// <returns>Returns a task completing the action.</returns>
		/// <remarks>
		/// The message's subject, headers and body encoding is set to UTF8.
		/// </remarks>
		public async Task SendEmailAsync(
			string recepients,
			string subject,
			string body,
			bool isBodyHTML,
			string sender = null)
		{
			if (recepients == null) throw new ArgumentNullException(nameof(recepients));
			if (subject == null) throw new ArgumentNullException(nameof(subject));
			if (body == null) throw new ArgumentNullException(nameof(body));

			using (var emailClient = this.CreateEmailClient())
			{
				await emailClient.SendEmailAsync(
					recepients,
					subject,
					body,
					isBodyHTML,
					sender);
			}
		}

		/// <summary>
		/// Queue an e-mail message to be sent asynchronously.
		/// </summary>
		/// <param name="recepients">A list of recepients separated by comma or semicolon.</param>
		/// <param name="subject">The subject of the message.</param>
		/// <param name="body">The body of the message.</param>
		/// <param name="isBodyHTML">If true, the format of the body message is HTML.</param>
		/// <param name="sender">
		/// The sender of the message, is specified, else 
		/// the configured <see cref="Email.EmailSettings.DefaultSenderAddress"/> is used.
		/// </param>
		/// <returns>Returns a task completing the action.</returns>
		/// <remarks>
		/// The message's subject, headers and body encoding is set to UTF8.
		/// </remarks>
		public void QueueEmail(
			string recepients,
			string subject,
			string body,
			bool isBodyHTML,
			string sender = null)
		{
			if (recepients == null) throw new ArgumentNullException(nameof(recepients));
			if (subject == null) throw new ArgumentNullException(nameof(subject));
			if (body == null) throw new ArgumentNullException(nameof(body));

			var emailSettings = this.Settings.Resolve<Email.EmailSettings>();

			System.Net.Mail.MailAddress senderAddress;

			if (sender != null)
			{
				senderAddress = new System.Net.Mail.MailAddress(sender);
			}
			else
			{
				senderAddress = new System.Net.Mail.MailAddress(emailSettings.DefaultSenderAddress, emailSettings.DefaultSenderDisplayName);
			}

			var mailMessage = new System.Net.Mail.MailMessage
			{
				Sender = senderAddress,
				From = senderAddress,
				Subject = subject,
				Body = body,
				IsBodyHtml = isBodyHTML,
				BodyEncoding = Encoding.UTF8,
				SubjectEncoding = Encoding.UTF8,
				HeadersEncoding = Encoding.UTF8,
			};

			foreach (string recepient in recepients.Split(';', ','))
			{
				mailMessage.To.Add(recepient.Trim());
			}

			QueueEmail(mailMessage);
		}

		/// <summary>
		/// Queue an e-mail message to be sent asynchronously.
		/// </summary>
		/// <param name="mailMessage">The e-mail message to queue.</param>
		public void QueueEmail(System.Net.Mail.MailMessage mailMessage)
		{
			if (mailMessage == null) throw new ArgumentNullException(nameof(mailMessage));

			mailQueue.Enqueue(mailMessage);
		}

		#endregion

		#region Text rendering

		/// <summary>
		/// Get the configured template rendering provider.
		/// </summary>
		public IRenderProvider GetRenderProvider() => this.Settings.Resolve<IRenderProvider>();

		#endregion

		#region Channels messaging

		/// <summary>
		/// Send a notification to the registered <see cref="IChannel{T}"/>s sequentially.
		/// </summary>
		/// <typeparam name="M">The type of the model in the message.</typeparam>
		/// <typeparam name="T">The type of the topic.</typeparam>
		/// <param name="channelMessage">The message to send via the channels.</param>
		/// <returns>Returns a task which is completed when all channel notifications have been completed.</returns>
		public async Task SendMessageToChannelsAsync<M, T>(IChannelMessage<M, T> channelMessage)
		{
			if (channelMessage == null) throw new ArgumentNullException(nameof(channelMessage));

			var channels = this.Settings.ResolveAll<IChannel<T>>();

			foreach (var channel in channels)
			{
				await SendMessageToChannelAsync(channel, channelMessage);
			}
		}

		/// <summary>
		/// Send a notification to the registered <see cref="IChannel{T}"/>s sequentially.
		/// </summary>
		/// <typeparam name="T">The type of the notification topic.</typeparam>
		/// <param name="channelMessage">The message to send via the channels.</param>
		/// <returns>Returns a task which is completed when all channel notifications have been completed.</returns>
		public async Task SendMessageToChannelsAsync<T>(IChannelMessage<T> channelMessage)
		{
			if (channelMessage == null) throw new ArgumentNullException(nameof(channelMessage));

			var channels = this.Settings.ResolveAll<IChannel<T>>();

			foreach (var channel in channels)
			{
				await SendMessageToChannelAsync(channel, channelMessage);
			}
		}

		/// <summary>
		/// Post a notification to the registered <see cref="IChannel{T}"/>s in parallel.
		/// </summary>
		/// <typeparam name="M">The type of the model in the message.</typeparam>
		/// <typeparam name="T">The type of the notification topic.</typeparam>
		/// <param name="channelMessage">The message to send via the channels.</param>
		/// <returns>Returns a task which is completed when all channel notifications have been completed.</returns>
		public Task PostMessageToChannelsAsync<M, T>(IChannelMessage<M, T> channelMessage)
		{
			if (channelMessage == null) throw new ArgumentNullException(nameof(channelMessage));

			var channels = this.Settings.ResolveAll<IChannel<T>>();

			if (!channels.Any()) return Task.CompletedTask;

			var channelsTasks = new List<Task>(channels.Count());

			foreach (var channel in channels)
			{
				var channelTask = Task.Run(async () =>
				{
					await SendMessageToChannelAsync(channel, channelMessage);
				});

				channelsTasks.Add(channelTask);
			}

			return Task.WhenAll(channelsTasks);
		}

		/// <summary>
		/// Post a notification to the registered <see cref="IChannel{T}"/>s in parallel.
		/// </summary>
		/// <typeparam name="T">The type of the notification topic.</typeparam>
		/// <param name="channelMessage">The message to send via the channels.</param>
		/// <returns>Returns a task which is completed when all channel notifications have been completed.</returns>
		public Task PostMessageToChannelsAsync<T>(IChannelMessage<T> channelMessage)
		{
			if (channelMessage == null) throw new ArgumentNullException(nameof(channelMessage));

			var channels = this.Settings.ResolveAll<IChannel<T>>();

			if (!channels.Any()) return Task.CompletedTask;

			var channelsTasks = new List<Task>(channels.Count());

			foreach (var channel in channels)
			{
				var channelTask = Task.Run(async () =>
				{
					await SendMessageToChannelAsync(channel, channelMessage);
				});

				channelsTasks.Add(channelTask);
			}

			return Task.WhenAll(channelsTasks);
		}

		/// <summary>
		/// Queue a message to all available channels.
		/// </summary>
		/// <typeparam name="T">The type of the topic in the message.</typeparam>
		/// <param name="channelMessage">The message to send to the available channels.</param>
		/// <returns>Returns a task whose completion is the successful queuing of the <paramref name="channelMessage"/>.</returns>
		public async Task QueueMessageToChannelsAsync<T>(IChannelMessage<T> channelMessage)
		{
			if (channelMessage == null) throw new ArgumentNullException(nameof(channelMessage));

			IChannelsDispatcher<T> channelsDispatcher = GetChannelsDispatcher<T>();

			await channelsDispatcher.QueueMessageToChannelsAsync(channelMessage);
		}

		/// <summary>
		/// Queue a message to all available channels.
		/// </summary>
		/// <typeparam name="M">The type of the model in the message.</typeparam>
		/// <typeparam name="T">The type of the topic in the messages.</typeparam>
		/// <param name="channelMessage">The message to send to the available channels.</param>
		/// <returns>Returns a task whose completion is the successful queuing of the <paramref name="channelMessage"/>.</returns>
		public async Task QueueMessageToChannelsAsync<M, T>(IChannelMessage<M, T> channelMessage)
		{
			if (channelMessage == null) throw new ArgumentNullException(nameof(channelMessage));

			var channelsDispatcher = GetChannelsDispatcher<T>();

			await channelsDispatcher.QueueMessageToChannelsAsync(channelMessage);
		}

		#endregion

		/// <summary>
		/// Dispose the environment.
		/// </summary>
		public void Dispose()
		{
			this.Settings.Dispose();
			storageProvidersCache.Clear();
		}

		#endregion

		#region Protected methods

		/// <summary>
		/// Load the settings corresponding to a configuration section.
		/// </summary>
		/// <param name="configurationSectionName">The name of the configuration section.</param>
		protected virtual Settings LoadSettings(string configurationSectionName)
			=> Settings.Load(configurationSectionName);

		#endregion

		#region Private methods

		private IReadOnlyDictionary<string, int> LoadContentTypeIDsByMIME()
		{
			using (var domainContainer = this.Settings.Resolve<D>())
			{
				var query = from ct in domainContainer.ContentTypes
										select new { ct.ID, ct.MIME };

				return query.ToDictionary(r => r.MIME, r => r.ID);
			}
		}

		private IReadOnlyDictionary<string, string> LoadContentTypesByExtension()
		{
			var filesConfiguration = this.Settings.Resolve<Configuration.FilesConfiguration>();

			if (filesConfiguration == null)
				throw new LogicConfigurationException(
					this.ConfigurationSectionName,
					"No FilesConfiguration is defined.");

			if (String.IsNullOrWhiteSpace(filesConfiguration.ContentTypeAssociationsXamlPath))
				throw new LogicConfigurationException(
					this.ConfigurationSectionName,
					"The ContentTypeAssociationsXamlPath property of FilesConfiguration is not specified.");

			var contentTypeAssociations =
				XamlConfiguration<Configuration.ContentTypeAssociations>.LoadSettings(
					filesConfiguration.ContentTypeAssociationsXamlPath);

			return contentTypeAssociations.ToDictionary(
				a => a.FileExtension.Trim().ToLower(),
				a => a.MIMEType.Trim());
		}

		private IReadOnlyDictionary<string, string> LoadExtensionsByContentType()
			=> this.ContentTypesByExtension.ToDictionary(e => e.Value, e => e.Key);

		private Logging.LoggersRepository CreateLoggerRepository()
		{
			Logging.ILoggerProvider loggerProvider;

			if (this.Settings.IsRegistered<Logging.ILoggerProvider>())
			{
				loggerProvider = this.Settings.Resolve<Logging.ILoggerProvider>();
			}
			else
			{
				loggerProvider = new Logging.Trace.TraceLoggerProvider();
			}

			return new Logging.LoggersRepository(loggerProvider);
		}

		/// <summary>
		/// Attempt to send a notification to a channel and log any error.
		/// </summary>
		/// <typeparam name="M">The type of the model in the message.</typeparam>
		/// <typeparam name="T">The type of the topic.</typeparam>
		/// <param name="channel">The channel to send the message to.</param>
		/// <param name="channelMessage">The message to send to the channel.</param>
		private async Task SendMessageToChannelAsync<M, T>(IChannel<T> channel, IChannelMessage<M, T> channelMessage)
		{
			try
			{
				await channel.SendMessageAsync(channelMessage);
			}
			catch (Exception e)
			{
				var loggersRepository = lazyLoggerRepository.Value;

				var logger = loggersRepository.GetLogger(channelPostLoggerName);

				logger.Log(
					Logging.LogLevel.Error,
					e,
					$"Failed to send notification with model {channelMessage.Model.GetType().FullName} via channel of type {channel.GetType().FullName}, subject: '{channelMessage.Subject}'");
			}
		}

		/// <summary>
		/// Attempt to send a notification to a channel and log any error.
		/// </summary>
		/// <typeparam name="T">The type of the topic.</typeparam>
		/// <param name="channel">The Channel to send to.</param>
		/// <param name="channelMessage">The message to send via the channel.</param>
		private async Task SendMessageToChannelAsync<T>(IChannel<T> channel, IChannelMessage<T> channelMessage)
		{
			try
			{
				await channel.SendMessageAsync(channelMessage);
			}
			catch (Exception e)
			{
				var loggersRepository = lazyLoggerRepository.Value;

				var logger = loggersRepository.GetLogger(channelPostLoggerName);

				logger.Log(
					Logging.LogLevel.Error,
					e,
					$"Failed to send via channel of type {channel.GetType().FullName}, subject: '{channelMessage.Subject}'");
			}
		}

		private IChannelsDispatcher<T> GetChannelsDispatcher<T>()
		{
			var channelsDispatcher = this.Settings.Resolve<IChannelsDispatcher<T>>();

			if (channelsDispatcher == null)
				throw new LogicConfigurationException(
					this.ConfigurationSectionName,
					$"No IChannelsDispatcher<{typeof(T).FullName}> is defined in the configuration.");

			return channelsDispatcher;
		}

		#endregion
	}

	/// <summary>
	/// Binds a session to its configuration environment
	/// and sets up a unity container using a specified configurator
	/// of type <typeparamref name="C"/>.
	/// </summary>
	/// <typeparam name="U">The type of users in the session.</typeparam>
	/// <typeparam name="D">The type of domain container of the session.</typeparam>
	/// <typeparam name="C">
	/// The type of configurator to use to setup the <see cref="Settings"/>.
	/// property.
	/// </typeparam>
	public class LogicSessionEnvironment<U, D, C> : LogicSessionEnvironment<U, D>
		where U : User
		where D : IUsersDomainContainer<U>
		where C : Configurator, new()
	{
		#region Construiction

		/// <summary>
		/// Create.
		/// </summary>
		/// <param name="configurationSectionName">The name of the Unity configuration section.</param>
		public LogicSessionEnvironment(string configurationSectionName) : base(configurationSectionName)
		{
		}

		#endregion

		#region Protected methods

		/// <summary>
		/// Load the settings corresponding to a configuration section.
		/// </summary>
		/// <param name="configurationSectionName">The name of the configuration section.</param>
		protected override Settings LoadSettings(string configurationSectionName)
			=> Settings.Load<C>(configurationSectionName);

		#endregion
	}
}
