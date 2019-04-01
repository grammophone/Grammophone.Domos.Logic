using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Grammophone.Domos.Logic
{
	/// <summary>
	/// Source or destination of notifications sent via <see cref="IChannel{T}"/>.
	/// </summary>
	public interface IChannelIdentity
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
