using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Grammophone.Domos.Logic
{
	/// <summary>
	/// Exception whose message is aimed to be displayed to the user.
	/// </summary>
	[Serializable]
	public class UserException : LogicException
	{
		/// <summary>
		/// Create.
		/// </summary>
		/// <param name="message">The exception message.</param>
		public UserException(string message) : base(message) { }

		/// <summary>
		/// Create.
		/// </summary>
		/// <param name="message">The exception message.</param>
		/// <param name="inner">The inner cause of the exception.</param>
		public UserException(string message, Exception inner) : base(message, inner) { }

		/// <summary>
		/// Used in serializatoin.
		/// </summary>
		protected UserException(
			System.Runtime.Serialization.SerializationInfo info,
			System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
	}
}
