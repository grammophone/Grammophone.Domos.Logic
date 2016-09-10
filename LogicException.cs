using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Grammophone.Domos.Logic
{
	/// <summary>
	/// Exception from the logic layer.
	/// </summary>
	[Serializable]
	public class LogicException : ApplicationException
	{
		/// <summary>
		/// Create.
		/// </summary>
		/// <param name="message">The exception message.</param>
		public LogicException(string message) : base(message) { }

		/// <summary>
		/// Create.
		/// </summary>
		/// <param name="message">The exception message.</param>
		/// <param name="inner">The inner cause of the exception.</param>
		public LogicException(string message, Exception inner) : base(message, inner) { }

		/// <summary>
		/// Used in serializatoin.
		/// </summary>
		protected LogicException(
			System.Runtime.Serialization.SerializationInfo info,
			System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
	}
}
