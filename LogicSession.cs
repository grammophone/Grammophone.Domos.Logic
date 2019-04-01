using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.Entity;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using Grammophone.Caching;
using Grammophone.DataAccess;
using Grammophone.Domos.AccessChecking;
using Grammophone.Domos.DataAccess;
using Grammophone.Domos.Domain;
using Grammophone.Domos.Environment;
using Grammophone.TemplateRendering;
using Grammophone.Email;
using Grammophone.Setup;
using Grammophone.Logging;

namespace Grammophone.Domos.Logic
{
	/// <summary>
	/// Abstract base for business logic session.
	/// </summary>
	/// <typeparam name="U">
	/// The type of the user, derived from <see cref="Domain.User"/>.
	/// </typeparam>
	/// <typeparam name="D">
	/// The type of domain container, derived from <see cref="IUsersDomainContainer{U}"/>.
	/// </typeparam>
	/// <remarks>
	/// <para>
	/// Each session depends on a Unity DI container defined in a configuration section.
	/// This container must provide resolutions for the following:
	/// <list>
	/// <item><typeparamref name="D"/> (required)</item>
	/// <item>
	/// <see cref="IUserContext"/> (required only when using the constructor which
	/// implies the current user)
	/// </item>
	/// <item><see cref="IPermissionsSetupProvider"/> (required)</item>
	/// <item>
	/// <see cref="IRenderProvider"/>
	/// (required when RenderTemplate methods are used, singleton lifetime is strongly recommended)
	/// </item>
	/// </list>
	/// </para>
	/// <para>
	/// If the system is working with <see cref="Domain.Files.IFile"/> entities,
	/// the session's configuration section must provide resulutions for the following as well:
	/// <list>
	/// <item>
	/// <see cref="Configuration.FilesConfiguration"/> (singleton lifetime is strongly recommended)</item>
	/// <item>
	/// Named and/or unnamed registrations of <see cref="Storage.IStorageProvider"/> implementations
	/// (singleton lifetime is strongly recommended),
	/// whose name (null or not) matches the <see cref="Domain.Files.IFile.ProviderName"/> property.
	/// </item>
	/// </list>
	/// </para>
	/// </remarks>
	public abstract class LogicSession<U, D> : IDisposable
		where U : User
		where D : IUsersDomainContainer<U>
	{
		#region Constants

		/// <summary>
		/// The size of <see cref="sessionEnvironmentsCache"/>.
		/// </summary>
		internal const int SessionEnvironmentsCacheSize = 128;

		#endregion

		#region Private classes

		/// <summary>
		/// Enforces security while accessing entities.
		/// </summary>
		private class EntityListener : Loggable, IUserTrackingEntityListener
		{
			#region Private fields

			private readonly AccessResolver<U> accessResolver;

			private readonly U user;

			private readonly LogicSession<U, D> logicSession;

			#endregion

			#region Construction

			/// <summary>
			/// Create.
			/// </summary>
			/// <param name="logicSession">The logic session which employs the entity listener.</param>
			public EntityListener(LogicSession<U, D> logicSession)
				: base(logicSession.Environment)
			{
				if (logicSession == null) throw new ArgumentNullException(nameof(logicSession));

				this.logicSession = logicSession;
				this.user = logicSession.user; // WARNING: Use the backing field, not the property which is null for anonymous users.
				this.accessResolver = logicSession.AccessResolver;
			}

			#endregion

			#region Public properties

			/// <summary>
			/// If true, entity access check is suppressed and only the CreationDate, ModificationDate
			/// fields are updated.
			/// </summary>
			public bool SupressAccessCheck { get; set; }

			#endregion

			#region Public methods

			public void OnAdding(object entity)
			{
				var utcNow = DateTime.UtcNow;

				if (entity is ICreationLoggingEntity<U> creationLoggingEntity)
				{
					creationLoggingEntity.SetCreator(user, utcNow);
				}

				if (entity is IUpdatableOwnerEntity<U> updatableOwnerEntity)
				{
					if (!updatableOwnerEntity.HasOwners()) updatableOwnerEntity.AddOwner(user);
				}

				if (entity is IChangeLoggingEntity<U> changeLoggingEntity)
				{
					changeLoggingEntity.RecordChange(user, utcNow);
				}

				if (!this.SupressAccessCheck && !accessResolver.CanUserCreateEntity(user, entity))
				{
					LogActionAndThrowAccessDenied(entity, "create");
				}
			}

			public void OnChanging(object entity)
			{
				if (!this.SupressAccessCheck && !accessResolver.CanUserWriteEntity(user, entity))
				{
					LogActionAndThrowAccessDenied(entity, "write");
				}

				if (entity is IChangeLoggingEntity<U> changeLoggingEntity)
				{
					changeLoggingEntity.RecordChange(user, DateTime.UtcNow);
				}
			}

			public void OnDeleting(object entity)
			{
				if (!this.SupressAccessCheck && !accessResolver.CanUserDeleteEntity(user, entity))
				{
					LogActionAndThrowAccessDenied(entity, "delete");
				}
			}

			public void OnRead(object entity)
			{
				if (!this.SupressAccessCheck && !accessResolver.CanUserReadEntity(user, entity))
				{
					LogActionAndThrowAccessDenied(entity, "read");
				}
			}

			#endregion

			#region Protected methods

			/// <summary>
			/// Returns class logger name in the form of "[logic session class logger name].EntityListener".
			/// </summary>
			protected override string GetClassLoggerName() => $"{logicSession.GetClassLoggerName()}.{nameof(EntityListener)}";

			#endregion

			#region Private methods

			/// <summary>
			/// Format an access denial message using the <paramref name="action"/> parameter,
			/// log and throw a <see cref="AccessDeniedDomainException"/>.
			/// </summary>
			/// <param name="entity">The entity to which the access is denied.</param>
			/// <param name="action">The verb form of the action being denied, like "read", "create" etc.</param>
			/// <exception cref="AccessDeniedDomainException">Thrown always.</exception>
			private void LogActionAndThrowAccessDenied(object entity, string action)
			{
				string message;

				string entityName = AccessRight.GetEntityTypeName(entity);

				var entityWithID = entity as IEntityWithID<long>; // Don't inline cast test because covariance wouldn't work.

				if (entityWithID != null)
				{
					if (user.IsAnonymous)
					{
						message = $"The anonymous user cannot {action} an entity of type {entityName} with ID {entityWithID.ID}.";
					}
					else
					{
						message = $"The user with ID {user.ID} cannot {action} an entity of type {entityName} with ID {entityWithID.ID}.";
					}
				}
				else
				{
					if (user.IsAnonymous)
					{
						message = $"The anonymous user cannot {action} an entity of type {entityName}.";
					}
					else
					{
						message = $"The user with ID {user.ID} cannot {action} an entity of type {entityName}.";
					}
				}

				LogAndThrowAccessDenied(entityName, message);
			}

			/// <summary>
			/// Log the denial of access and throw a <see cref="AccessDeniedDomainException"/>.
			/// </summary>
			/// <param name="entityName">The name of the entity to which the access is denied.</param>
			/// <param name="message">The message to log and throw.</param>
			/// <exception cref="AccessDeniedDomainException">Thrown always.</exception>
			private void LogAndThrowAccessDenied(string entityName, string message)
			{
				this.ClassLogger.Log(LogLevel.Warn, message);

				throw new EntityAccessDeniedException(entityName, message);
			}

			#endregion
		}

		#endregion

		#region Private fields

		/// <summary>
		/// Caches <see cref="LogicSessionEnvironment{U, D}"/>s by configuration section names.
		/// </summary>
		private static MRUCache<string, LogicSessionEnvironment<U, D>> sessionEnvironmentsCache;

		/// <summary>
		/// The user owning the session.
		/// </summary>
		private U user;

		/// <summary>
		/// The entity listener enforcing access checking.
		/// </summary>
		private EntityListener entityListener;

		/// <summary>
		/// The nesting level of access elevation.
		/// </summary>
		private int accessElevationNestingLevel;

		/// <summary>
		/// Backing field for the <see cref="ClassLogger"/> property.
		/// </summary>
		private ILogger classLogger;

		#endregion

		#region Construction

		/// <summary>
		/// Static initialization.
		/// </summary>
		static LogicSession()
		{
			sessionEnvironmentsCache = new MRUCache<string, LogicSessionEnvironment<U, D>>(
				configurationSectionName => new LogicSessionEnvironment<U, D>(configurationSectionName), 
				SessionEnvironmentsCacheSize);
		}

		/// <summary>
		/// Create a session impersonating the user specified
		/// in the registered <see cref="IUserContext"/>
		/// inside the configuration
		/// section specified by <paramref name="configurationSectionName"/>.
		/// </summary>
		/// <param name="configurationSectionName">The element name of a Unity configuration section.</param>
		/// <exception cref="LogicException">
		/// Thrown when the resolved <see cref="IUserContext"/> fails to specify an existing user.
		/// </exception>
		/// <remarks>
		/// Each session depends on a Unity DI container defined in a configuration section.
		/// This container must at least provide resolutions for the following:
		/// <list>
		/// <item><typeparamref name="D"/></item>
		/// <item><see cref="IUserContext"/></item>
		/// <item><see cref="IPermissionsSetupProvider"/></item>
		/// </list>
		/// </remarks>
		public LogicSession(string configurationSectionName)
		{
			if (configurationSectionName == null) throw new ArgumentNullException(nameof(configurationSectionName));

			Initialize(configurationSectionName);

			var userContext = this.Settings.Resolve<IUserContext>();

			long? userID = userContext.UserID;

			IQueryable<U> userQuery = this.DomainContainer.Users;

			if (userID.HasValue)
			{
				userQuery = userQuery.Where(u => u.ID == userID.Value);
			}
			else
			{
				userQuery = userQuery.Where(u => u.IsAnonymous);
			}

			Login(userQuery);
		}

		/// <summary>
		/// Create a session impersonating the user specified
		/// using a predicate.
		/// </summary>
		/// <param name="configurationSectionName">The element name of a Unity configuration section.</param>
		/// <param name="userPickPredicate">A predicate to filter a single user.</param>
		/// <exception cref="LogicException">
		/// Thrown when the <paramref name="userPickPredicate"/> fails to specify an existing user.
		/// </exception>
		/// <remarks>
		/// Each session depends on a Unity DI container defined in a configuration section.
		/// This container must at least provide resolutions for the following:
		/// <list>
		/// <item><typeparamref name="D"/></item>
		/// <item><see cref="IPermissionsSetupProvider"/></item>
		/// </list>
		/// </remarks>
		public LogicSession(string configurationSectionName, Expression<Func<U, bool>> userPickPredicate)
		{
			if (configurationSectionName == null) throw new ArgumentNullException(nameof(configurationSectionName));
			if (userPickPredicate == null) throw new ArgumentNullException(nameof(userPickPredicate));

			Initialize(configurationSectionName);

			var userQuery = this.DomainContainer.Users.Where(userPickPredicate);

			Login(userQuery);
		}

		#endregion

		#region Public properties

		/// <summary>
		/// The user owning the session or null of anonymous.
		/// </summary>
		public U User
		{
			get
			{
				if (user.IsAnonymous)
					return null;
				else
					return user;
			}
		}

		/// <summary>
		/// If true, lazy loading is enabled. The default is true.
		/// </summary>
		public bool IsLazyLoadingEnabled
		{
			get
			{
				return this.DomainContainer.IsLazyLoadingEnabled;
			}
			set
			{
				this.DomainContainer.IsLazyLoadingEnabled = value;
			}
		}

		/// <summary>
		/// If set as true and all preconditions are met, the container
		/// will provide proxy classes wherever applicable. Default is true.
		/// </summary>
		public bool IsProxyCreationEnabled
		{
			get
			{
				return this.DomainContainer.IsProxyCreationEnabled;
			}
			set
			{
				this.DomainContainer.IsProxyCreationEnabled = value;
			}
		}

		/// <summary>
		/// The name of the configuration section for this session.
		/// </summary>
		public string ConfigurationSectionName { get; private set; }

		/// <summary>
		/// Provides low and high-level access checking for entities and managers.
		/// </summary>
		public AccessResolver<U> AccessResolver => this.Environment.AccessResolver;

		/// <summary>
		/// The Unity dependency injection container for this session.
		/// </summary>
		public Settings Settings => this.Environment.Settings;

		/// <summary>
		/// The environment of the session.
		/// </summary>
		public LogicSessionEnvironment<U, D> Environment { get; private set; }

		#endregion

		#region Protected properties

		/// <summary>
		/// References the domain model. Do not dispose, because the owner
		/// is the session object.
		/// </summary>
		protected internal D DomainContainer { get; private set; }

		/// <summary>
		/// The logger associated with the session's class.
		/// </summary>
		protected ILogger ClassLogger
		{
			get
			{
				if (classLogger == null)
				{
					classLogger = this.Environment.GetLogger(GetClassLoggerName());
				}

				return classLogger;
			}
		}

		/// <summary>
		/// The acting user. Differs from property <see cref="User"/> when the acting user is anonymous.
		/// In this case, <see cref="User"/> is null but this property contains the anonymous impersonating user.
		/// </summary>
		protected U ActingUser => user;

		#endregion

		#region Public methods

		/// <summary>
		/// Create a container proxy for a new object of type <typeparamref name="T"/>.
		/// </summary>
		/// <typeparam name="T">The type of the object to be proxied.</typeparam>
		/// <returns>Returns a proxy for the new object.</returns>
		public T Create<T>()
			where T : class
		{
			return this.DomainContainer.Create<T>();
		}

		/// <summary>
		/// Get the session environment which corresponds to a configuration section.
		/// </summary>
		/// <param name="configurationSectionName">The name of the configuration section.</param>
		/// <returns>Returns the environment, if successful.</returns>
		public static LogicSessionEnvironment<U, D> GetEnvironment(string configurationSectionName)
		{
			if (configurationSectionName == null) throw new ArgumentNullException(nameof(configurationSectionName));

			return sessionEnvironmentsCache.Get(configurationSectionName);
		}

		#region E-mail sending

		/// <summary>
		/// Send an e-mail.
		/// </summary>
		/// <param name="recepients">A list of recepients separated by comma or semicolon.</param>
		/// <param name="subject">The subject of the message.</param>
		/// <param name="body">The body of the message.</param>
		/// <param name="isBodyHTML">If true, the format of the body message is HTML.</param>
		/// <param name="sender">
		/// The sender of the message, is specified, else 
		/// the configured <see cref="EmailSettings.DefaultSenderAddress"/> is used.
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
			=> await this.Environment.SendEmailAsync(recepients, subject, body, isBodyHTML, sender);

		/// <summary>
		/// Send an e-mail.
		/// </summary>
		/// <param name="mailMessage">
		/// The message to send. If its Sender property is not set, 
		/// the configured <see cref="EmailSettings.DefaultSenderAddress"/> is used.
		/// </param>
		/// <returns>Returns a task completing the action.</returns>
		public async Task SendEmailAsync(System.Net.Mail.MailMessage mailMessage)
			=> await this.Environment.SendEmailAsync(mailMessage);

		/// <summary>
		/// Queue an e-mail message to be sent asynchronously.
		/// </summary>
		/// <param name="recepients">A list of recepients separated by comma or semicolon.</param>
		/// <param name="subject">The subject of the message.</param>
		/// <param name="body">The body of the message.</param>
		/// <param name="isBodyHTML">If true, the format of the body message is HTML.</param>
		/// <param name="sender">
		/// The sender of the message, is specified, else 
		/// the configured <see cref="EmailSettings.DefaultSenderAddress"/> is used.
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
			=> this.Environment.QueueEmail(recepients, subject, body, isBodyHTML, sender);

		/// <summary>
		/// Queue an e-mail message to be sent asynchronously.
		/// </summary>
		/// <param name="mailMessage">The e-mail message to queue.</param>
		public void QueueEmail(System.Net.Mail.MailMessage mailMessage)
			=> this.Environment.QueueEmail(mailMessage);

		#endregion

		#region Text rendering

		/// <summary>
		/// Render a template using a strong-type <paramref name="model"/>.
		/// </summary>
		/// <typeparam name="M">The type of the model.</typeparam>
		/// <param name="templateKey">The key of the template.</param>
		/// <param name="textWriter">The writer used for output.</param>
		/// <param name="model">The model.</param>
		/// <param name="dynamicProperties">Optional dynamic properties.</param>
		public void RenderTemplate<M>(
			string templateKey,
			System.IO.TextWriter textWriter,
			M model,
			IDictionary<string, object> dynamicProperties = null)
		{
			var renderProvider = this.Environment.GetRenderProvider();

			renderProvider.Render(templateKey, textWriter, model, dynamicProperties);
		}

		/// <summary>
		/// Render a template.
		/// </summary>
		/// <param name="templateKey">The key of the template.</param>
		/// <param name="textWriter">The writer used for output.</param>
		/// <param name="dynamicProperties">The dynamic properties.</param>
		public void RenderTemplate(
			string templateKey,
			System.IO.TextWriter textWriter,
			IDictionary<string, object> dynamicProperties)
		{
			var renderProvider = this.Environment.GetRenderProvider();

			renderProvider.Render(templateKey, textWriter, dynamicProperties);
		}

		/// <summary>
		/// Render a template to string using a strong-type <paramref name="model"/>.
		/// </summary>
		/// <typeparam name="M">The type of the model.</typeparam>
		/// <param name="templateKey">The key of the template.</param>
		/// <param name="model">The model.</param>
		/// <param name="dynamicProperties">Optional dynamic properties.</param>
		public string RenderTemplateToString<M>(
			string templateKey,
			M model,
			IDictionary<string, object> dynamicProperties = null)
		{
			using (var writer = new System.IO.StringWriter())
			{
				RenderTemplate(templateKey, writer, model, dynamicProperties);

				return writer.ToString();
			}
		}

		/// <summary>
		/// Render a template to string.
		/// </summary>
		/// <param name="templateKey">The key of the template.</param>
		/// <param name="dynamicProperties">The dynamic properties.</param>
		public string RenderTemplateToString(
			string templateKey,
			IDictionary<string, object> dynamicProperties)
		{
			using (var writer = new System.IO.StringWriter())
			{
				RenderTemplate(templateKey, writer, dynamicProperties);

				return writer.ToString();
			}
		}

		#endregion

		#region Transaction initiation

		/// <summary>
		/// Begins a local transaction on the unerlying store.
		/// Use this method to enroll manager actions under a single transaction.
		/// The caller is responsible
		/// for disposing the object. Use <see cref="ITransaction.Commit"/>
		/// before disposing for accepting the transaction, else rollback will take place.
		/// </summary>
		public ITransaction BeginTransaction()
		{
			return this.DomainContainer.BeginTransaction();
		}

		/// <summary>
		/// Begins a local transaction on the unerlying store.
		/// Use this method to enroll manager actions under a single transaction.
		/// The caller is responsible
		/// for disposing the object. Use <see cref="ITransaction.Commit"/>
		/// before disposing for accepting the transaction, else rollback will take place.
		/// </summary>
		/// <param name="isolationLevel">The transaction isolation level.</param>
		public ITransaction BeginTransaction(System.Data.IsolationLevel isolationLevel)
		{
			return this.DomainContainer.BeginTransaction(isolationLevel);
		}

		#endregion

		#region Virtual file handling

		/// <summary>-
		/// Get the URL of a <see cref="Domain.Files.File"/>.
		/// </summary>
		/// <remarks>
		/// The URL might not allow public access. 
		/// The access behavior is typically controlled by the file's storage container.
		/// </remarks>
		public string GetFileURL(Domain.Files.File file)
		{
			if (file == null) throw new ArgumentNullException(nameof(file));

			var storageProvider = this.Environment.GetStorageProvider(file.ProviderName);

			return $"{storageProvider.URLBase}/{Uri.EscapeDataString(file.ContainerName)}/{Uri.EscapeDataString(file.FullName)}";
		}

		#endregion

		#region Permission checking

		/// <summary>
		/// Determine whether the current user has a permission via the user's roles alone or optionally
		/// via her dispositions against a segregated entity.
		/// </summary>
		/// <param name="permissionCodeName">The code name of the permission.</param>
		/// <param name="segregatedEntity">The optional segregated entity to check user dispositions against.</param>
		public bool HasPermission(string permissionCodeName, ISegregatedEntity segregatedEntity = null)
			=> this.AccessResolver.UserHasPermission(user, permissionCodeName, segregatedEntity);

		/// <summary>
		/// Determine whether the current user has a permission via the user's roles alone or optionally
		/// via her dispositions against a segregation.
		/// </summary>
		/// <param name="permissionCodeName">The code name of the permission.</param>
		/// <param name="segregationID">The ID of the segregation to check user dispositions against.</param>
		public bool HasPermission(string permissionCodeName, long segregationID)
			=> this.AccessResolver.UserHasPermission(user, permissionCodeName, segregationID);

		/// <summary>
		/// Determines whether the current user has a permission as implied from a
		/// user's roles and a disposition she owns as current context.
		/// </summary>
		/// <param name="permissionCodeName">The code name of the permission.</param>
		/// <param name="currentDisposition">The current disposition.</param>
		public bool HasPermissionByDisposition(string permissionCodeName, Disposition currentDisposition)
			=> this.AccessResolver.UserHasPermissionByDisposition(user, permissionCodeName, currentDisposition);

		/// <summary>
		/// Determines whether the current user has a permission as implied from a
		/// user's roles and a disposition she owns as current context.
		/// </summary>
		/// <param name="permissionCodeName">The code name of the permission.</param>
		/// <param name="currentDispositionID">The ID of the current disposition.</param>
		public bool HasPermissionByDisposition(string permissionCodeName, long currentDispositionID)
			=> this.AccessResolver.UserHasPermissionByDisposition(user, permissionCodeName, currentDispositionID);

		#endregion

		#region Notification Channels

		/// <summary>
		/// Send a notification to the registered <see cref="IChannel{T}"/>s sequentially.
		/// </summary>
		/// <typeparam name="M">The type of the model in the message.</typeparam>
		/// <typeparam name="T">The type of the topic.</typeparam>
		/// <param name="channelMessage">The message to send via the channels.</param>
		/// <returns>Returns a task which is completed when all channel notifications have been completed.</returns>
		public async Task SendMessageToChannelsAsync<M, T>(IChannelMessage<M, T> channelMessage)
			=> await this.Environment.SendMessageToChannelsAsync(channelMessage);

		/// <summary>
		/// Send a notification to the registered <see cref="IChannel{T}"/>s sequentially.
		/// </summary>
		/// <typeparam name="T">The type of the notification topic.</typeparam>
		/// <param name="channelMessage">The message to send via the channels.</param>
		/// <returns>Returns a task which is completed when all channel notifications have been completed.</returns>
		public async Task SendMessageToChannelsAsync<T>(IChannelMessage<T> channelMessage)
			=> await this.Environment.SendMessageToChannelsAsync(channelMessage);

		/// <summary>
		/// Post a notification to the registered <see cref="IChannel{T}"/>s in parallel.
		/// </summary>
		/// <typeparam name="M">The type of the model in the message.</typeparam>
		/// <typeparam name="T">The type of the notification topic.</typeparam>
		/// <param name="channelMessage">The message to send via the channels.</param>
		/// <returns>Returns a task which is completed when all channel notifications have been completed.</returns>
		public Task PostMessageToChannelsAsync<M, T>(IChannelMessage<M, T> channelMessage)
			=> this.Environment.PostMessageToChannelsAsync(channelMessage);

		/// <summary>
		/// Post a notification to the registered <see cref="IChannel{T}"/>s in parallel.
		/// </summary>
		/// <typeparam name="T">The type of the notification topic.</typeparam>
		/// <param name="channelMessage">The message to send via the channels.</param>
		/// <returns>Returns a task which is completed when all channel notifications have been completed.</returns>
		public Task PostMessageToChannelsAsync<T>(IChannelMessage<T> channelMessage)
			=> this.Environment.PostMessageToChannelsAsync(channelMessage);

		/// <summary>
		/// Queue a message to all available channels.
		/// </summary>
		/// <typeparam name="T">The type of the topic in the message.</typeparam>
		/// <param name="channelMessage">The message to send to the available channels.</param>
		/// <returns>Returns a task whose completion is the successful queuing of the <paramref name="channelMessage"/>.</returns>
		public async Task QueueMessageToChannelsAsync<T>(IChannelMessage<T> channelMessage)
			=> await this.Environment.QueueMessageToChannelsAsync(channelMessage);

		/// <summary>
		/// Queue a message to all available channels.
		/// </summary>
		/// <typeparam name="M">The type of the model in the message.</typeparam>
		/// <typeparam name="T">The type of the topic in the messages.</typeparam>
		/// <param name="channelMessage">The message to send to the available channels.</param>
		/// <returns>Returns a task whose completion is the successful queuing of the <paramref name="channelMessage"/>.</returns>
		public async Task QueueMessageToChannelsAsync<M, T>(IChannelMessage<M, T> channelMessage)
			=> await this.Environment.QueueMessageToChannelsAsync(channelMessage);

		#endregion

		/// <summary>
		/// Disposes the underlying resources of the <see cref="DomainContainer"/>.
		/// </summary>
		public void Dispose()
		{
			this.DomainContainer.Dispose();
		}

		#endregion

		#region Internal methods

		/// <summary>
		/// Get a scope of elevated access, taking care of nesting.
		/// Please ensure that <see cref="ElevatedAccessScope.Dispose"/> is called in all cases.
		/// </summary>
		/// <remarks>
		/// Until all nested of elevated access scopes are disposed,
		/// no security checks are performed by the session.
		/// </remarks>
		protected internal ElevatedAccessScope GetElevatedAccessScope()
		{
			IncrementAccessElevationLevel();

			return new ElevatedAccessScope(DecrementAccessElevationLevel);
		}

		/// <summary>
		/// Get the session environment corresponding to a configuration section name.
		/// </summary>
		internal virtual LogicSessionEnvironment<U, D> ResolveEnvironment()
		{
			return GetEnvironment(this.ConfigurationSectionName);
		}

		#endregion

		#region Protected methods

		/// <summary>
		/// Get the name of the logger used in <see cref="ClassLogger"/> property.
		/// </summary>
		/// <returns>Returns the name of the logger to use for the <see cref="ClassLogger"/> property.</returns>
		/// <remarks>
		/// The default implementation yields [configuration section name].[full type name of the session class].
		/// </remarks>
		protected virtual string GetClassLoggerName() => $"{this.ConfigurationSectionName}.{GetType().FullName}";

		/// <summary>
		/// Elevate access to all entities for the duration of a <paramref name="transaction"/>,
		/// taking care of any nesting.
		/// </summary>
		/// <param name="transaction">The transaction.</param>
		/// <remarks>
		/// This is suitable for domain containers having <see cref="TransactionMode.Deferred"/>,
		/// where the saving takes place at the topmost transaction.
		/// The <see cref="GetElevatedAccessScope"/> method for elevating access rights might
		/// restore them too soon.
		/// </remarks>
		protected internal void ElevateTransactionAccessRights(ITransaction transaction)
		{
			if (transaction == null) throw new ArgumentNullException(nameof(transaction));

			transaction.Succeeding += DecrementAccessElevationLevel;
			transaction.RollingBack += DecrementAccessElevationLevel;

			IncrementAccessElevationLevel();
		}

		/// <summary>
		/// Override to specify any additional eager fetches along the current user
		/// during session login.
		/// </summary>
		/// <param name="userQuery">The query to append.</param>
		/// <returns>Returns the appended query.</returns>
		/// <remarks>
		/// The default implementatin does nothing and returns
		/// the <paramref name="userQuery"/> unchanged.
		/// </remarks>
		protected virtual IQueryable<U> IncludeWithUser(IQueryable<U> userQuery)
		{
			return userQuery;
		}

		/// <summary>
		/// Checks whether the current user has access to a manager
		/// depending on her roles and her dispositions.
		/// </summary>
		/// <param name="managerType">The type of the manager.</param>
		/// <param name="dispositionID">The ID of a disposition the user owns.</param>
		/// <remarks>
		/// If the type of the manager is generic, it uses the full
		/// name of its generic type definition.
		/// If the user doesn't own the disposition, the method returns false.
		/// </remarks>
		protected bool CanAccessManagerByDisposition(Type managerType, long dispositionID)
		{
			if (managerType == null) throw new ArgumentNullException(nameof(managerType));

			return this.AccessResolver.CanUserAccessManagerByDisposition(user, managerType, dispositionID);
		}

		/// <summary>
		/// Checks whether the current user has access to a manager
		/// depending on her roles and her dispositions.
		/// </summary>
		/// <param name="managerType">The type of the manager.</param>
		/// <param name="disposition">A disposition the user owns.</param>
		/// <remarks>
		/// If the type of the manager is generic, it uses the full
		/// name of its generic type definition.
		/// If the user doesn't own the disposition, the method returns false.
		/// </remarks>
		protected bool CanAccessManagerByDisposition(Type managerType, Disposition disposition)
		{
			if (managerType == null) throw new ArgumentNullException(nameof(managerType));
			if (disposition == null) throw new ArgumentNullException(nameof(disposition));

			return this.AccessResolver.CanUserAccessManagerByDisposition(user, managerType, disposition);
		}

		/// <summary>
		/// Checks whether the current user has access to a manager,
		/// </summary>
		/// <param name="managerType">The type of the manager.</param>
		/// <param name="segregatedEntity">
		/// If specified, the segregated entity to manage. It allows access checking 
		/// based on the current user's dispositions against the segregation, beyond user roles.
		/// Otherwise, checks whether the current user's roles suffice to manage entities of all segregations.
		/// </param>
		protected bool CanAccessManager(Type managerType, ISegregatedEntity segregatedEntity = null)
		{
			if (managerType == null) throw new ArgumentNullException(nameof(managerType));

			return this.AccessResolver.CanUserAccessManager(user, managerType, segregatedEntity);
		}

		/// <summary>
		/// Checks whether the current user has access to a manager
		/// for an entity under a segregation.
		/// </summary>
		/// <param name="managerType">The type of the manager.</param>
		/// <param name="segregationID">
		/// The ID of the segregation. It allows access cheking 
		/// based on the current user's dispositions against the segregation, beyond user roles.
		/// </param>
		protected bool CanAccessManager(Type managerType, long segregationID)
		{
			if (managerType == null) throw new ArgumentNullException(nameof(managerType));

			return this.AccessResolver.CanUserAccessManager(user, managerType, segregationID);
		}

		/// <summary>
		/// Get a manager of type <typeparamref name="M"/>
		/// for an entity under a segregation
		/// or null if the user is not authorized.
		/// </summary>
		/// <typeparam name="M">The type of the manager.</typeparam>
		/// <param name="managerCreator">
		/// The function to construct the manager.
		/// </param>
		protected M TryGetManager<M>(Func<M> managerCreator)
			where M : class
		{
			if (managerCreator == null) throw new ArgumentNullException(nameof(managerCreator));

			if (!CanAccessManager(typeof(M))) return null;

			return managerCreator.Invoke();
		}

		/// <summary>
		/// Get a manager of type <typeparamref name="M"/> asynchronously
		/// for an entity under a segregation
		/// or null if the user is not authorized.
		/// </summary>
		/// <typeparam name="M">The type of the manager.</typeparam>
		/// <param name="asyncManagerCreator">
		/// The asynchronous function to construct the manager.
		/// </param>
		protected async Task<M> TryGetManagerAsync<M>(Func<Task<M>> asyncManagerCreator)
			where M : class
		{
			if (asyncManagerCreator == null) throw new ArgumentNullException(nameof(asyncManagerCreator));

			if (!CanAccessManager(typeof(M))) return null;

			return await asyncManagerCreator.Invoke();
		}

		/// <summary>
		/// Get a manager of type <typeparamref name="M"/>
		/// for an entity under a segregation
		/// or null if the user is not authorized.
		/// </summary>
		/// <typeparam name="M">The type of the manager.</typeparam>
		/// <param name="managerCreator">
		/// The function to construct the manager.
		/// </param>
		/// <param name="segregatedEntity">
		/// The entity belonging to a segregation. It allows access cheking 
		/// based on the current user's dispositions against the segregation, beyond user roles.
		/// </param>
		protected M TryGetManager<M>(Func<M> managerCreator, ISegregatedEntity segregatedEntity)
			where M : class
		{
			if (managerCreator == null) throw new ArgumentNullException(nameof(managerCreator));
			if (segregatedEntity == null) throw new ArgumentNullException(nameof(segregatedEntity));

			if (!CanAccessManager(typeof(M), segregatedEntity)) return null;

			return managerCreator.Invoke();
		}

		/// <summary>
		/// Get a manager of type <typeparamref name="M"/> asynchronously
		/// for an entity under a segregation
		/// or null if the user is not authorized.
		/// </summary>
		/// <typeparam name="M">The type of the manager.</typeparam>
		/// <param name="asyncManagerCreator">
		/// The asynchronous function to construct the manager.
		/// </param>
		/// <param name="segregatedEntity">
		/// The entity belonging to a segregation. It allows access cheking 
		/// based on the current user's dispositions against the segregation, beyond user roles.
		/// </param>
		protected async Task<M> TryGetManagerAsync<M>(
			Func<Task<M>> asyncManagerCreator, 
			ISegregatedEntity segregatedEntity)
			where M : class
		{
			if (asyncManagerCreator == null) throw new ArgumentNullException(nameof(asyncManagerCreator));
			if (segregatedEntity == null) throw new ArgumentNullException(nameof(segregatedEntity));

			if (!CanAccessManager(typeof(M), segregatedEntity)) return null;

			return await asyncManagerCreator.Invoke();
		}

		/// <summary>
		/// Get a manager of type <typeparamref name="M"/>
		/// for an entity under a segregation
		/// or null if the user is not authorized.
		/// </summary>
		/// <typeparam name="M">The type of the manager.</typeparam>
		/// <param name="managerCreator">
		/// The function to construct the manager.
		/// </param>
		/// <param name="segregationID">
		/// The ID of the segregation. It allows access cheking 
		/// based on the current user's dispositions against the segregation, beyond user roles.
		/// </param>
		protected M TryGetManager<M>(Func<M> managerCreator, long segregationID)
			where M : class
		{
			if (managerCreator == null) throw new ArgumentNullException(nameof(managerCreator));

			if (!CanAccessManager(typeof(M), segregationID)) return null;

			return managerCreator.Invoke();
		}

		/// <summary>
		/// Get a manager of type <typeparamref name="M"/> asynchornously
		/// for an entity under a segregation
		/// or null if the user is not authorized.
		/// </summary>
		/// <typeparam name="M">The type of the manager.</typeparam>
		/// <param name="asyncManagerCreator">
		/// The asynchronous function to construct the manager.
		/// </param>
		/// <param name="segregationID">
		/// The ID of the segregation. It allows access cheking 
		/// based on the current user's dispositions against the segregation, beyond user roles.
		/// </param>
		protected async Task<M> TryGetManagerAsync<M>(Func<Task<M>> asyncManagerCreator, long segregationID)
			where M : class
		{
			if (asyncManagerCreator == null) throw new ArgumentNullException(nameof(asyncManagerCreator));

			if (!CanAccessManager(typeof(M), segregationID)) return null;

			return await asyncManagerCreator.Invoke();
		}

		/// <summary>
		/// Get a manager of type <typeparamref name="M"/>
		/// for an entity under a segregation.
		/// </summary>
		/// <typeparam name="M">The type of the manager.</typeparam>
		/// <param name="managerCreator">
		/// The function to construct the manager.
		/// </param>
		/// <exception cref="ManagerAccessDeniedException">
		/// Thrown when the session user is not authorized.
		/// </exception>
		protected M GetManager<M>(Func<M> managerCreator)
			where M : class
		{
			M manager = TryGetManager(managerCreator);

			if (manager == null)
			{
				Type managerType = typeof(M);

				string message = GetManagerAccessDeniedMessage(managerType);

				this.ClassLogger.Log(LogLevel.Warn, message);

				throw new ManagerAccessDeniedException(managerType, message);
			}

			return manager;
		}

		/// <summary>
		/// Get a manager of type <typeparamref name="M"/> asynchornously
		/// for an entity under a segregation.
		/// </summary>
		/// <typeparam name="M">The type of the manager.</typeparam>
		/// <param name="asyncManagerCreator">
		/// The asynchronous function to construct the manager.
		/// </param>
		/// <exception cref="ManagerAccessDeniedException">
		/// Thrown when the session user is not authorized.
		/// </exception>
		protected async Task<M> GetManagerAsync<M>(Func<Task<M>> asyncManagerCreator)
			where M : class
		{
			M manager = await TryGetManagerAsync(asyncManagerCreator);

			if (manager == null)
			{
				Type managerType = typeof(M);

				string message = GetManagerAccessDeniedMessage(managerType);

				this.ClassLogger.Log(LogLevel.Warn, message);

				throw new ManagerAccessDeniedException(managerType, message);
			}

			return manager;
		}

		/// <summary>
		/// Get a manager of type <typeparamref name="M"/>
		/// for an entity under a segregation.
		/// </summary>
		/// <typeparam name="M">The type of the manager.</typeparam>
		/// <param name="managerCreator">
		/// The function to construct the manager.
		/// </param>
		/// <param name="segregatedEntity">
		/// The entity belonging to a segregation. It allows access cheking 
		/// based on the current user's dispositions against the segregation, beyond user roles.
		/// </param>
		/// <exception cref="ManagerAccessDeniedException">
		/// Thrown when the session user is not authorized.
		/// </exception>
		protected M GetManager<M>(Func<M> managerCreator, ISegregatedEntity segregatedEntity)
			where M : class
		{
			M manager = TryGetManager(managerCreator, segregatedEntity);

			if (manager == null)
			{
				Type managerType = typeof(M);

				string message = GetManagerAccessDeniedMessage(managerType, segregatedEntity);

				this.ClassLogger.Log(LogLevel.Warn, message);

				throw new ManagerAccessDeniedException(managerType, message);
			}

			return manager;
		}

		/// <summary>
		/// Get a manager of type <typeparamref name="M"/> asynchronously
		/// for an entity under a segregation.
		/// </summary>
		/// <typeparam name="M">The type of the manager.</typeparam>
		/// <param name="asyncManagerCreator">
		/// The asynchronous function to construct the manager.
		/// </param>
		/// <param name="segregatedEntity">
		/// The entity belonging to a segregation. It allows access cheking 
		/// based on the current user's dispositions against the segregation, beyond user roles.
		/// </param>
		/// <exception cref="ManagerAccessDeniedException">
		/// Thrown when the session user is not authorized.
		/// </exception>
		protected async Task<M> GetManagerAsync<M>(
			Func<Task<M>> asyncManagerCreator, 
			ISegregatedEntity segregatedEntity)
			where M : class
		{
			M manager = await TryGetManagerAsync(asyncManagerCreator, segregatedEntity);

			if (manager == null)
			{
				Type managerType = typeof(M);

				string message = GetManagerAccessDeniedMessage(managerType, segregatedEntity);

				this.ClassLogger.Log(LogLevel.Warn, message);

				throw new ManagerAccessDeniedException(managerType, message);
			}

			return manager;
		}

		/// <summary>
		/// Get a manager of type <typeparamref name="M"/>
		/// for an entity under a segregation
		/// or null if the user is not authorized.
		/// </summary>
		/// <typeparam name="M">The type of the manager.</typeparam>
		/// <param name="managerCreator">
		/// The function to construct the manager.
		/// </param>
		/// <param name="segregationID">
		/// The ID of the segregation. It allows access cheking 
		/// based on the current user's dispositions against the segregation, beyond user roles.
		/// </param>
		protected M GetManager<M>(Func<M> managerCreator, long segregationID)
			where M : class
		{
			M manager = TryGetManager(managerCreator, segregationID);

			if (manager == null)
			{
				Type managerType = typeof(M);

				string message = GetManagerAccessDeniedMessage(managerType, segregationID);

				this.ClassLogger.Log(LogLevel.Warn, message);

				throw new ManagerAccessDeniedException(managerType, message);
			}

			return manager;
		}

		/// <summary>
		/// Get a manager of type <typeparamref name="M"/> asynchronously
		/// for an entity under a segregation
		/// or null if the user is not authorized.
		/// </summary>
		/// <typeparam name="M">The type of the manager.</typeparam>
		/// <param name="asyncManagerCreator">
		/// The asynchronous function to construct the manager.
		/// </param>
		/// <param name="segregationID">
		/// The ID of the segregation. It allows access cheking 
		/// based on the current user's dispositions against the segregation, beyond user roles.
		/// </param>
		protected async Task<M> GetManagerAsync<M>(Func<Task<M>> asyncManagerCreator, long segregationID)
			where M : class
		{
			M manager = await TryGetManagerAsync(asyncManagerCreator, segregationID);

			if (manager == null)
			{
				Type managerType = typeof(M);

				string message = GetManagerAccessDeniedMessage(managerType, segregationID);

				this.ClassLogger.Log(LogLevel.Warn, message);

				throw new ManagerAccessDeniedException(managerType, message);
			}

			return manager;
		}

		/// <summary>
		/// Create a new domain container which has entity access security enabled.
		/// It is the caller's responsibility to dispose the container.
		/// </summary>
		protected D CreateSecuredDomainContainer()
		{
			D domainContainer = this.CreateDomainContainer();

			var entityListener = new EntityListener(this);

			domainContainer.EntityListeners.Add(entityListener);

			return domainContainer;
		}

		/// <summary>
		/// Flush the cached static resources of sessions created using a
		/// specific configuration section name.
		/// Forces new sessions of the given configuration section name to have
		/// regenerated static resources.
		/// </summary>
		/// <param name="configurationSectionName">
		/// The configuration section name upon which the sessions are set up.
		/// </param>
		/// <returns>
		/// Returns true if the resources were existing and flushed.
		/// Else, the resources were not found either because no session
		/// has been created under the configuration section name since the
		/// start of the application or the previous call of this method,
		/// either the resources were dropped out of the cache as
		/// least-recently-used items to make room for other resources.
		/// </returns>
		/// <remarks>
		/// A <see cref="LogicSessionEnvironment{U, D}"/>
		/// is created per <paramref name="configurationSectionName"/>
		/// on an as-needed basis.
		/// It contains the <see cref="Settings"/>, the <see cref="AccessResolver"/>
		/// and mappings of MIME content types for the sessions created using
		/// the given configuration section name.
		/// </remarks>
		protected static bool FlushSessionsEnvironment(string configurationSectionName)
		{
			if (configurationSectionName == null) throw new ArgumentNullException(nameof(configurationSectionName));


			if (sessionEnvironmentsCache.Remove(
				configurationSectionName, 
				out LogicSessionEnvironment<U, D> sessionEnvironment))
			{
				sessionEnvironment.Settings.Dispose();

				return true;
			}
			else
			{
				return false;
			}
		}

		#endregion

		#region Private methods

		/// <summary>
		/// Establish the current user and install
		/// entity access control on behalf of her.
		/// </summary>
		/// <param name="userQuery">A query defining the user.</param>
		private void Login(IQueryable<U> userQuery)
		{
			if (userQuery == null) throw new ArgumentNullException(nameof(userQuery));

			userQuery = IncludeWithUser(userQuery)
				.Include(u => u.Roles)
				.Include(u => u.Dispositions.Select(d => d.Type));

			user = userQuery.FirstOrDefault();

			if (user == null)
				throw new LogicException("The specified user doesn't exist in the database.");

			entityListener = new EntityListener(this);

			this.DomainContainer.EntityListeners.Add(entityListener);
		}

		/// <summary>
		/// Creates a domain container ready for use.
		/// It is the caller's responsibility to dispose the object.
		/// </summary>
		private D CreateDomainContainer()
		{
			return this.Settings.Resolve<D>();
		}

		/// <summary>
		/// Set up <see cref="Settings"/>, <see cref="DomainContainer"/>
		/// and <see cref="AccessResolver"/> based on the configuration.
		/// </summary>
		/// <param name="configurationSectionName">The name of a Unity configuration section.</param>
		private void Initialize(string configurationSectionName)
		{
			this.ConfigurationSectionName = configurationSectionName;

			this.Environment = this.ResolveEnvironment();

			this.DomainContainer = this.CreateDomainContainer();
		}

		/// <summary>
		/// Elevates access to all entities for the rest of the session.
		/// </summary>
		private void ElevateAccessRights()
		{
			entityListener.SupressAccessCheck = true;
		}

		/// <summary>
		/// Restores any previous access rights elevation.
		/// </summary>
		private void RestoreAccessRights()
		{
			entityListener.SupressAccessCheck = false;
		}

		/// <summary>
		/// Elevate access rights, taking into account the nesting.
		/// </summary>
		private void IncrementAccessElevationLevel()
		{
			if (accessElevationNestingLevel++ == 0)
			{
				ElevateAccessRights();
			}
		}

		/// <summary>
		/// Restore access rights, taking into account the nesting.
		/// </summary>
		private void DecrementAccessElevationLevel()
		{
			if (--accessElevationNestingLevel == 0)
			{
				RestoreAccessRights();
			}
		}

		/// <summary>
		/// Get the message for reporting when access to a manager is denied.
		/// </summary>
		/// <param name="managerType">The type of the manager.</param>
		/// <returns>Returns the message.</returns>
		private string GetManagerAccessDeniedMessage(Type managerType)
		{
			if (this.User != null)
			{
				return $"The user with ID {this.user.ID} cannot access manager '{managerType.FullName}'.";
			}
			else
			{
				return $"An anonymous user cannot access manager '{managerType.FullName}'.";
			}
		}

		/// <summary>
		/// Get the message for reporting when access to a manager is denied.
		/// </summary>
		/// <param name="managerType">The type of the manager.</param>
		/// <param name="segregatedEntity">The segregated entity against which a manager is requested.</param>
		/// <returns>Returns the message.</returns>
		private string GetManagerAccessDeniedMessage(Type managerType, ISegregatedEntity segregatedEntity)
		{
			if (this.User != null)
			{
				return $"The user with ID {this.user.ID} cannot access manager '{managerType.FullName}'" + 
					$" for an entity of type '{AccessRight.GetEntityTypeName(segregatedEntity)}' under segregation with ID {segregatedEntity.SegregationID}.";
			}
			else
			{
				return $"An anonymous user cannot access manager '{managerType.FullName}'" +
					$" for an entity of type '{AccessRight.GetEntityTypeName(segregatedEntity)}' under segregation with ID {segregatedEntity.SegregationID}.";
			}
		}

		/// <summary>
		/// Get the message for reporting when access to a manager is denied.
		/// </summary>
		/// <param name="managerType">The type of the manager.</param>
		/// <param name="segregationID">The ID of the segregation against which the manager is requested.</param>
		/// <returns>Returns the message.</returns>
		private string GetManagerAccessDeniedMessage(Type managerType, long segregationID)
		{
			if (this.User != null)
			{
				return $"The user with ID {this.user.ID} cannot access manager '{managerType.FullName}' for segregation with ID {segregationID}.";
			}
			else
			{
				return $"An anonymous user cannot access manager '{managerType.FullName}' for segregation with ID {segregationID}.";
			}
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
				this.ClassLogger.Log(
					LogLevel.Error,
					$"Failed to send notification with model {channelMessage.Model.GetType().FullName} via channel of type {channel.GetType().FullName}, subject: '{channelMessage.Subject}'",
					e);
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
				this.ClassLogger.Log(
					LogLevel.Error,
					$"Failed to send via channel of type {channel.GetType().FullName}, subject: '{channelMessage.Subject}'",
					e);
			}
		}

		#endregion
	}

	/// <summary>
	/// Abstract base for business logic session.
	/// </summary>
	/// <typeparam name="U">
	/// The type of the user, derived from <see cref="Domain.User"/>.
	/// </typeparam>
	/// <typeparam name="D">
	/// The type of domain container, derived from <see cref="IUsersDomainContainer{U}"/>.
	/// </typeparam>
	/// <typeparam name="C">
	/// The type of configurator for the <see cref="LogicSession{U, D}.Settings"/> property,
	/// derived from <see cref="Configurator"/>.
	/// </typeparam>
	/// <remarks>
	/// <para>
	/// Each session depends on a Unity DI container defined in a configuration section.
	/// This container must provide resolutions for the following:
	/// <list>
	/// <item><typeparamref name="D"/> (required)</item>
	/// <item>
	/// <see cref="IUserContext"/> (required only when using the constructor which
	/// implies the current user)
	/// </item>
	/// <item><see cref="IPermissionsSetupProvider"/> (required)</item>
	/// <item>
	/// <see cref="IRenderProvider"/>
	/// (required when RenderTemplate methods are used, singleton lifetime is strongly recommended)
	/// </item>
	/// </list>
	/// </para>
	/// <para>
	/// If the system is working with <see cref="Domain.Files.IFile"/> entities,
	/// the session's configuration section must provide resulutions for the following as well:
	/// <list>
	/// <item>
	/// <see cref="Configuration.FilesConfiguration"/> (singleton lifetime is strongly recommended)</item>
	/// <item>
	/// Named and/or unnamed registrations of <see cref="Storage.IStorageProvider"/> implementations
	/// (singleton lifetime is strongly recommended),
	/// whose name (null or not) matches the <see cref="Domain.Files.IFile.ProviderName"/> property.
	/// </item>
	/// </list>
	/// </para>
	/// </remarks>
	public abstract class LogicSession<U, D, C> : LogicSession<U, D>
		where U : User
		where D : IUsersDomainContainer<U>
		where C : Configurator, new()
	{
		#region Private fields

		/// <summary>
		/// Caches <see cref="LogicSessionEnvironment{U, D, C}"/>s by configuration section names.
		/// </summary>
		private static MRUCache<string, LogicSessionEnvironment<U, D, C>> sessionEnvironmentsCache;

		#endregion

		#region Construction

		/// <summary>
		/// Static initialization.
		/// </summary>
		static LogicSession()
		{
			sessionEnvironmentsCache = new MRUCache<string, LogicSessionEnvironment<U, D, C>>(
				configurationSectionName => new LogicSessionEnvironment<U, D, C>(configurationSectionName),
				SessionEnvironmentsCacheSize);
		}

		/// <summary>
		/// Create a session impersonating the user specified
		/// in the registered <see cref="IUserContext"/>
		/// inside the configuration
		/// section specified by <paramref name="configurationSectionName"/>.
		/// </summary>
		/// <param name="configurationSectionName">The element name of a Unity configuration section.</param>
		/// <exception cref="LogicException">
		/// Thrown when the resolved <see cref="IUserContext"/> fails to specify an existing user.
		/// </exception>
		/// <remarks>
		/// Each session depends on a Unity DI container defined in a configuration section.
		/// This container must at least provide resolutions for the following:
		/// <list>
		/// <item><typeparamref name="D"/></item>
		/// <item><see cref="IUserContext"/></item>
		/// <item><see cref="IPermissionsSetupProvider"/></item>
		/// </list>
		/// </remarks>
		public LogicSession(string configurationSectionName)
			: base(configurationSectionName)
		{
		}

		/// <summary>
		/// Create a session impersonating the user specified
		/// using a predicate.
		/// </summary>
		/// <param name="configurationSectionName">The element name of a Unity configuration section.</param>
		/// <param name="userPickPredicate">A predicate to filter a single user.</param>
		/// <exception cref="LogicException">
		/// Thrown when the <paramref name="userPickPredicate"/> fails to specify an existing user.
		/// </exception>
		/// <remarks>
		/// Each session depends on a Unity DI container defined in a configuration section.
		/// This container must at least provide resolutions for the following:
		/// <list>
		/// <item><typeparamref name="D"/></item>
		/// <item><see cref="IPermissionsSetupProvider"/></item>
		/// </list>
		/// </remarks>
		public LogicSession(string configurationSectionName, Expression<Func<U, bool>> userPickPredicate)
			: base(configurationSectionName, userPickPredicate)
		{
		}

		#endregion

		#region Public methods

		/// <summary>
		/// Get the session environment which corresponds to a configuration section.
		/// </summary>
		/// <param name="configurationSectionName">The name of the configuration section.</param>
		/// <returns>Returns the environment, if successful.</returns>
		public static new LogicSessionEnvironment<U, D, C> GetEnvironment(string configurationSectionName)
		{
			if (configurationSectionName == null) throw new ArgumentNullException(nameof(configurationSectionName));

			return sessionEnvironmentsCache.Get(configurationSectionName);
		}

		#endregion

		#region Internal methods

		/// <summary>
		/// Get the session environment corresponding to a configuration section name.
		/// </summary>
		internal override LogicSessionEnvironment<U, D> ResolveEnvironment()
		{
			// Implementation seems the same as the original method being overriden, 
			// but this Session<U, D, C>.GetEnvironment static method is not the same as
			// the parent's Session<U, D>.GetEnvironment static method.
			return GetEnvironment(this.ConfigurationSectionName);
		}

		#endregion

		#region Protected methods

		/// <summary>
		/// Flush the cached static resources of sessions created using a
		/// specific configuration section name.
		/// Forces new sessions of the given configuration section name to have
		/// regenerated static resources.
		/// </summary>
		/// <param name="configurationSectionName">
		/// The configuration section name upon which the sessions are set up.
		/// </param>
		/// <returns>
		/// Returns true if the resources were existing and flushed.
		/// Else, the resources were not found either because no session
		/// has been created under the configuration section name since the
		/// start of the application or the previous call of this method,
		/// either the resources were dropped out of the cache as
		/// least-recently-used items to make room for other resources.
		/// </returns>
		/// <remarks>
		/// A <see cref="LogicSessionEnvironment{U, D, C}"/>
		/// is created per <paramref name="configurationSectionName"/>
		/// on an as-needed basis.
		/// It contains the <see cref="LogicSession{U, D}.Settings"/>, the <see cref="AccessResolver{U}"/>
		/// and mappings of MIME content types for the sessions created using
		/// the given configuration section name.
		/// </remarks>
		protected static new bool FlushSessionsEnvironment(string configurationSectionName)
		{
			if (configurationSectionName == null) throw new ArgumentNullException(nameof(configurationSectionName));

			return sessionEnvironmentsCache.Remove(configurationSectionName);
		}

		#endregion
	}

	/// <summary>
	/// Abstract base for business logic session, which also offers
	/// a <see cref="PublicDomain"/> data access property.
	/// </summary>
	/// <typeparam name="U">
	/// The type of the user, derived from <see cref="Domain.User"/>.
	/// </typeparam>
	/// <typeparam name="D">
	/// The type of domain container, derived from <see cref="IUsersDomainContainer{U}"/>.
	/// </typeparam>
	/// <typeparam name="C">
	/// The type of configurator for the <see cref="LogicSession{U, D}.Settings"/> property,
	/// derived from <see cref="Configurator"/>.
	/// </typeparam>
	/// <typeparam name="PD">
	/// The type of public domain access instance,
	/// derived from <see cref="PublicDomain{D}"/>.
	/// </typeparam>
	/// <remarks>
	/// <para>
	/// Each session depends on a Unity DI container defined in a configuration section.
	/// This container must provide resolutions for the following:
	/// <list>
	/// <item><typeparamref name="D"/> (required)</item>
	/// <item>
	/// <see cref="IUserContext"/> (required only when using the constructor which
	/// implies the current user)
	/// </item>
	/// <item><see cref="IPermissionsSetupProvider"/> (required)</item>
	/// <item>
	/// <see cref="IRenderProvider"/>
	/// (required when RenderTemplate methods are used, singleton lifetime is strongly recommended)
	/// </item>
	/// </list>
	/// </para>
	/// <para>
	/// If the system is working with <see cref="Domain.Files.IFile"/> entities,
	/// the session's configuration section must provide resulutions for the following as well:
	/// <list>
	/// <item>
	/// <see cref="Configuration.FilesConfiguration"/> (singleton lifetime is strongly recommended)</item>
	/// <item>
	/// Named and/or unnamed registrations of <see cref="Storage.IStorageProvider"/> implementations
	/// (singleton lifetime is strongly recommended),
	/// whose name (null or not) matches the <see cref="Domain.Files.IFile.ProviderName"/> property.
	/// </item>
	/// </list>
	/// </para>
	/// </remarks>
	public abstract class LogicSession<U, D, C, PD> : LogicSession<U, D>, IPublicDomainProvider<D, PD>
		where U : User
		where D : IUsersDomainContainer<U>
		where C : Configurator, new()
		where PD : PublicDomain<D>
	{
		#region Private fields

		/// <summary>
		/// Backing field for <see cref="PublicDomain"/>.
		/// </summary>
		private PD publicDomain;

		#endregion

		#region Construction

		/// <summary>
		/// Create a session impersonating the user specified
		/// in the registered <see cref="IUserContext"/>
		/// inside the configuration
		/// section specified by <paramref name="configurationSectionName"/>.
		/// </summary>
		/// <param name="configurationSectionName">The element name of a Unity configuration section.</param>
		/// <exception cref="LogicException">
		/// Thrown when the resolved <see cref="IUserContext"/> fails to specify an existing user.
		/// </exception>
		/// <remarks>
		/// Each session depends on a Unity DI container defined in a configuration section.
		/// This container must at least provide resolutions for the following:
		/// <list>
		/// <item><typeparamref name="D"/></item>
		/// <item><see cref="IUserContext"/></item>
		/// <item><see cref="IPermissionsSetupProvider"/></item>
		/// </list>
		/// </remarks>
		public LogicSession(string configurationSectionName) 
			: base(configurationSectionName)
		{
		}

		/// <summary>
		/// Create a session impersonating the user specified
		/// using a predicate.
		/// </summary>
		/// <param name="configurationSectionName">The element name of a Unity configuration section.</param>
		/// <param name="userPickPredicate">A predicate to filter a single user.</param>
		/// <exception cref="LogicException">
		/// Thrown when the <paramref name="userPickPredicate"/> fails to specify an existing user.
		/// </exception>
		/// <remarks>
		/// Each session depends on a Unity DI container defined in a configuration section.
		/// This container must at least provide resolutions for the following:
		/// <list>
		/// <item><typeparamref name="D"/></item>
		/// <item><see cref="IPermissionsSetupProvider"/></item>
		/// </list>
		/// </remarks>
		public LogicSession(string configurationSectionName, Expression<Func<U, bool>> userPickPredicate)
			: base(configurationSectionName, userPickPredicate)
		{
		}

		#endregion

		#region Public properties

		/// <summary>
		/// Get the public domain associated with the session.
		/// </summary>
		/// <remarks>
		/// The wrapped domain container has always entity access security enabled.
		/// </remarks>
		public PD PublicDomain
		{
			get
			{
				if (publicDomain == null)
				{
					publicDomain = CreatePublicDomain(this.DomainContainer, false);
				}

				return publicDomain;
			}
		}

		#endregion

		#region Public methods

		/// <summary>
		/// Create a new public domain. The caller is responsible for disposing it.
		/// </summary>
		/// <remarks>
		/// The wrapped domain container has always entity access security enabled.
		/// </remarks>
		public PD CreatePublicDomain()
		{
			return CreatePublicDomain(CreateSecuredDomainContainer(), true);
		}

		#endregion

		#region Protected methods

		/// <summary>
		/// Create a new instance of a public domain. Specified by derivations.
		/// The method supports the <see cref="PublicDomain"/> property and
		/// the <see cref="CreatePublicDomain()"/> method.
		/// </summary>
		/// <param name="domainContainer">
		/// The domain container to wrap.
		/// </param>
		/// <param name="ownsDomainContainer">
		/// If true, the public domain owns the <paramref name="domainContainer"/>.
		/// </param>
		/// <returns>Returns the created public domain.</returns>
		protected abstract PD CreatePublicDomain(D domainContainer, bool ownsDomainContainer);

		#endregion
	}
}
