using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Grammophone.Domos.Logic.ChangeLogging
{
	/// <summary>
	/// Type of change recorded via the <see cref="IEntityChangeLogger{U, D}"/> interface.
	/// </summary>
	public enum EntityChangeType
	{
		/// <summary>
		/// Entity is being added.
		/// </summary>
		Addition,

		/// <summary>
		/// Entity is being modified.
		/// </summary>
		Modification,

		/// <summary>
		/// Entity is being deleted.
		/// </summary>
		Deletetion
	}
}
