using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Grammophone.Domos.DataAccess;
using Grammophone.Domos.Domain;
using Grammophone.Domos.Domain.Workflow;

namespace Grammophone.Domos.Logic
{
	/// <summary>
	/// Interface to be implemented by actions invoked during
	/// state transitions.
	/// </summary>
	/// <typeparam name="U">The type of the user, derived from <see cref="User"/>.</typeparam>
	/// <typeparam name="D">The type of domain container, derived from <see cref="IWorkflowUsersDomainContainer{U, ST}"/>.</typeparam>
	/// <typeparam name="S">The type of session, derived from <see cref="Session{U, D}"/>.</typeparam>
	/// <typeparam name="ST">The type of state transition, derived from <see cref="StateTransition{U}"/>.</typeparam>
	public interface IWorkflowAction<U, D, S, ST>
		where U : User
		where D : IUsersDomainContainer<U>
		where S : Session<U, D>
		where ST : StateTransition<U>
	{
		/// <summary>
		/// Execute the action.
		/// </summary>
		/// <param name="session">The session.</param>
		/// <param name="stateful">The stateful instance to execute upon.</param>
		/// <param name="stateTransition">The state transition being executed.</param>
		/// <param name="actionArguments">The arguments to the action.</param>
		/// <returns>Returns a task completing the operation.</returns>
		Task ExecuteAsync(
			S session, 
			IStateful<U, ST> stateful, 
			ST stateTransition, 
			IDictionary<string, object> actionArguments);

		/// <summary>
		/// Get the specifications of parameters expected in the
		/// parameters dictionary used 
		/// by <see cref="ExecuteAsync(S, IStateful{U, ST}, ST, IDictionary{string, object})"/>
		/// method.
		/// </summary>
		/// <returns>Returns a collection of parameter specifications.</returns>
		IEnumerable<ParameterSpecification> GetParameterSpecifications();
	}
}
