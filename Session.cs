using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.Entity;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using Grammophone.Caching;
using Grammophone.Configuration;
using Grammophone.DataAccess;
using Grammophone.Domos.AccessChecking;
using Grammophone.Domos.DataAccess;
using Grammophone.Domos.Domain;
using Grammophone.Domos.Environment;
using Grammophone.TemplateRendering;
using Microsoft.Practices.Unity;
using Microsoft.Practices.Unity.Configuration;

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
	public abstract class Session<U, D> : Loggable, IDisposable
		where U : User
		where D : IUsersDomainContainer<U>
	{
		#region Constants

		/// <summary>
		/// The size of <see cref="sessionEnvironmentsCache"/>.
		/// </summary>
		private const int SessionEnvironmentsCacheSize = 128;

		#endregion

		#region Private classes

		/// <summary>
		/// Enforces security while accessing entities.
		/// </summary>
		private class EntityListener : Loggable, IUserTrackingEntityListener
		{
			#region Private fields

			private readonly AccessResolver accessResolver;

			private readonly U user;

			#endregion

			#region Construction

			/// <summary>
			/// Create.
			/// </summary>
			/// <param name="user">
			/// The user, which must have prefetched the roles, disposition and disposition types
			/// for proper performance.
			/// </param>
			/// <param name="accessResolver">
			/// The <see cref="AccessChecking.AccessResolver"/> to use in order to enforce entity rights.
			/// </param>
			public EntityListener(U user, AccessResolver accessResolver)
			{
				if (user == null) throw new ArgumentNullException(nameof(user));
				if (accessResolver == null) throw new ArgumentNullException(nameof(accessResolver));

				this.user = user;
				this.accessResolver = accessResolver;
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
				var trackedEntity = entity as ITrackingEntity;

				if (trackedEntity != null)
				{
					trackedEntity.CreatorUserID = user.ID;
					trackedEntity.LastModifierUserID = user.ID;

					var now = DateTime.UtcNow;
					trackedEntity.CreationDate = now;
					trackedEntity.LastModificationDate = now;

					var userTrackingEntity = trackedEntity as IUserTrackingEntity;

					if (userTrackingEntity.OwningUserID == 0L)
					{
						userTrackingEntity.OwningUserID = user.ID;
					}
				}

				if (!SupressAccessCheck && !accessResolver.CanUserCreateEntity(user, entity))
				{
					LogActionAndThrowAccessDenied(entity, "create");
				}
			}

			public void OnChanging(object entity)
			{
				if (!SupressAccessCheck && !accessResolver.CanUserWriteEntity(user, entity))
				{
					LogActionAndThrowAccessDenied(entity, "write");
				}

				var trackedEntity = entity as ITrackingEntity;

				if (trackedEntity != null)
				{
					trackedEntity.LastModifierUserID = user.ID;

					var now = DateTime.UtcNow;
					trackedEntity.LastModificationDate = now;

					var userTrackingEntity = trackedEntity as IUserTrackingEntity;
				}
			}

			public void OnDeleting(object entity)
			{
				if (!SupressAccessCheck && !accessResolver.CanUserDeleteEntity(user, entity))
				{
					LogActionAndThrowAccessDenied(entity, "delete");
				}
			}

			public void OnRead(object entity)
			{
				if (!SupressAccessCheck && !accessResolver.CanUserReadEntity(user, entity))
				{
					LogActionAndThrowAccessDenied(entity, "read");
				}
			}

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
				var entityWithID = entity as IEntityWithID<object>;

				string message;

				if (entityWithID != null)
				{
					message =
						$"The user with ID {user.ID} cannot {action} an entity of type {AccessRight.GetEntityTypeName(entity)} with ID {entityWithID.ID}.";
				}
				else
				{
					message =
						$"The user with ID {user.ID} cannot {action} an entity of type {AccessRight.GetEntityTypeName(entity)}.";
				}

				LogAndThrowAccessDenied(entity, message);
			}

			/// <summary>
			/// Log the denial of access and throw a <see cref="AccessDeniedDomainException"/>.
			/// </summary>
			/// <param name="entity">The entity to which the access is denied.</param>
			/// <param name="message">The message to log and throw.</param>
			/// <exception cref="AccessDeniedDomainException">Thrown always.</exception>
			private void LogAndThrowAccessDenied(object entity, string message)
			{
				this.Logger.Error(message);

				throw new AccessDeniedDomainException(message, entity);
			}

			#endregion
		}

		#endregion

		#region Private fields

		/// <summary>
		/// Caches <see cref="SessionEnvironment{U, D}"/>s by configuration section names.
		/// </summary>
		private static MRUCache<string, SessionEnvironment<U, D>> sessionEnvironmentsCache;

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

		#endregion

		#region Construction

		/// <summary>
		/// Static initialization.
		/// </summary>
		static Session()
		{
			sessionEnvironmentsCache = new MRUCache<string, SessionEnvironment<U, D>>(
				configurationSectionName => new SessionEnvironment<U, D>(configurationSectionName), 
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
		public Session(string configurationSectionName)
		{
			if (configurationSectionName == null) throw new ArgumentNullException(nameof(configurationSectionName));

			Initialize(configurationSectionName);

			var userContext = this.DIContainer.Resolve<IUserContext>();

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
		public Session(string configurationSectionName, Expression<Func<U, bool>> userPickPredicate)
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

		#endregion

		#region Protected properties

		/// <summary>
		/// References the domain model. Do not dispose, because the owner
		/// is the session object.
		/// </summary>
		protected internal D DomainContainer { get; private set; }

		/// <summary>
		/// The Unity dependency injection container for this session.
		/// </summary>
		protected internal IUnityContainer DIContainer => this.Environment.DIContainer;

		/// <summary>
		/// Provides low and high-level access checking for entities and managers.
		/// </summary>
		protected internal AccessResolver AccessResolver => this.Environment.AccessResolver;

		#endregion

		#region Internal properties

		/// <summary>
		/// The environment of the session.
		/// </summary>
		internal SessionEnvironment<U, D> Environment { get; private set; }

		#endregion

		#region Public methods

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
		{
			if (recepients == null) throw new ArgumentNullException(nameof(recepients));
			if (subject == null) throw new ArgumentNullException(nameof(subject));
			if (body == null) throw new ArgumentNullException(nameof(body));

			var emailSettings = this.DIContainer.Resolve<EmailSettings>();

			var mailMessage = new System.Net.Mail.MailMessage
			{
				Sender = new System.Net.Mail.MailAddress(sender ?? emailSettings.DefaultSenderAddress),
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

			await SendEmailAsync(mailMessage);
		}

		/// <summary>
		/// Send an e-mail.
		/// </summary>
		/// <param name="mailMessage">
		/// The message to send. If its Sender property is not set, 
		/// the configured <see cref="EmailSettings.DefaultSenderAddress"/> is used.
		/// </param>
		/// <returns>Returns a task completing the action.</returns>
		public async Task SendEmailAsync(System.Net.Mail.MailMessage mailMessage)
		{
			if (mailMessage == null) throw new ArgumentNullException(nameof(mailMessage));

			var emailSettings = this.DIContainer.Resolve<EmailSettings>();

			if (mailMessage.Sender == null)
			{
				mailMessage.Sender = new System.Net.Mail.MailAddress(emailSettings.DefaultSenderAddress);
			}

			using (var emailClient = new System.Net.Mail.SmtpClient(emailSettings.SmtpServerName, emailSettings.SmtpServerPort))
			{
				emailClient.EnableSsl = emailSettings.UseSSL;

				await emailClient.SendMailAsync(mailMessage);
			}
		}

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
			var renderProvider = this.DIContainer.Resolve<IRenderProvider>();

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
			var renderProvider = this.DIContainer.Resolve<IRenderProvider>();

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

		/// <summary>
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
		internal ElevatedAccessScope GetElevatedAccessScope()
		{
			IncrementAccessElevationLevel();

			return new ElevatedAccessScope(DecrementAccessElevationLevel);
		}

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
		internal void ElevateTransactionAccessRights(ITransaction transaction)
		{
			if (transaction == null) throw new ArgumentNullException(nameof(transaction));

			transaction.Succeeding += DecrementAccessElevationLevel;
			transaction.RollingBack += DecrementAccessElevationLevel;

			IncrementAccessElevationLevel();
		}

		#endregion

		#region Protected methods

		/// <summary>
		/// Override to specify any additional eager fetches along the current user.
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
		/// depending on her roles only.
		/// </summary>
		/// <param name="managerType">The type of the manager.</param>
		/// <remarks>
		/// If the type of the manager is generic, it uses the full
		/// name of its generic type definition.
		/// </remarks>
		protected bool CanAccessManager(Type managerType)
		{
			if (managerType == null) throw new ArgumentNullException(nameof(managerType));

			if (managerType.IsConstructedGenericType) managerType = managerType.GetGenericTypeDefinition();

			string managerName = managerType.FullName;

			return this.AccessResolver.CanUserAccessManager(user, managerName);
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
		/// </remarks>
		protected bool CanAccessManager(Type managerType, long dispositionID)
		{
			if (managerType == null) throw new ArgumentNullException(nameof(managerType));

			if (managerType.IsConstructedGenericType) managerType = managerType.GetGenericTypeDefinition();

			string managerName = managerType.FullName;

			return this.AccessResolver.CanUserAccessManager(user, dispositionID, managerName);
		}

		/// <summary>
		/// Create a new domain container which has entity access security enabled.
		/// It is the caller's responsibility to dispose the container.
		/// </summary>
		protected D CreateSecuredDomainContainer()
		{
			D domainContainer = this.CreateDomainContainer();

			InstallEntityAccessListener(domainContainer);

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
		/// A session environment is created per <paramref name="configurationSectionName"/>
		/// on an as-needed basis.
		/// It contains the <see cref="DIContainer"/>, the <see cref="AccessResolver"/>
		/// and mappings of MIME content types for the sessions created using
		/// the given configuration section name.
		/// </remarks>
		protected static bool FlushSessionsEnvironment(string configurationSectionName)
		{
			if (configurationSectionName == null) throw new ArgumentNullException(nameof(configurationSectionName));

			SessionEnvironment<U, D> sessionEnvironment;

			if (sessionEnvironmentsCache.Remove(configurationSectionName, out sessionEnvironment))
			{
				sessionEnvironment.DIContainer.Dispose();

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
		/// entity access control on ehalf of her.
		/// </summary>
		/// <param name="userQuery">A query defining the user.</param>
		private void Login(IQueryable<U> userQuery)
		{
			if (userQuery == null) throw new ArgumentNullException(nameof(userQuery));

			userQuery = IncludeWithUser(userQuery)
				.Include(u => u.Roles)
				.Include("Dispositions.Type");

			user = userQuery.FirstOrDefault();

			if (user == null)
				throw new LogicException("The specified user doesn't exist in the database.");

			InstallEntityAccessListener(this.DomainContainer);
		}

		/// <summary>
		/// Installs entity access check control to a domain container.
		/// </summary>
		private void InstallEntityAccessListener(D domainContainer)
		{
			if (domainContainer == null) throw new ArgumentNullException(nameof(domainContainer));

			entityListener = new EntityListener(user, this.AccessResolver);

			domainContainer.EntityListeners.Add(entityListener);
		}

		/// <summary>
		/// Creates a domain container ready for use.
		/// It is the caller's responsibility to dispose the object.
		/// </summary>
		private D CreateDomainContainer()
		{
			return this.Environment.DIContainer.Resolve<D>();
		}

		/// <summary>
		/// Set up <see cref="DIContainer"/>, <see cref="DomainContainer"/>
		/// and <see cref="AccessResolver"/> based on the configuration.
		/// </summary>
		/// <param name="configurationSectionName">The name of a Unity configuration section.</param>
		private void Initialize(string configurationSectionName)
		{
			this.ConfigurationSectionName = configurationSectionName;

			this.Environment = sessionEnvironmentsCache.Get(configurationSectionName);

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

		#endregion
	}

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
	public abstract class Session<U, D, PD> : Session<U, D>
		where U : User
		where D : IUsersDomainContainer<U>
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
		public Session(string configurationSectionName) 
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
		public Session(string configurationSectionName, Expression<Func<U, bool>> userPickPredicate)
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
