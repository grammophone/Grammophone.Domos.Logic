using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Grammophone.Domos.Logic.Models.Workflow
{
	/// <summary>
	/// Path execution result
	/// on a set of stateful objects.
	/// </summary>
	/// <typeparam name="SO">The type of the stateful object.</typeparam>
	/// <typeparam name="ST">The type of state transition.</typeparam>
	[Serializable]
	public class ExecutionResult<SO, ST> : IExecutionResult<SO, ST>
		where ST : class
	{
		/// <inheritdoc/>
		public SO StatefulObject { get; internal set; }

		/// <inheritdoc/>
		public ST StateTransition { get; internal set; }

		/// <inheritdoc/>
		public Exception Exception { get; internal set; }
	}
}
