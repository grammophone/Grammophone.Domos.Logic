using System;

namespace Grammophone.Domos.Logic.Models.Workflow
{
	/// <summary>
	/// Interface for path execution result
	/// on a set of stateful objects.
	/// </summary>
	/// <typeparam name="SO">The type of the stateful object.</typeparam>
	/// <typeparam name="ST">The type of state transition.</typeparam>
	public interface IExecutionResult<out SO, out ST>
		where ST : class
	{
		/// <summary>
		/// If there was an error in the path execution, this
		/// holds the exception, else null.
		/// </summary>
		Exception Exception { get; }

		/// <summary>
		/// If there was an error in the path execution, this
		/// holds the exception, else null.
		/// </summary>
		SO StatefulObject { get; }

		/// <summary>
		/// If the path was executed successfully, this holds the
		/// path transition, else null.
		/// </summary>
		ST StateTransition { get; }
	}
}
