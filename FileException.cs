using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Grammophone.Domos.Domain.Files;

namespace Grammophone.Domos.Logic
{
	/// <summary>
	/// Exception thrown when handling <see cref="File"/> descendants.
	/// </summary>
	[Serializable]
	public class FileException : UserException
	{
		/// <summary>
		/// Create.
		/// </summary>
		/// <param name="message">The exception message.</param>
		public FileException(string message) : base(message) { }

		/// <summary>
		/// Create.
		/// </summary>
		/// <param name="message">The exception message.</param>
		/// <param name="inner">The inner cause of the exception.</param>
		public FileException(string message, Exception inner) : base(message, inner) { }

		/// <summary>
		/// Used in serialization.
		/// </summary>
		protected FileException(
		System.Runtime.Serialization.SerializationInfo info,
		System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
	}
}
