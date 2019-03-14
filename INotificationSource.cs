using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Grammophone.Domos.Logic
{
	/// <summary>
	/// Source of notifications sent via <see cref="INotificationChannel{T}"/>.
	/// </summary>
	public interface INotificationSource
	{
		/// <summary>
		/// The name of the sender.
		/// </summary>
		string Name { get; }

		/// <summary>
		/// The e-mail of the sender.
		/// </summary>
		string Email { get; }
 	}
}
