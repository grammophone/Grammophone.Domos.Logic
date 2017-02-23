using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Grammophone.Domos.Domain;

namespace Grammophone.Domos.Logic
{
	/// <summary>
	/// Exception base for signaling access denial.
	/// </summary>
	[Serializable]
	public class AccessDeniedException : LogicException, IAccessDeniedException
	{
		/// <summary>
		/// Create with default message.
		/// </summary>
		public AccessDeniedException()
			: base("Access is denied.")
		{ }

		/// <summary>
		/// Create.
		/// </summary>
		/// <param name="message"></param>
		public AccessDeniedException(string message) : base(message) { }

		/// <summary>
		/// Create.
		/// </summary>
		/// <param name="message">The exception message.</param>
		/// <param name="inner">The inner exception.</param>
		public AccessDeniedException(string message, Exception inner) : base(message, inner) { }

		/// <summary>
		/// Used for serialization.
		/// </summary>
		protected AccessDeniedException(
		System.Runtime.Serialization.SerializationInfo info,
		System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
	}
}
