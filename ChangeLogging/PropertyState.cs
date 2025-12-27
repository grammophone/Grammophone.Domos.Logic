using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Grammophone.DataAccess;

namespace Grammophone.Domos.Logic.ChangeLogging
{
	/// <summary>
	/// The state of a property of an entity, conaining the original value and the current value.
	/// </summary>
	public class PropertyState
	{
		#region Construction

		internal PropertyState(string name, object originalValue, object currentValue, bool isPrimitive, bool isComplexType)
		{
			this.Name = name;
			this.OriginalValue = originalValue;
			this.CurrentValue = currentValue;
			this.IsPrimitive = isPrimitive;
			this.IsComplexType = isComplexType;
		}

		#endregion

		#region Public properties

		/// <summary>
		/// The name of the property.
		/// </summary>
		public string Name { get; }

		/// <summary>
		/// The original value of the property.
		/// </summary>
		public object OriginalValue { get; }

		/// <summary>
		/// The current value of the property.
		/// </summary>
		public object CurrentValue { get; }

		/// <summary>
		/// If true, the type of the property is a primitive type.
		/// </summary>
		public bool IsPrimitive { get; }

		/// <summary>
		/// If true, the type of the property is a complex type.
		/// </summary>
		public bool IsComplexType { get; }

		#endregion
	}
}
