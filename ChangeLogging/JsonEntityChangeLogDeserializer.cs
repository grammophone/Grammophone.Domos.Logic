using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Grammophone.DataAccess;

namespace Grammophone.Domos.Logic.ChangeLogging
{
	/// <summary>
	/// Deserializer that resolves JSON complex objects into the respective complex type properties.
	/// </summary>
	/// <typeparam name="D">The type of the domain container to use for entity creation.</typeparam>
	public class JsonEntityChangeLogDeserializer<D> : EntityChangeLogDeserializer<D>
		where D : IDomainContainer
	{
		/// <inheritdoc/>
		public JsonEntityChangeLogDeserializer(D domainContainer) : base(domainContainer)
		{
		}

		/// <summary>
		/// If the <paramref name="value"/> is of type <see cref="JsonElement"/>, attempt to deserialize it in the type specified by <paramref name="propertyInfo"/>
		/// and set it in the <paramref name="entity"/>.
		/// </summary>
		protected override void ResolvePropertyAssignment<E>(E entity, PropertyInfo propertyInfo, object value)
		{
			if (value is JsonElement jsonElement)
			{
				var deserializedValue = jsonElement.Deserialize(propertyInfo.PropertyType);

				if (deserializedValue != null)
				{
					propertyInfo.SetValue(entity, deserializedValue);
				}
			}
		}
	}
}
