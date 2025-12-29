using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Grammophone.DataAccess;

namespace Grammophone.Domos.Logic.ChangeLogging
{
	/// <summary>
	/// Deserializes an array of <see cref="PropertyState"/> into an entity.
	/// </summary>
	public class EntityChangeLogDeserializer<D>
		where D : IDomainContainer
	{
		#region Private fields

		private readonly D domainContainer;

		#endregion

		#region Construction

		/// <summary>
		/// Create.
		/// </summary>
		/// <param name="domainContainer">The domain container to use to create the deserialized entities.</param>
		/// <exception cref="ArgumentNullException"></exception>
		public EntityChangeLogDeserializer(D domainContainer)
		{
			if (domainContainer == null) throw new ArgumentNullException(nameof(domainContainer));

			this.domainContainer = domainContainer;
		}

		#endregion

		#region Public methods

		/// <summary>
		/// Deserialize the property states into an entity of type <typeparamref name="E"/>.
		/// </summary>
		/// <typeparam name="E">The type of the entity to deserialize.</typeparam>
		/// <param name="propertyStates">The property states recorded.</param>
		/// <param name="entityStateType">Option to deserialize from original values or current values.</param>
		/// <returns>Returns the deserialized entity.</returns>
		public E Deserialize<E>(IReadOnlyCollection<PropertyState> propertyStates, EntityStateType entityStateType)
			where E : class
		{
			if (propertyStates == null) throw new ArgumentNullException(nameof(propertyStates));

			IDictionary<string, object> propertiesByName;

			switch (entityStateType)
			{
				case EntityStateType.Original:
					propertiesByName = propertyStates.ToDictionary(propertyState => propertyState.Name, propertyState => propertyState.OriginalValue);
					break;
				
				case EntityStateType.Current:
					propertiesByName = propertyStates.ToDictionary(propertyState => propertyState.Name, propertyState => propertyState.CurrentValue);
					break;
				
				default:
					throw new LogicException($"Unknown entity state type {entityStateType}.");
			}

			E entity = domainContainer.Create<E>();

			PopulateEntity(entity, propertiesByName);

			return entity;
		}

		#endregion

		#region Protected methods

		/// <summary>
		/// Overridable method that is called to resolve the case when
		/// the type of the property of a entity is incompatible to the deserialized value for the property.
		/// </summary>
		/// <remarks>
		/// The default implementation does nothing.
		/// </remarks>
		/// <typeparam name="E">The type of the entity.</typeparam>
		/// <param name="entity">The entity/</param>
		/// <param name="propertyInfo">The reflection information for the entity's property.</param>
		/// <param name="value">The value attempted to be serialized. It is guaranteed to be not a null reference (could be a nullable value).</param>
		protected virtual void ResolvePropertyAssignment<E>(E entity, PropertyInfo propertyInfo, object value)
			where E : class
		{
			// Default implementation does nothing.
		}

		#endregion

		#region Private methods

		private void PopulateEntity<E>(E entity, IDictionary<string, object> propertiesByName)
			where E : class
		{
			var entityType = typeof(E);

			foreach (var entry in propertiesByName)
			{
				if (entry.Value == null) continue;

				var propertyInfo = entityType.GetProperty(entry.Key);

				if (propertyInfo == null) continue;

				var propertyType = propertyInfo.PropertyType;

				var valueType = entry.Value.GetType();

				if (propertyType.IsAssignableFrom(valueType))
				{
					propertyInfo.SetValue(entity, entry.Value);
				}
				else
				{
					ResolvePropertyAssignment(entity, propertyInfo, entry.Value);
				}
			}
		}

		#endregion
	}
}
