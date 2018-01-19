using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Grammophone.Domos.Logic.Models.FundsTransfer
{
	/// <summary>
	/// Exception when there is a schema validation error while loading XML
	/// representations of <see cref="FundsRequestFile"/> or <see cref="FundsResponseFile"/>.
	/// </summary>
	[Serializable]
	public class FundsFileSchemaException : LogicException
	{
		/// <summary>
		/// Create.
		/// </summary>
		/// <param name="message">The exception message.</param>
		public FundsFileSchemaException(string message) : base(message) { }

		/// <summary>
		/// Create.
		/// </summary>
		/// <param name="message">The exception message.</param>
		/// <param name="inner">The inner exception cause.</param>
		public FundsFileSchemaException(string message, Exception inner) : base(message, inner) { }

		/// <summary>
		/// Used for serialization.
		/// </summary>
		protected FundsFileSchemaException(
			System.Runtime.Serialization.SerializationInfo info,
			System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
	}
}
