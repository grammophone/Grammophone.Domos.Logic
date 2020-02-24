using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Grammophone.Domos.Domain;
using Grammophone.Domos.Domain.Workflow;
using Grammophone.Domos.Logic.Models.Workflow;

namespace Grammophone.Domos.Logic
{
	/// <summary>
	/// Contract for basic functions offered
	/// by <see cref="WorkflowManager{U, BST, D, S, ST, SO, C}"/> descendants.
	/// </summary>
	/// <typeparam name="U">The type of the user, derived from <see cref="User"/>.</typeparam>
	/// <typeparam name="ST">
	/// The type of state transition, derived from <see cref="StateTransition{U}"/>.
	/// </typeparam>
	/// <typeparam name="SO">
	/// The type of stateful being managed, derived from <see cref="IStateful{U, ST}"/>.
	/// </typeparam>
	public interface IWorkflowManager<U, ST, SO>
		where U : User
		where ST : StateTransition<U>
		where SO : IStateful<U, ST>
	{
		#region Public properties

		/// <summary>
		/// The states in the system.
		/// </summary>
		IQueryable<State> States { get; }

		/// <summary>
		/// The state groups in the system.
		/// </summary>
		IQueryable<StateGroup> StateGroups { get; }

		/// <summary>
		/// The workflow graphs in the system.
		/// </summary>
		IQueryable<WorkflowGraph> WorkflowGraphs { get; }

		/// <summary>
		/// The state paths in the system.
		/// </summary>
		IQueryable<StatePath> StatePaths { get; }

		/// <summary>
		/// The state transitions
		/// of type <typeparamref name="ST"/> in the system.
		/// </summary>
		IQueryable<ST> StateTransitions { get; }

		#endregion

		#region Methods

		/// <summary>
		/// Get an object managed by this manager.
		/// </summary>
		/// <param name="objectID">The ID of the stateful object.</param>
		/// <returns>Returns the stateful object.</returns>
		/// <exception cref="InvalidOperationException">
		/// Thrown when the <paramref name="objectID"/> doesn't correspond to an object managed by this manager.
		/// </exception>
		Task<SO> GetStatefulObjectAsync(long objectID);

		/// <summary>
		/// Execute a state path against a stateful instance.
		/// </summary>
		/// <param name="stateful">The stateful instance to execute the transition upon.</param>
		/// <param name="pathCodeName">The <see cref="StatePath.CodeName"/> of the state path.</param>
		/// <param name="actionArguments">A dictinary of arguments to be passed to the path actions.</param>
		/// <returns>Returns the state transition created.</returns>
		/// <exception cref="AccessDeniedDomainException">
		/// Thrown when the session user has no right to execute the state path.
		/// </exception>
		/// <exception cref="StatePathArgumentsException">
		/// Thrown when the <paramref name="actionArguments"/> are not valid
		/// against the parameter specifications of the path actions.
		/// </exception>
		/// <exception cref="LogicException">
		/// Thrown when the specified path 
		/// is not available for the current state of the stateful object,
		/// or when the path doesn't exist,
		/// or when the path's workflow is not compatible with transitions 
		/// of type <typeparamref name="ST"/>.
		/// </exception>
		Task<ST> ExecuteStatePathAsync(
			SO stateful,
			string pathCodeName,
			IDictionary<string, object> actionArguments);

		/// <summary>
		/// Execute a state path against a stateful instance.
		/// </summary>
		/// <param name="stateful">The stateful instance to execute the transition upon.</param>
		/// <param name="pathID">The ID of the state path.</param>
		/// <param name="actionArguments">A dictinary of arguments to be passed to the path actions.</param>
		/// <returns>Returns the state transition created.</returns>
		/// <exception cref="AccessDeniedDomainException">
		/// Thrown when the session user has no right to execute the state path.
		/// </exception>
		/// <exception cref="StatePathArgumentsException">
		/// Thrown when the <paramref name="actionArguments"/> are not valid
		/// against the parameter specifications of the path actions.
		/// </exception>
		/// <exception cref="LogicException">
		/// Thrown when the specified path 
		/// is not available for the current state of the stateful object,
		/// or when the path doesn't exist,
		/// or when the path's workflow is not compatible with transitions 
		/// of type <typeparamref name="ST"/>.
		/// </exception>
		Task<ST> ExecuteStatePathAsync(
			SO stateful,
			long pathID,
			IDictionary<string, object> actionArguments);

		/// <summary>
		/// Execute a state path against a stateful instance.
		/// </summary>
		/// <param name="stateful">The stateful instance to execute the transition upon.</param>
		/// <param name="statePath">The state path.</param>
		/// <param name="actionArguments">A dictinary of arguments to be passed to the path actions.</param>
		/// <returns>Returns the state transition created.</returns>
		/// <exception cref="AccessDeniedDomainException">
		/// Thrown when the session user has no right to execute the state path.
		/// </exception>
		/// <exception cref="StatePathArgumentsException">
		/// Thrown when the <paramref name="actionArguments"/> are not valid
		/// against the parameter specifications of the path actions.
		/// </exception>
		/// <exception cref="LogicException">
		/// Thrown when the specified path 
		/// is not available for the current state of the stateful object
		/// or when the <see cref="WorkflowGraph"/> where the path belongs 
		/// works with a different <see cref="WorkflowGraph.StateTransitionTypeName"/>
		/// than <typeparamref name="ST"/>.
		/// </exception>
		/// <remarks>
		/// For best performance, the <see cref="StatePath.PreviousState"/>,
		/// <see cref="StatePath.NextState"/> and <see cref="StatePath.WorkflowGraph"/>
		/// properties of the <paramref name="statePath"/> must be eagerly loaded.
		/// </remarks>
		Task<ST> ExecuteStatePathAsync(
			SO stateful,
			StatePath statePath,
			IDictionary<string, object> actionArguments);

		/// <summary>
		/// Get the specifications of parameters required by all pre-actions
		/// and post-actions of a state path.
		/// </summary>
		/// <param name="pathCodeName">The code name of the state path.</param>
		/// <returns>
		/// Returns a dictionary of the parameter specifications having as key 
		/// the <see cref="ParameterSpecification.Key"/> property.
		/// If actions specify parametrs with overlapping keys, the last one 
		/// in the actions list overwrites the previous, first from pre-actions to
		/// post-actions.
		/// </returns>
		IReadOnlyDictionary<string, ParameterSpecification> GetPathParameterSpecifications(
			string pathCodeName);

		/// <summary>
		/// Get the specifications of parameters required by all pre-actions
		/// and post-actions of a state path.
		/// </summary>
		/// <param name="statePathID">The ID of the state path.</param>
		/// <returns>
		/// Returns a dictionary of the parameter specifications having as key 
		/// the <see cref="ParameterSpecification.Key"/> property.
		/// If actions specify parametrs with overlapping keys, the last one 
		/// in the actions list overwrites the previous, first from pre-actions to
		/// post-actions.
		/// </returns>
		Task<IReadOnlyDictionary<string, ParameterSpecification>> GetPathParameterSpecificationsAsync(
			long statePathID);

		/// <summary>
		/// Validate the arguments to be supplied to a state path execution.
		/// </summary>
		/// <param name="statePathCodeName">The <see cref="StatePath.CodeName"/>.</param>
		/// <param name="actionArguments">The arguments to validate.</param>
		/// <returns>
		/// Returns a dictionary of validation error messages grouped by
		/// parameter key.
		/// </returns>
		IDictionary<string, ICollection<string>> ValidateStatePathArguments(
			string statePathCodeName,
			IDictionary<string, object> actionArguments);

		/// <summary>
		/// Validate the arguments to be supplied to a state path execution.
		/// </summary>
		/// <param name="statePathID">The ID of the state path.</param>
		/// <param name="actionArguments">The arguments to validate.</param>
		/// <returns>
		/// Returns a dictionary of validation error messages grouped by
		/// parameter key.
		/// </returns>
		Task<IDictionary<string, ICollection<string>>> ValidateStatePathArgumentsAsync(
			long statePathID,
			IDictionary<string, object> actionArguments);

		/// <summary>
		/// Get the set of all the possible next state paths which can 
		/// be executed on a <paramref name="stateful"/> object.
		/// Use <see cref="FilterAllowedStatePaths(SO, IEnumerable{StatePath})"/>
		/// to narrow the result to the paths which can be executed by the 
		/// current session user.
		/// </summary>
		/// <param name="stateful">The stateful object.</param>
		/// <returns>
		/// Returns all the next paths available to the state of a claim,
		/// irrespective of user rights.
		/// Use <see cref="FilterAllowedStatePaths(SO, IEnumerable{StatePath})"/>
		/// to only select the ones allowed by the current session user.
		/// </returns>
		IQueryable<StatePath> GetNextPaths(SO stateful);

		/// <summary>
		/// Filter the state paths which can be executed on a
		/// stateful object by the current session user.
		/// </summary>
		/// <param name="stateful">The claim to check execution upon.</param>
		/// <param name="statePaths">The paths to filter.</param>
		/// <returns>
		/// Returns the filtered list containing only the paths which can be executed 
		/// by the current session user.
		/// </returns>
		IEnumerable<StatePath> FilterAllowedStatePaths(SO stateful, IEnumerable<StatePath> statePaths);

		/// <summary>
		/// Get the set of state transitions of a stateful object.
		/// </summary>
		/// <param name="statefulID">The ID of the stateful object.</param>
		IQueryable<ST> GetStateTransitions(long statefulID);

		/// <summary>
		/// Get the set of state transitions of a stateful object.
		/// </summary>
		/// <param name="stateful">The stateful object.</param>
		IQueryable<ST> GetStateTransitions(SO stateful);

		/// <summary>
		/// Get the set of state paths leading to a given next state
		/// from the current state of a stateful object.
		/// </summary>
		/// <param name="stateful">The stateful object.</param>
		/// <param name="nextStateCodeName">The code name of the next state.</param>
		IQueryable<StatePath> GetPathsToState(SO stateful, string nextStateCodeName);

		/// <summary>
		/// Get the set of state paths leading to a given next state
		/// from the current state of a stateful object.
		/// </summary>
		/// <param name="stateful">The stateful object.</param>
		/// <param name="nextStateID">The ID of the next state.</param>
		IQueryable<StatePath> GetPathsToState(SO stateful, long nextStateID);

		/// <summary>
		/// Get the first <see cref="StatePath"/> which leads to a given state.
		/// </summary>
		/// <param name="stateful">The stateful object.</param>
		/// <param name="nextStateCodeName">The code name of the next state.</param>
		/// <returns>Returns the first path or null if no such path exists.</returns>
		/// <exception cref="LogicException">
		/// Thrown when the <see cref="WorkflowGraph"/> where the path belongs 
		/// works with a different <see cref="WorkflowGraph.StateTransitionTypeName"/>
		/// than <typeparamref name="ST"/>.
		/// </exception>
		Task<StatePath> GetPathToStateAsync(SO stateful, string nextStateCodeName);

		/// <summary>
		/// Get the first <see cref="StatePath"/> which leads to a given state.
		/// </summary>
		/// <param name="stateful">The stateful object.</param>
		/// <param name="nextStateID">The ID of the next state.</param>
		/// <returns>Returns the first path or null if no such path exists.</returns>
		/// <exception cref="LogicException">
		/// Thrown when the <see cref="WorkflowGraph"/> where the path belongs 
		/// works with a different <see cref="WorkflowGraph.StateTransitionTypeName"/>
		/// than <typeparamref name="ST"/>.
		/// </exception>
		Task<StatePath> GetPathToStateAsync(SO stateful, long nextStateID);

		/// <summary>
		/// Get the first <see cref="StatePath"/> which leads to a given state 
		/// and can be executed on a given stateful object.
		/// </summary>
		/// <param name="stateful">The stateful object.</param>
		/// <param name="nextStateCodeName">The code name of the next state.</param>
		/// <returns>Returns the first allowed path or null if no such path exists.</returns>
		/// <exception cref="LogicException">
		/// Thrown when the <see cref="WorkflowGraph"/> where the path belongs 
		/// works with a different <see cref="WorkflowGraph.StateTransitionTypeName"/>
		/// than <typeparamref name="ST"/>.
		/// </exception>
		Task<StatePath> GetAllowedPathToStateAsync(SO stateful, string nextStateCodeName);

		/// <summary>
		/// Get the first <see cref="StatePath"/> which leads to a given state 
		/// and can be executed on a given stateful object.
		/// </summary>
		/// <param name="stateful">The stateful object.</param>
		/// <param name="nextStateID">The ID of the next state.</param>
		/// <returns>Returns the first allowed path or null if no such path exists.</returns>
		/// <exception cref="LogicException">
		/// Thrown when the <see cref="WorkflowGraph"/> where the path belongs 
		/// works with a different <see cref="WorkflowGraph.StateTransitionTypeName"/>
		/// than <typeparamref name="ST"/>.
		/// </exception>
		Task<StatePath> GetAllowedPathToStateAsync(SO stateful, long nextStateID);

		/// <summary>
		/// Execute a path on a batch of stateful objects.
		/// </summary>
		/// <param name="statefulObjectsQuery">The query defining the stateful objects.</param>
		/// <param name="statePath">The state path to be executed.</param>
		/// <param name="actionArguments">
		/// The common arguments to be passed to all path actions. If "batchID" key is missing
		/// from the arguments, it will be added with a new GUID.
		/// </param>
		/// <returns>
		/// Returns a collection of <see cref="ExecutionResult{SO, ST}"/> items
		/// for each stateful object.
		/// </returns>
		/// <remarks>
		/// For best performance, the <see cref="StatePath.PreviousState"/>,
		/// <see cref="StatePath.NextState"/> and <see cref="StatePath.WorkflowGraph"/>
		/// properties of the <paramref name="statePath"/> must be eagerly loaded.
		/// </remarks>
		Task<IReadOnlyCollection<ExecutionResult<SO, ST>>> ExecuteStatePathBatchAsync(
			IQueryable<SO> statefulObjectsQuery,
			StatePath statePath,
			IDictionary<string, object> actionArguments);

		/// <summary>
		/// Execute a path on a batch of stateful objects.
		/// </summary>
		/// <param name="statefulObjects">The stateful objects.</param>
		/// <param name="statePath">The state path to be executed.</param>
		/// <param name="actionArguments">
		/// The common arguments to be passed to all path actions. If "batchID" key is missing
		/// from the arguments, it will be added with a new GUID.
		/// </param>
		/// <returns>
		/// Returns a collection of <see cref="ExecutionResult{SO, ST}"/> items
		/// for each stateful object.
		/// </returns>
		/// <remarks>
		/// For best performance, the <see cref="StatePath.PreviousState"/>,
		/// <see cref="StatePath.NextState"/> and <see cref="StatePath.WorkflowGraph"/>
		/// properties of the <paramref name="statePath"/> must be eagerly loaded
		/// as well as the <see cref="IStateful{U, ST}.State"/> 
		/// of the <paramref name="statefulObjects"/>.
		/// </remarks>
		Task<IReadOnlyCollection<ExecutionResult<SO, ST>>> ExecuteStatePathBatchAsync(
			IReadOnlyList<SO> statefulObjects,
			StatePath statePath,
			IDictionary<string, object> actionArguments);

		/// <summary>
		/// Execute a path on a batch of stateful objects.
		/// </summary>
		/// <param name="statefulObjectsQuery">The query defining the stateful objects.</param>
		/// <param name="pathCodeName">The <see cref="StatePath.CodeName"/> of the path.</param>
		/// <param name="actionArguments">
		/// The common arguments to be passed to all path actions. If "batchID" key is missing
		/// from the arguments, it will be added with a new GUID.
		/// </param>
		/// <returns>
		/// Returns a collection of <see cref="ExecutionResult{SO, ST}"/> items
		/// for each stateful object.
		/// </returns>
		/// <exception cref="LogicException">
		/// Thrown when no state path exists having
		/// the given <paramref name="pathCodeName"/>.
		/// </exception>
		Task<IReadOnlyCollection<ExecutionResult<SO, ST>>> ExecuteStatePathBatchAsync(
			IQueryable<SO> statefulObjectsQuery,
			string pathCodeName,
			IDictionary<string, object> actionArguments);

		/// <summary>
		/// Execute a path on a batch of stateful objects.
		/// </summary>
		/// <param name="statefulObjectsQuery">The query defining the stateful objects.</param>
		/// <param name="statePathID">The ID of the state path.</param>
		/// <param name="actionArguments">
		/// The common arguments to be passed to all path actions. If "batchID" key is missing
		/// from the arguments, it will be added with a new GUID.
		/// </param>
		/// <returns>
		/// Returns a collection of <see cref="ExecutionResult{SO, ST}"/> items
		/// for each stateful object.
		/// </returns>
		/// <exception cref="LogicException">
		/// Thrown when no state path exists having
		/// the given <paramref name="statePathID"/>.
		/// </exception>
		Task<IReadOnlyCollection<ExecutionResult<SO, ST>>> ExecuteStatePathBatchAsync(
			IQueryable<SO> statefulObjectsQuery,
			long statePathID,
			IDictionary<string, object> actionArguments);

		#endregion
	}
}
