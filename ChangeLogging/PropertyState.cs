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
	[Serializable]
	public class PropertyState
	{
		#region Construction

		internal PropertyState(string name, object originalValue, object currentValue, bool isModified)
		{
			this.Name = name;
			this.OriginalValue = originalValue;
			this.CurrentValue = currentValue;
			this.IsModified = isModified;
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
		/// True if there has been a modification in the property.
		/// </summary>
		public bool IsModified { get; }

		#endregion
	}
}
