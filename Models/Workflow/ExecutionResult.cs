using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Grammophone.Domos.Logic.Models.Workflow
{
	/// <summary>
	/// Path execution result
	/// on a stateful object inside a batch.
	/// </summary>
	/// <typeparam name="SO">The type of the stateful object.</typeparam>
	/// <typeparam name="ST">The type of state transition.</typeparam>
	[Serializable]
	public class ExecutionResult<SO, ST>
		where ST : class
	{
		/// <summary>
		/// The stateful object on which the path is executed.
		/// </summary>
		public SO StatefulObject { get; internal set; }

		/// <summary>
		/// If the path was executed successfully, this holds the
		/// path transition, else null.
		/// </summary>
		public ST StateTransition { get; internal set; }

		/// <summary>
		/// If there was an error in the path execution, this
		/// holds the exception, else null.
		/// </summary>
		public Exception Exception { get; internal set; }
	}
}
