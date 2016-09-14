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
using Microsoft.Practices.Unity;
using Microsoft.Practices.Unity.Configuration;

namespace Grammophone.Domos.Logic
{
	/// <summary>
	/// Abstract base for business logic sessions.
	/// </summary>
	/// <typeparam name="U">
	/// The type of the user, derived from <see cref="Domain.User"/>.
	/// </typeparam>
	/// <typeparam name="D">
	/// The type of domain container, derived from <see cref="IUsersDomainContainer{U}"/>.
	/// </typeparam>
	/// <remarks>
	/// Each session depends on a Unity DI container defined in a configuration section.
	/// This container must at least provide resolutions for the following:
	/// <list>
	/// <item><typeparamref name="D"/></item>
	/// <item>
	/// <see cref="IUserContext"/> (required only when using the constructor which
	/// implies the current user)
	/// </item>
	/// <item><see cref="IPermissionsSetupProvider"/></item>
	/// </list>
	/// </remarks>
	public class Session<U, D> : Loggable, IDisposable
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
		private class EntityListener : Loggable, IEntityListener
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
						$"The user {user.Email} cannot {action} an entity of type {AccessRight.GetEntityTypeName(entity)} with ID {entityWithID.ID}.";
				}
				else
				{
					message =
						$"The user {user.Email} cannot {action} an entity of type {AccessRight.GetEntityTypeName(entity)}.";
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

		/// <summary>
		/// Binds a session to its configuration environment.
		/// </summary>
		private class SessionEnvironment
		{
			/// <summary>
			/// Create.
			/// </summary>
			/// <param name="configurationSectionName">The name of the Unity configuration section.</param>
			public SessionEnvironment(string configurationSectionName)
			{
				if (configurationSectionName == null) throw new ArgumentNullException(nameof(configurationSectionName));

				this.DIContainer = CreateDIContainer(configurationSectionName);

				var permissionsSetupProvider = this.DIContainer.Resolve<IPermissionsSetupProvider>();

				this.AccessResolver = new AccessResolver(permissionsSetupProvider);
			}

			/// <summary>
			/// The Unity DI container.
			/// </summary>
			public IUnityContainer DIContainer;

			/// <summary>
			/// The access resolver using the <see cref="IPermissionsSetupProvider"/>
			/// specified in <see cref="DIContainer"/>.
			/// </summary>
			public AccessResolver AccessResolver;
		}

		#endregion

		#region Private fields

		/// <summary>
		/// Caches <see cref="SessionEnvironment"/>s by configuration section names.
		/// </summary>
		private static MRUCache<string, SessionEnvironment> sessionEnvironmentsCache;

		/// <summary>
		/// The name of the Unity configuration section for this session.
		/// </summary>
		private string configurationSectionName;

		/// <summary>
		/// The user owning the session.
		/// </summary>
		private U user;

		/// <summary>
		/// The entity listener enforcing access checking.
		/// </summary>
		private EntityListener entityListener;

		#endregion

		#region Construction

		/// <summary>
		/// Static initialization.
		/// </summary>
		static Session()
		{
			sessionEnvironmentsCache = new MRUCache<string, SessionEnvironment>(
				configurationSectionName => new SessionEnvironment(configurationSectionName), 
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
		/// <item><see cref="IUserContext"/></item>
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
		protected internal IUnityContainer DIContainer { get; private set; }

		/// <summary>
		/// Provides low and high-level access checking for entities and managers.
		/// </summary>
		protected internal AccessResolver AccessResolver { get; private set; }

		#endregion

		#region Public methods

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
		/// Elevate access to all entities for the duration of a <paramref name="transaction"/>.
		/// </summary>
		/// <param name="transaction">The transaction.</param>
		internal void ElevateTransactionAccessRights(ITransaction transaction)
		{
			if (transaction == null) throw new ArgumentNullException(nameof(transaction));

			transaction.Succeeding += RestoreAccessRights;
			transaction.RollingBack += RestoreAccessRights;

			ElevateAccessRights();
		}

		/// <summary>
		/// Create a Unity DI container from a configuration section.
		/// </summary>
		/// <param name="configurationSectionName">The element name of the configuratio section.</param>
		/// <returns>Returns the container.</returns>
		internal static IUnityContainer CreateDIContainer(string configurationSectionName)
		{
			if (configurationSectionName == null) throw new ArgumentNullException(nameof(configurationSectionName));

			var configurationSection = ConfigurationManager.GetSection(configurationSectionName)
				as UnityConfigurationSection;

			if (configurationSection == null)
				throw new LogicException($"The '{configurationSectionName}' configuration section is not defined.");

			return new UnityContainer().LoadConfiguration(configurationSection);
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

			InstallEntityAccessListener();
		}

		/// <summary>
		/// Installs entity access check control.
		/// </summary>
		private void InstallEntityAccessListener()
		{
			entityListener = new EntityListener(user, this.AccessResolver);

			this.DomainContainer.EntityListeners.Add(entityListener);
		}

		/// <summary>
		/// Set up <see cref="DIContainer"/>, <see cref="DomainContainer"/>
		/// and <see cref="AccessResolver"/> based on the configuration.
		/// </summary>
		/// <param name="configurationSectionName">The name of a Unity configuration section.</param>
		private void Initialize(string configurationSectionName)
		{
			this.configurationSectionName = configurationSectionName;

			var sessionEnvironment = sessionEnvironmentsCache.Get(configurationSectionName);

			this.DIContainer = sessionEnvironment.DIContainer;
			this.AccessResolver = sessionEnvironment.AccessResolver;

			this.DomainContainer = this.DIContainer.Resolve<D>();
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

		#endregion
	}
}
