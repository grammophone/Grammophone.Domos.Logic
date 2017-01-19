using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Grammophone.Domos.Logic
{
	/// <summary>
	/// Thrown when the arguments passed to ExecuteStatePathAsync methods 
	/// of <see cref="WorkflowManager{U, BST, D, S, ST, SO}"/>
	/// are not valid.
	/// </summary>
	[Serializable]
	public class WorkflowActionValidationException : UserException
	{
		/// <summary>
		/// Create.
		/// </summary>
		/// <param name="validationErrors">
		/// A dictionary of validation error messages grouped by
		/// parameter key.
		/// </param>
		internal WorkflowActionValidationException(IDictionary<string, ICollection<string>> validationErrors)
			: base(WorkflowManagerMessages.INVALID_ACTION_PARAMETERS)
		{
			if (validationErrors == null) throw new ArgumentNullException(nameof(validationErrors));

			this.ValidationErrors = validationErrors;
		}

		/// <summary>
		/// Used for serialization.
		/// </summary>
		protected WorkflowActionValidationException(
		System.Runtime.Serialization.SerializationInfo info,
		System.Runtime.Serialization.StreamingContext context) : base(info, context) { }

		/// <summary>
		/// A dictionary of validation error messages grouped by
		/// parameter key.
		/// </summary>
		public IDictionary<string, ICollection<string>> ValidationErrors { get; private set; }
	}
}
