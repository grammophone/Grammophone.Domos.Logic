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
	/// The type of the user, derived from <see cref="User"/>.
	/// </typeparam>
	/// <typeparam name="D">
	/// The type of domain container, derived from <see cref="IUsersDomainContainer{U}"/>.
	/// </typeparam>
	public class UserSession<U, D> : IDisposable
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
		private class EntityListener : IEntityListener
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
			/// log and throw a <see cref="DomainAccessDeniedException"/>.
			/// </summary>
			/// <param name="entity">The entity to which the access is denied.</param>
			/// <param name="message">The verb form of the action being denied, like "read", "create" etc.</param>
			/// <exception cref="DomainAccessDeniedException">Thrown always.</exception>
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
			/// Log the denial of access and throw a <see cref="DomainAccessDeniedException"/>.
			/// </summary>
			/// <param name="entity">The entity to which the access is denied.</param>
			/// <param name="message">The message to log and throw.</param>
			/// <exception cref="DomainAccessDeniedException">Thrown always.</exception>
			private void LogAndThrowAccessDenied(object entity, string message)
			{
				throw new DomainAccessDeniedException(message, entity);
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
		/// The name of the Unity configuration section for this session
		/// where <see cref="diContainer"/> is defined.
		/// </summary>
		private string configurationSectionName;

		/// <summary>
		/// The user owning the session.
		/// </summary>
		private U currentUser;

		/// <summary>
		/// The entity listener enforcing access checking.
		/// </summary>
		private EntityListener entityListener;

		#endregion

		#region Construction

		/// <summary>
		/// Static initialization.
		/// </summary>
		static UserSession()
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
		public UserSession(string configurationSectionName)
		{
			if (configurationSectionName == null) throw new ArgumentNullException(nameof(configurationSectionName));

			this.configurationSectionName = configurationSectionName;

			var sessionEnvironment = sessionEnvironmentsCache.Get(configurationSectionName);

			this.DIContainer = sessionEnvironment.DIContainer;
			this.AccessResolver = sessionEnvironment.AccessResolver;

			this.DomainContainer = this.DIContainer.Resolve<D>();

			var userContext = this.DIContainer.Resolve<IUserContext>();

			long? userID = userContext.UserID;

			var userQuery = this.DomainContainer.Users
				.Include(u => u.Roles)
				.Include("Dispositions.Type");

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
		public UserSession(string configurationSectionName, Expression<Func<U, bool>> userPickPredicate)
		{
			if (configurationSectionName == null) throw new ArgumentNullException(nameof(configurationSectionName));
			if (userPickPredicate == null) throw new ArgumentNullException(nameof(userPickPredicate));

			this.configurationSectionName = configurationSectionName;

			var sessionEnvironment = sessionEnvironmentsCache.Get(configurationSectionName);

			this.DIContainer = sessionEnvironment.DIContainer;
			this.AccessResolver = sessionEnvironment.AccessResolver;

			this.DomainContainer = this.DIContainer.Resolve<D>();

			var userQuery = this.DomainContainer.Users
				.Include(u => u.Roles)
				.Include("Dispositions.Type");

			Login(userQuery);
		}

		#endregion

		#region Public properties

		/// <summary>
		/// The user owning the session or null of anonymous.
		/// </summary>
		public U CurrentUser
		{
			get
			{
				if (currentUser.IsAnonymous)
					return null;
				else
					return currentUser;
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

		#endregion

		#region Private methods

		private void Login(IQueryable<U> userQuery)
		{
			if (userQuery == null) throw new ArgumentNullException(nameof(userQuery));

			userQuery = IncludeWithUser(userQuery);

			currentUser = userQuery.FirstOrDefault();

			if (currentUser == null)
				throw new LogicException("The specified user doesn't exist in the database.");

			InstallEntityAccessListener();
		}

		/// <summary>
		/// Create a Unity DI container from a configuration section.
		/// </summary>
		/// <param name="configurationSectionName">The element name of the configuratio section.</param>
		/// <returns>Returns the container.</returns>
		private static IUnityContainer CreateDIContainer(string configurationSectionName)
		{
			if (configurationSectionName == null) throw new ArgumentNullException(nameof(configurationSectionName));

			var configurationSection = ConfigurationManager.GetSection(configurationSectionName)
				as UnityConfigurationSection;

			if (configurationSection == null)
				throw new LogicException($"The '{configurationSectionName}' configuration section is not defined.");

			return new UnityContainer().LoadConfiguration(configurationSection);
		}

		/// <summary>
		/// Installs entity access check control.
		/// </summary>
		private void InstallEntityAccessListener()
		{
			entityListener = new EntityListener(currentUser, this.AccessResolver);

			this.DomainContainer.EntityListeners.Add(entityListener);
		}

		#endregion
	}
}
