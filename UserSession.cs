using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Grammophone.DataAccess;
using Grammophone.Domos.AccessChecking;
using Grammophone.Domos.DataAccess;
using Grammophone.Domos.Domain;

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
			/// The <see cref="AccessResolver"/> to use in order to enforce entity rights.
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

		#endregion

		#region Protected properties

		/// <summary>
		/// References the domain model. Do not dispose, because the owner
		/// is the session object.
		/// </summary>
		protected internal D DomainContainer { get; private set; }

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
	}
}
