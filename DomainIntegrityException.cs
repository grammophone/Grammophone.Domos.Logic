using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Grammophone.Domos.Logic
{
	/// <summary>
	/// Exception thrown when deleting an entity upon which
	/// other entities rely. The message is intended to be
	/// displayed to the user.
	/// </summary>
	[Serializable]
	public class DomainIntegrityException : UserException
	{
		/// <summary>
		/// Create with a default message.
		/// </summary>
		public DomainIntegrityException()
			: this(CommonMessages.ENTITY_IN_USE)
		{ }

		/// <summary>
		/// Create.
		/// </summary>
		/// <param name="message">The exception message.</param>
		public DomainIntegrityException(string message) : base(message) { }

		/// <summary>
		/// Create.
		/// </summary>
		/// <param name="message">The exception message.</param>
		/// <param name="inner">The inner exceptino cause.</param>
		public DomainIntegrityException(string message, Exception inner) : base(message, inner) { }

		/// <summary>
		/// Used for serialization.
		/// </summary>
		protected DomainIntegrityException(
		System.Runtime.Serialization.SerializationInfo info,
		System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
	}
}
