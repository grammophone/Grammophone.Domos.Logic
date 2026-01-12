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
		/// <summary>
		/// The name of the property.
		/// </summary>
		public string Name { get; set; }

		/// <summary>
		/// The original value of the property.
		/// </summary>
		public object OriginalValue { get; set; }

		/// <summary>
		/// The current value of the property.
		/// </summary>
		public object CurrentValue { get; set; }
	}
}
