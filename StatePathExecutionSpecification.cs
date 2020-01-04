using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Grammophone.Domos.Domain.Workflow;

namespace Grammophone.Domos.Logic
{
	/// <summary>
	/// Implementations of manager methods
	/// return an instance of this value type to specify which state path to execute on a stateful object upon line digestion or null
	/// to take default action.
	/// </summary>
	public struct StatePathExecutionSpecification : IEquatable<StatePathExecutionSpecification>
	{
		#region Construction

		/// <summary>
		/// Create.
		/// </summary>
		/// <param name="statePathCodeName">The <see cref="StatePath.CodeName"/> of the <see cref="StatePath"/> to execute.</param>
		/// <param name="workflowGraphCodeName">The <see cref="WorkflowGraph.CodeName"/> of the <see cref="WorkflowGraph"/> where the path belongs.</param>
		public StatePathExecutionSpecification(string statePathCodeName, string workflowGraphCodeName)
		{
			if (statePathCodeName == null) throw new ArgumentNullException(nameof(statePathCodeName));
			if (workflowGraphCodeName == null) throw new ArgumentNullException(nameof(workflowGraphCodeName));

			this.StatePathCodeName = statePathCodeName;
			this.WorkflowGraphCodeName = workflowGraphCodeName;
		}

		#endregion

		#region Public properties

		/// <summary>
		/// The <see cref="StatePath.CodeName"/> of the <see cref="StatePath"/> to execute.
		/// </summary>
		public string StatePathCodeName { get; }

		/// <summary>
		/// The <see cref="WorkflowGraph.CodeName"/> of the <see cref="WorkflowGraph"/> where the path belongs.
		/// </summary>
		public string WorkflowGraphCodeName { get; }

		#endregion

		#region IEquatable and related methods implementation

		/// <summary>
		/// Returns true when the <paramref name="other"/> object has equal <see cref="StatePathCodeName"/>
		/// and <see cref="WorkflowGraphCodeName"/> properties as this one.
		/// </summary>
		/// <param name="other">The other object.</param>
		public bool Equals(StatePathExecutionSpecification other)
		{
			return this.StatePathCodeName == other.StatePathCodeName && this.WorkflowGraphCodeName == other.WorkflowGraphCodeName;
		}

		/// <summary>
		/// Returns true when the <paramref name="other"/> object
		/// is <see cref="StatePathExecutionSpecification"/> and
		/// has equal <see cref="StatePathCodeName"/>
		/// and <see cref="WorkflowGraphCodeName"/> properties as this one.
		/// </summary>
		/// <param name="other">The other object.</param>
		public override bool Equals(object other)
		{
			if (!(other is StatePathExecutionSpecification)) return false;

			return Equals((StatePathExecutionSpecification)other);
		}

		/// <summary>
		/// Take into account <see cref="StatePathCodeName"/> and <see cref="WorkflowGraphCodeName"/>
		/// prroperties to produce a hash code.
		/// </summary>
		public override int GetHashCode() => (this.StatePathCodeName, this.WorkflowGraphCodeName).GetHashCode();

		#endregion
	}
}
