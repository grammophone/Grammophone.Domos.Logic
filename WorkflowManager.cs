using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Grammophone.Caching;
using Grammophone.Domos.AccessChecking;
using Grammophone.Domos.DataAccess;
using Grammophone.Domos.Domain;
using Grammophone.Domos.Domain.Workflow;
using Grammophone.Domos.Logic.Configuration;
using Grammophone.Domos.Logic.Models.Workflow;
using Grammophone.Setup;

namespace Grammophone.Domos.Logic
{
	/// <summary>
	/// Base manager for querying and executing workflow actions.
	/// </summary>
	/// <typeparam name="U">The type of the user, derived from <see cref="User"/>.</typeparam>
	/// <typeparam name="BST">
	/// The base type of the system's state transitions, derived fom <see cref="StateTransition{U}"/>.
	/// </typeparam>
	/// <typeparam name="D">
	/// The type of domain container, derived from <see cref="IWorkflowUsersDomainContainer{U, ST}"/>.
	/// </typeparam>
	/// <typeparam name="S">
	/// The type of session, derived from <see cref="LogicSession{U, D}"/>.
	/// </typeparam>
	/// <typeparam name="ST">
	/// The type of state transition, derived from <typeparamref name="BST"/>.
	/// </typeparam>
	/// <typeparam name="SO">
	/// The type of stateful being managed, derived from <see cref="IStateful{U, ST}"/>.
	/// </typeparam>
	/// <typeparam name="C">
	/// The type of configurator used to setup 
	/// the <see cref="ConfiguredManager{U, D, S, C}.ManagerSettings"/> property,
	/// derived from <see cref="Configurator"/>.
	/// </typeparam>
	/// <remarks>
	/// This manager expects a dedicated Unity DI container for workflow, where at least there 
	/// are <see cref="StatePathConfiguration{U, D, S, ST, SO}"/> instances named 
	/// after <see cref="StatePath.CodeName"/> for every <see cref="StatePath"/> in the system.
	/// </remarks>
	public abstract class WorkflowManager<U, BST, D, S, ST, SO, C> 
		: ConfiguredManager<U, D, S, C>, IWorkflowManager<U, ST, SO>
		where U : User
		where BST : StateTransition<U>
		where D : IWorkflowUsersDomainContainer<U, BST>
		where S : LogicSession<U, D>
		where ST : BST, new()
		where SO : class, IStateful<U, ST>
		where C : Configurator, new()
	{
		#region Private fields

		private readonly AsyncLazy<ISet<long>> asyncLazyStatePathIDsSet;

		private readonly AsyncSequentialMRUCache<string, StatePath> asyncStatePathsByCodeNameCache;

		private readonly AsyncSequentialMRUCache<long, StatePath> asyncStatePathsByIDCache;

		private readonly AsyncSequentialMRUCache<string, State> asyncStatesByCodeNameCache;

		private readonly AsyncSequentialMRUCache<long, State> asyncStatesByIDCache;
 
		#endregion

		#region Construction

		/// <summary>
		/// Create.
		/// </summary>
		/// <param name="session">The owning session.</param>
		/// <param name="configurationSectionName">The name of the Unity configuration section dedicated for workflow.</param>
		protected WorkflowManager(S session, string configurationSectionName)
			: base(session, configurationSectionName)
		{
			asyncLazyStatePathIDsSet = new AsyncLazy<ISet<long>>(LoadStatePathIDsSet, false);

			asyncStatePathsByCodeNameCache = new AsyncSequentialMRUCache<string, StatePath>(async codeName => await LoadStatePathAsync(codeName));
			asyncStatePathsByIDCache = new AsyncSequentialMRUCache<long, StatePath>(async id => await LoadStatePathAsync(id));

			asyncStatesByCodeNameCache = new AsyncSequentialMRUCache<string, State>(async codeName => await LoadStateAsync(codeName));
			asyncStatesByIDCache = new AsyncSequentialMRUCache<long, State>(async id => await LoadStateAsync(id));
		}

		#endregion

		#region Public properties

		/// <summary>
		/// The states handled by the manager. The default implementation yields all states.
		/// </summary>
		public virtual IQueryable<State> States => from s in this.DomainContainer.States
																							 where this.StateGroups.Any(sg => sg.ID == s.GroupID)
																							 select s;

		/// <summary>
		/// The state groups handled by the manager. The default implementation yields all state groups.
		/// </summary>
		public virtual IQueryable<StateGroup> StateGroups => from sg in this.DomainContainer.StateGroups
																												 where this.WorkflowGraphs.Any(wg => wg.ID == sg.WorkflowGraphID)
																												 select sg;

		/// <summary>
		/// The workflow graphs handled by the manager. The default implementation yields all graphs.
		/// </summary>
		public virtual IQueryable<WorkflowGraph> WorkflowGraphs => this.DomainContainer.WorkflowGraphs;

		/// <summary>
		/// The state paths handled by the manager. The default implementation yields all state paths.
		/// </summary>
		public virtual IQueryable<StatePath> StatePaths => from sp in this.DomainContainer.StatePaths
																											 where this.WorkflowGraphs.Any(wg => wg.ID == sp.WorkflowGraphID)
																											 select sp;

		/// <summary>
		/// The state transitions
		/// of type <typeparamref name="ST"/> in the system.
		/// </summary>
		public virtual IQueryable<ST> StateTransitions => this.DomainContainer.StateTransitions.OfType<ST>();

		#endregion

		#region Public methods

		/// <summary>
		/// Get an object managed by this manager.
		/// </summary>
		/// <param name="objectID">The ID of the stateful object.</param>
		/// <returns>Returns the stateful object.</returns>
		/// <exception cref="InvalidOperationException">
		/// Thrown when the <paramref name="objectID"/> doesn't correspond to an object managed by this manager.
		/// </exception>
		public abstract Task<SO> GetStatefulObjectAsync(long objectID);

		/// <summary>
		/// Get the <see cref="StatePath"/> having the given code name among the <see cref="StatePaths"/>.
		/// </summary>
		/// <returns>Returns the path found.</returns>
		/// <exception cref="InvalidOperationException">
		/// Thrown when no <see cref="StatePath"/> exists having the given code name among <see cref="StatePaths"/>.
		/// </exception>
		/// <exception cref="LogicException">
		/// Thrown when the <see cref="WorkflowGraph"/> where the path belongs 
		/// works with a different <see cref="WorkflowGraph.StateTransitionTypeName"/>
		/// than <typeparamref name="ST"/>.
		/// </exception>
		/// <remarks>The results of this call are cached for the life of this manager.</remarks>
		public async Task<StatePath> GetStatePathAsync(string pathCodeName) => await asyncStatePathsByCodeNameCache.Get(pathCodeName);

		/// <summary>
		/// Get the <see cref="StatePath"/> among having the given ID among the <see cref="StatePaths"/> .
		/// </summary>
		/// <returns>Returns the path found.</returns>
		/// <exception cref="InvalidOperationException">
		/// Thrown when no <see cref="StatePath"/> exists having the given ID among <see cref="StatePaths"/>.
		/// </exception>
		/// <exception cref="LogicException">
		/// Thrown when the <see cref="WorkflowGraph"/> where the path belongs 
		/// works with a different <see cref="WorkflowGraph.StateTransitionTypeName"/>
		/// than <typeparamref name="ST"/>.
		/// </exception>
		/// <remarks>The results of this call are cached for the life of this manager.</remarks>
		public async Task<StatePath> GetStatePathAsync(long pathID) => await asyncStatePathsByIDCache.Get(pathID);

		/// <summary>
		/// Load the <see cref="State"/> having the given code name among the <see cref="States"/>.
		/// </summary>
		/// <param name="stateCodeName">The code name of the state.</param>
		/// <returns>Returns the path found.</returns>
		/// <exception cref="InvalidOperationException">
		/// Thrown when no <see cref="State"/> exists having the given code name among <see cref="States"/>.
		/// </exception>
		/// <exception cref="LogicException">
		/// Thrown when the <see cref="WorkflowGraph"/> where the state belongs 
		/// works with a different <see cref="WorkflowGraph.StateTransitionTypeName"/>
		/// than <typeparamref name="ST"/>.
		/// </exception>
		/// <remarks>The results of this call are cached for the life of this manager.</remarks>
		public async Task<State> GetStateAsync(string stateCodeName) => await asyncStatesByCodeNameCache.Get(stateCodeName);

		/// <summary>
		/// Load the <see cref="State"/> among having the given ID among the <see cref="States"/> .
		/// </summary>
		/// <param name="stateID">The ID of the state.</param>
		/// <returns>Returns the path found.</returns>
		/// <exception cref="InvalidOperationException">
		/// Thrown when no <see cref="State"/> exists having the given ID among <see cref="States"/>.
		/// </exception>
		/// <exception cref="LogicException">
		/// Thrown when the <see cref="WorkflowGraph"/> where the state belongs 
		/// works with a different <see cref="WorkflowGraph.StateTransitionTypeName"/>
		/// than <typeparamref name="ST"/>.
		/// </exception>
		/// <remarks>The results of this call are cached for the life of this manager.</remarks>
		public async Task<State> GetStateAsync(long stateID) => await asyncStatesByIDCache.Get(stateID);

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
		/// or when the path doesn't exist among <see cref="StatePaths"/>,
		/// or when the path's workflow is not compatible with transitions 
		/// of type <typeparamref name="ST"/>.
		/// </exception>
		public async Task<ST> ExecuteStatePathAsync(
			SO stateful, 
			string pathCodeName, 
			IDictionary<string, object> actionArguments)
		{
			if (stateful == null) throw new ArgumentNullException(nameof(stateful));
			if (pathCodeName == null) throw new ArgumentNullException(nameof(pathCodeName));
			if (actionArguments == null) throw new ArgumentNullException(nameof(actionArguments));

			var path = await GetStatePathAsync(pathCodeName);

			return await ExecuteStatePathImplementationAsync(stateful, path, actionArguments);
		}

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
		/// or when the path doesn't exist among <see cref="StatePaths"/>,
		/// or when the path's workflow is not compatible with transitions 
		/// of type <typeparamref name="ST"/>.
		/// </exception>
		public async Task<ST> ExecuteStatePathAsync(
			SO stateful,
			long pathID,
			IDictionary<string, object> actionArguments)
		{
			if (stateful == null) throw new ArgumentNullException(nameof(stateful));
			if (actionArguments == null) throw new ArgumentNullException(nameof(actionArguments));

			var path = await GetStatePathAsync(pathID);

			return await ExecuteStatePathImplementationAsync(stateful, path, actionArguments);
		}

		/// <summary>
		/// Execute a state path against a stateful instance.
		/// </summary>
		/// <param name="stateful">The stateful instance to execute the transition upon.</param>
		/// <param name="statePath">The state path.</param>
		/// <param name="actionArguments">A dictinary of arguments to be passed to the path actions.</param>
		/// <returns>Returns the state transition created.</returns>
		/// <exception cref="StatePathAccessDeniedException">
		/// Thrown when the session user has no right to execute the state path.
		/// </exception>
		/// <exception cref="StatePathArgumentsException">
		/// Thrown when the <paramref name="actionArguments"/> are not valid
		/// against the parameter specifications of the path actions.
		/// </exception>
		/// <exception cref="LogicException">
		/// Thrown when the <see cref="WorkflowGraph"/> where the path belongs 
		/// works with a different <see cref="WorkflowGraph.StateTransitionTypeName"/>
		/// than <typeparamref name="ST"/> or when the <paramref name="statePath"/>
		/// does not belong in the <see cref="StatePaths"/>.
		/// </exception>
		/// <exception cref="UserException">
		/// Thrown when the specified path 
		/// is not available for the current state of the stateful object.
		/// </exception>
		/// <remarks>
		/// For best performance, the <see cref="StatePath.PreviousState"/>,
		/// <see cref="StatePath.NextState"/> and <see cref="StatePath.WorkflowGraph"/>
		/// properties of the <paramref name="statePath"/> must be eagerly loaded.
		/// </remarks>
		public virtual async Task<ST> ExecuteStatePathAsync(
			SO stateful,
			StatePath statePath,
			IDictionary<string, object> actionArguments)
		{
			if (stateful == null) throw new ArgumentNullException(nameof(stateful));
			if (statePath == null) throw new ArgumentNullException(nameof(statePath));
			if (actionArguments == null) throw new ArgumentNullException(nameof(actionArguments));

			ValidatePath(statePath);

			await EnsureStatePathIsInSetAsync(statePath.ID);

			return await ExecuteStatePathImplementationAsync(stateful, statePath, actionArguments);
		}

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
		public IReadOnlyDictionary<string, ParameterSpecification> GetPathParameterSpecifications(
			string pathCodeName)
		{
			if (pathCodeName == null) throw new ArgumentNullException(nameof(pathCodeName));

			var statePathConfiguration = GetStatePathConfiguration(pathCodeName);

			return GetPathParameterSpecifications(statePathConfiguration);
		}

		/// <summary>
		/// Get the specifications of parameters required by all pre-actions
		/// and post-actions of a state path.
		/// </summary>
		/// <param name="configurationSectionName">The name of the Unity configuration section dedicated to the manager.</param>
		/// <param name="pathCodeName">The code name of the state path.</param>
		/// <returns>
		/// Returns a dictionary of the parameter specifications having as key
		/// the <see cref="ParameterSpecification.Key"/> property.
		/// If actions specify parametrs with overlapping keys, the last one
		/// in the actions list overwrites the previous, first from pre-actions to
		/// post-actions.
		/// </returns>
		public static IReadOnlyDictionary<string, ParameterSpecification> GetPathParameterSpecifications(
			string configurationSectionName,
			string pathCodeName)
		{
			if (configurationSectionName == null) throw new ArgumentNullException(nameof(configurationSectionName));
			if (pathCodeName == null) throw new ArgumentNullException(nameof(pathCodeName));

			var settings = settingsFactory.Get(configurationSectionName);

			var configuration = GetStatePathConfiguration(settings, pathCodeName);

			return GetPathParameterSpecifications(configuration);
		}

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
		public async Task<IReadOnlyDictionary<string, ParameterSpecification>> GetPathParameterSpecificationsAsync(
			long statePathID)
		{
			var statePath = await GetStatePathAsync(statePathID);

			return GetPathParameterSpecifications(statePath.CodeName);
		}

		/// <summary>
		/// Validate the arguments to be supplied to a state path execution.
		/// </summary>
		/// <param name="statePathCodeName">The <see cref="StatePath.CodeName"/>.</param>
		/// <param name="actionArguments">The arguments to validate.</param>
		/// <returns>
		/// Returns a dictionary of validation error messages grouped by
		/// parameter key.
		/// </returns>
		public IDictionary<string, ICollection<string>> ValidateStatePathArguments(
			string statePathCodeName,
			IDictionary<string, object> actionArguments)
		{
			if (statePathCodeName == null) throw new ArgumentNullException(nameof(statePathCodeName));
			if (actionArguments == null) throw new ArgumentNullException(nameof(actionArguments));

			var parameterSpecifications = GetPathParameterSpecifications(statePathCodeName);

			var validationDictionary = new Dictionary<string, ICollection<string>>();

			foreach (var entry in parameterSpecifications)
			{
				string parameterKey = entry.Key;
				var parameterSpecification = entry.Value;

				object value;

				if (!actionArguments.TryGetValue(parameterKey, out value) && parameterSpecification.IsRequired)
				{
					AddValidationEntry(validationDictionary, entry.Key, WorkflowManagerMessages.PARAMETER_IS_REQUIRED);
				}
				else
				{
					if (value == null)
					{
						if (parameterSpecification.IsRequired)
						{
							AddValidationEntry(validationDictionary, entry.Key, WorkflowManagerMessages.PARAMETER_IS_REQUIRED);
						}
					}
					else
					{
						if (!parameterSpecification.Type.IsAssignableFrom(value.GetType()))
						{
							AddValidationEntry(validationDictionary, parameterKey, WorkflowManagerMessages.WRONG_PARAMETER_TYPE);
						}
					}
				}
			}

			return validationDictionary;
		}

		/// <summary>
		/// Validate the arguments to be supplied to a state path execution.
		/// </summary>
		/// <param name="statePathID">The ID of the state path.</param>
		/// <param name="actionArguments">The arguments to validate.</param>
		/// <returns>
		/// Returns a dictionary of validation error messages grouped by
		/// parameter key.
		/// </returns>
		public async Task<IDictionary<string, ICollection<string>>> ValidateStatePathArgumentsAsync(
			long statePathID,
			IDictionary<string, object> actionArguments)
		{
			if (actionArguments == null) throw new ArgumentNullException(nameof(actionArguments));

			var statePath = await GetStatePathAsync(statePathID);

			return ValidateStatePathArguments(statePath.CodeName, actionArguments);
		}

		/// <summary>
		/// Get the set of all the possible next state paths which can 
		/// be executed on a <paramref name="stateful"/> object.
		/// Use <see cref="FilterAllowedStatePaths(SO, IEnumerable{StatePath})"/>
		/// to narrow the result to the paths which can be executed by the 
		/// current session user.
		/// </summary>
		/// <param name="stateful">The stateful object.</param>
		/// <returns>
		/// Returns all the next paths available to the state of a stateful object,
		/// irrespective of user rights.
		/// Use <see cref="FilterAllowedStatePaths(SO, IEnumerable{StatePath})"/>
		/// to only select the ones allowed by the current session user.
		/// </returns>
		public IQueryable<StatePath> GetNextPaths(SO stateful)
		{
			if (stateful == null) throw new ArgumentNullException(nameof(stateful));

			long currentStateID = stateful.State.ID;

			return from sp in this.StatePaths
						 where currentStateID == sp.PreviousStateID
						 select sp;
		}

		/// <summary>
		/// Filter the state paths which can be executed on a
		/// stateful object by the current session user.
		/// </summary>
		/// <param name="stateful">The stateful object to check execution upon.</param>
		/// <param name="statePaths">The paths to filter.</param>
		/// <returns>
		/// Returns the filtered list containing only the paths which can be executed 
		/// by the current session user.
		/// </returns>
		public IEnumerable<StatePath> FilterAllowedStatePaths(SO stateful, IEnumerable<StatePath> statePaths)
		{
			if (statePaths == null) throw new ArgumentNullException(nameof(statePaths));

			return statePaths.AsEnumerable()
				.Where(sp => this.AccessResolver.CanUserExecuteStatePath(this.Session.User, stateful, sp));
		}

		/// <summary>
		/// Filter the state paths which can be executed on a
		/// stateful object by the current session user.
		/// The user must alse have read and write access rights on the stateful object to be allowed path execution.
		/// </summary>
		/// <param name="statePaths">The paths to filter.</param>
		/// <param name="segregation">Optional segregation where a stateful object may belong.</param>
		/// <returns>
		/// Returns the filtered list containing only the paths which can be executed 
		/// by the current session user.
		/// </returns>
		public IEnumerable<StatePath> FilterAllowedStatePaths(IEnumerable<StatePath> statePaths, Segregation<U> segregation = null)
		{
			if (statePaths == null) throw new ArgumentNullException(nameof(statePaths));

			return statePaths.AsEnumerable()
				.Where(sp => this.AccessResolver.CanUserExecuteStatePath(this.Session.User, sp, segregation));
		}

		/// <summary>
		/// Get the set of all the possible next state paths which are allowed to 
		/// be executed by the current user on a stateful object.
		/// </summary>
		/// <param name="statefulID">The ID of the stateful object.</param>
		/// <returns>
		/// Returns all the state paths which are allowed to be executed by the current user.
		/// </returns>
		public async Task<IEnumerable<StatePath>> GetAllowedNextPathsAsync(long statefulID)
		{
			SO stateful = await GetStatefulObjectAsync(statefulID);

			return await GetAllowedNextPathsAsync(stateful);
		}

		/// <summary>
		/// Get the set of all the possible next state paths which are allowed to 
		/// be executed by the current user on a <paramref name="stateful"/> object.
		/// </summary>
		/// <param name="stateful">The stateful object.</param>
		/// <returns>
		/// Returns all the state paths which are allowed to be executed by the current user.
		/// </returns>
		public async Task<IEnumerable<StatePath>> GetAllowedNextPathsAsync(SO stateful)
		{
			var nextPaths = await GetNextPaths(stateful)
				.Include(sp => sp.PreviousState)
				.Include(sp => sp.NextState)
				.Include(sp => sp.WorkflowGraph)
				.ToArrayAsync();

			return FilterAllowedStatePaths(stateful, nextPaths);
		}

		/// <summary>
		/// Get the set of state transitions of a stateful object.
		/// </summary>
		/// <param name="statefulID">The ID of the stateful object.</param>
		public abstract IQueryable<ST> GetStateTransitions(long statefulID);

		/// <summary>
		/// Get the set of state transitions of a stateful object.
		/// </summary>
		/// <param name="stateful">The stateful object.</param>
		public IQueryable<ST> GetStateTransitions(SO stateful)
		{
			if (stateful == null) throw new ArgumentNullException(nameof(stateful));

			return GetStateTransitions(stateful.ID);
		}

		/// <summary>
		/// Get the set of state paths leading to a given next state
		/// from the current state of a stateful object.
		/// </summary>
		/// <param name="stateful">The stateful object.</param>
		/// <param name="nextStateCodeName">The code name of the next state.</param>
		public IQueryable<StatePath> GetPathsToState(SO stateful, string nextStateCodeName)
		{
			long currentStateID = stateful.State.ID;

			return from sp in this.StatePaths
						 where sp.PreviousStateID == currentStateID
						 && sp.NextState.CodeName == nextStateCodeName
						 select sp;
		}

		/// <summary>
		/// Get the set of state paths leading to a given next state
		/// from the current state of a stateful object.
		/// </summary>
		/// <param name="stateful">The stateful object.</param>
		/// <param name="nextStateID">The ID of the next state.</param>
		public IQueryable<StatePath> GetPathsToState(SO stateful, long nextStateID)
		{
			long currentStateID = stateful.State.ID;

			return from sp in this.StatePaths
						 where sp.PreviousStateID == currentStateID
						 && sp.NextStateID == nextStateID
						 select sp;
		}

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
		public async Task<StatePath> TryGetPathToStateAsync(SO stateful, string nextStateCodeName)
		{
			var nextPath = await
				GetPathsToState(stateful, nextStateCodeName)
				.Include(sp => sp.PreviousState)
				.Include(sp => sp.NextState)
				.Include(sp => sp.WorkflowGraph)
				.FirstOrDefaultAsync();

			if (nextPath != null) ValidatePath(nextPath);

			return nextPath;
		}

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
		public async Task<StatePath> TryGetPathToStateAsync(SO stateful, long nextStateID)
		{
			var nextPath = await
				GetPathsToState(stateful, nextStateID)
				.Include(sp => sp.PreviousState)
				.Include(sp => sp.NextState)
				.Include(sp => sp.WorkflowGraph)
				.FirstOrDefaultAsync();

			if (nextPath != null) ValidatePath(nextPath);

			return nextPath;
		}

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
		public async Task<StatePath> TryGetAllowedPathToStateAsync(SO stateful, string nextStateCodeName)
		{
			var nextPaths = await
				GetPathsToState(stateful, nextStateCodeName)
				.Include(sp => sp.PreviousState)
				.Include(sp => sp.NextState)
				.Include(sp => sp.WorkflowGraph)
				.ToArrayAsync();

			var allowedNextPath = FilterAllowedStatePaths(stateful, nextPaths)
				.FirstOrDefault();

			if (allowedNextPath != null) ValidatePath(allowedNextPath);

			return allowedNextPath;
		}

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
		public async Task<StatePath> TryGetAllowedPathToStateAsync(SO stateful, long nextStateID)
		{
			var nextPaths = await
				GetPathsToState(stateful, nextStateID)
				.Include(sp => sp.PreviousState)
				.Include(sp => sp.NextState)
				.Include(sp => sp.WorkflowGraph)
				.ToArrayAsync();

			var allowedNextPath = FilterAllowedStatePaths(stateful, nextPaths)
				.FirstOrDefault();

			if (allowedNextPath != null) ValidatePath(allowedNextPath);

			return allowedNextPath;
		}

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
		/// as well as the <see cref="IStateful.State"/> 
		/// of the <paramref name="statefulObjects"/>.
		/// </remarks>
		public async Task<IReadOnlyCollection<ExecutionResult<SO, ST>>> ExecuteStatePathBatchAsync(
			IEnumerable<SO> statefulObjects,
			StatePath statePath,
			IDictionary<string, object> actionArguments)
		{
			if (statefulObjects == null) throw new ArgumentNullException(nameof(statefulObjects));
			if (statePath == null) throw new ArgumentNullException(nameof(statePath));
			if (actionArguments == null) throw new ArgumentNullException(nameof(actionArguments));

			ValidatePath(statePath);

			await EnsureStatePathIsInSetAsync(statePath.ID);

			var executionResults = new List<ExecutionResult<SO, ST>>(statefulObjects.Count());

			foreach (var statefulObject in statefulObjects)
			{
				var result = new ExecutionResult<SO, ST>
				{
					StatefulObject = statefulObject
				};

				try
				{
					if (statefulObject == null) throw new ArgumentException("There is a null item in the list of stateful objects.", nameof(statefulObjects));

					var transition = await ExecuteStatePathImplementationAsync(
						statefulObject,
						statePath,
						actionArguments);

					result.StateTransition = transition;
				}
				catch (Exception ex)
				{
					result.Exception = ex;
				}

				executionResults.Add(result);
			}

			return executionResults;
		}

		/// <summary>
		/// Execute a path on a batch of stateful objects.
		/// </summary>
		/// <param name="statefulObjects">The stateful objects.</param>
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
		public async Task<IReadOnlyCollection<ExecutionResult<SO, ST>>> ExecuteStatePathBatchAsync(
			IEnumerable<SO> statefulObjects,
			string pathCodeName,
			IDictionary<string, object> actionArguments)
		{
			if (statefulObjects == null) throw new ArgumentNullException(nameof(statefulObjects));
			if (pathCodeName == null) throw new ArgumentNullException(nameof(pathCodeName));
			if (actionArguments == null) throw new ArgumentNullException(nameof(actionArguments));

			var statePath = await GetStatePathAsync(pathCodeName);

			return await ExecuteStatePathBatchAsync(statefulObjects, statePath, actionArguments);
		}

		/// <summary>
		/// Execute a path on a batch of stateful objects.
		/// </summary>
		/// <param name="statefulObjects">The stateful objects.</param>
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
		public async Task<IReadOnlyCollection<ExecutionResult<SO, ST>>> ExecuteStatePathBatchAsync(
			IEnumerable<SO> statefulObjects,
			long statePathID,
			IDictionary<string, object> actionArguments)
		{
			if (statefulObjects == null) throw new ArgumentNullException(nameof(statefulObjects));
			if (actionArguments == null) throw new ArgumentNullException(nameof(actionArguments));

			var statePath = await GetStatePathAsync(statePathID);

			return await ExecuteStatePathBatchAsync(statefulObjects, statePath, actionArguments);
		}

		#endregion

		#region Private methods

		/// <summary>
		/// Load the set of IDs of the <see cref="StatePaths"/>.
		/// </summary>
		private async Task<ISet<long>> LoadStatePathIDsSet()
			=> (await this.StatePaths.Select(sp => sp.ID).ToArrayAsync()).ToHashSet();

		/// <summary>
		/// Execute a collection of workflow actions.
		/// </summary>
		/// <param name="actions">The actions to execute.</param>
		/// <param name="stateful">The stateful instance against which the actions are executed.</param>
		/// <param name="stateTransition">The state transition.</param>
		/// <param name="actionArguments">The arguments to the actions.</param>
		/// <returns>Returns a task completing the actions.</returns>
		private async Task ExecuteActionsAsync(
			IEnumerable<IWorkflowAction<U, D, S, ST, SO>> actions, 
			SO stateful,
			ST stateTransition, 
			IDictionary<string, object> actionArguments)
		{
			if (actions == null) throw new ArgumentNullException(nameof(actions));

			foreach (var action in actions)
			{
				await action.ExecuteAsync(
					this.Session, 
					this.DomainContainer, 
					stateful, 
					stateTransition, 
					actionArguments);
			}
		}

		/// <summary>
		/// Get the pre and post actions for a state paths.
		/// </summary>
		/// <param name="pathCodeName">The <see cref="StatePath.CodeName"/> of the path.</param>
		/// <returns>Returns the actions specifications.</returns>
		private StatePathConfiguration<U, D, S, ST, SO> GetStatePathConfiguration(string pathCodeName)
			=> GetStatePathConfiguration(this.ManagerSettings, pathCodeName);

		/// <summary>
		/// Get the pre and post actions for a state paths.
		/// </summary>
		/// <param name="managerSettings">The settings containing the state paths configuration.</param>
		/// <param name="pathCodeName">The <see cref="StatePath.CodeName"/> of the path.</param>
		/// <returns>Returns the actions specifications.</returns>
		private static StatePathConfiguration<U, D, S, ST, SO> GetStatePathConfiguration(Settings managerSettings, string pathCodeName)
		{
			if (managerSettings == null) throw new ArgumentNullException(nameof(managerSettings));
			if (pathCodeName == null) throw new ArgumentNullException(nameof(pathCodeName));

			return managerSettings.Resolve<StatePathConfiguration<U, D, S, ST, SO>>(pathCodeName);
		}

		/// <summary>
		/// Load the <see cref="StatePath"/> having the given code name among the <see cref="StatePaths"/>.
		/// </summary>
		/// <returns>Returns the path found.</returns>
		/// <exception cref="InvalidOperationException">
		/// Thrown when no <see cref="StatePath"/> exists having the given ID among <see cref="StatePaths"/>.
		/// </exception>
		/// <exception cref="LogicException">
		/// Thrown when the <see cref="WorkflowGraph"/> where the path belongs 
		/// works with a different <see cref="WorkflowGraph.StateTransitionTypeName"/>
		/// than <typeparamref name="ST"/>.
		/// </exception>
		private async Task<StatePath> LoadStatePathAsync(string pathCodeName)
		{
			var pathQuery = this.StatePaths
				.Where(sp => sp.CodeName == pathCodeName);

			return await LoadStatePathAsync(pathQuery);
		}

		/// <summary>
		/// Load the <see cref="StatePath"/> among having the given ID among the <see cref="StatePaths"/> .
		/// </summary>
		/// <returns>Returns the path found.</returns>
		/// <exception cref="InvalidOperationException">
		/// Thrown when no <see cref="StatePath"/> exists having the given code name among <see cref="StatePaths"/>.
		/// </exception>
		/// <exception cref="LogicException">
		/// Thrown when the <see cref="WorkflowGraph"/> where the path belongs 
		/// works with a different <see cref="WorkflowGraph.StateTransitionTypeName"/>
		/// than <typeparamref name="ST"/>.
		/// </exception>
		private async Task<StatePath> LoadStatePathAsync(long pathID)
		{
			var pathQuery = this.StatePaths
				.Where(sp => sp.ID == pathID);

			return await LoadStatePathAsync(pathQuery);
		}

		/// <summary>
		/// Load a <see cref="StatePath"/> via a query.
		/// </summary>
		/// <param name="pathQuery">The query to execute.</param>
		/// <returns>Returns the path found.</returns>
		/// <exception cref="LogicException">
		/// Thrown when no <see cref="StatePath"/> exists in the query
		/// or when the <see cref="WorkflowGraph"/> where the path belongs 
		/// works with a different <see cref="WorkflowGraph.StateTransitionTypeName"/>
		/// than <typeparamref name="ST"/>.
		/// </exception>
		private async Task<StatePath> LoadStatePathAsync(IQueryable<StatePath> pathQuery)
		{
			if (pathQuery == null) throw new ArgumentNullException(nameof(pathQuery));

			pathQuery = pathQuery
				.Include(sp => sp.PreviousState)
				.Include(sp => sp.NextState)
				.Include(sp => sp.WorkflowGraph);

			var path = await pathQuery.SingleAsync();

			ValidatePath(path);

			return path;
		}

		/// <summary>
		/// Load the <see cref="State"/> having the given code name among the <see cref="States"/>.
		/// </summary>
		/// <returns>Returns the path found.</returns>
		/// <exception cref="InvalidOperationException">
		/// Thrown when no <see cref="State"/> exists having the given code name among <see cref="States"/>.
		/// </exception>
		/// <exception cref="LogicException">
		/// Thrown when the <see cref="WorkflowGraph"/> where the state belongs 
		/// works with a different <see cref="WorkflowGraph.StateTransitionTypeName"/>
		/// than <typeparamref name="ST"/>.
		/// </exception>
		private async Task<State> LoadStateAsync(string stateCodeName)
		{
			if (stateCodeName == null) throw new ArgumentNullException(nameof(stateCodeName));

			var query = from s in this.States
									where s.CodeName == stateCodeName
									select s;

			return await LoadStateAsync(query);
		}

		/// <summary>
		/// Load the <see cref="State"/> among having the given ID among the <see cref="States"/> .
		/// </summary>
		/// <returns>Returns the path found.</returns>
		/// <exception cref="InvalidOperationException">
		/// Thrown when no <see cref="State"/> exists having the given ID among <see cref="States"/>.
		/// </exception>
		/// <exception cref="LogicException">
		/// Thrown when the <see cref="WorkflowGraph"/> where the state belongs 
		/// works with a different <see cref="WorkflowGraph.StateTransitionTypeName"/>
		/// than <typeparamref name="ST"/>.
		/// </exception>
		private async Task<State> LoadStateAsync(long stateID)
		{
			var query = from s in this.States
									where s.ID == stateID
									select s;

			return await LoadStateAsync(query);
		}

		/// <summary>
		/// Load a <see cref="State"/> via a query.
		/// </summary>
		/// <param name="stateQuery">The query to execute.</param>
		/// <returns>Returns the path found.</returns>
		/// <exception cref="InvalidOperationException">
		/// Thrown when the query specified other than one <see cref="State"/> exactly.
		/// </exception>
		/// <exception cref="LogicException">
		/// Thrown when the <see cref="WorkflowGraph"/> where the state belongs 
		/// works with a different <see cref="WorkflowGraph.StateTransitionTypeName"/>
		/// than <typeparamref name="ST"/>.
		/// </exception>
		private async Task<State> LoadStateAsync(IQueryable<State> stateQuery)
		{
			stateQuery = stateQuery.Include(s => s.Group.WorkflowGraph);

			var state = await stateQuery.SingleAsync();

			ValidateWorkflowGraph(state.Group.WorkflowGraph);

			return state;
		}

		/// <summary>
		/// Ensure that a given path belongs to a workflow
		/// working with state transitions of type <typeparamref name="ST"/> and that it has been unchanged.
		/// Any detached, added, modified or deleted state paths are not accepted.
		/// </summary>
		/// <param name="path">The path to validate.</param>
		/// <exception cref="LogicException">
		/// Thrown when the <see cref="WorkflowGraph"/> where the path belongs 
		/// works with a different <see cref="WorkflowGraph.StateTransitionTypeName"/>
		/// than <typeparamref name="ST"/> or if it is not unchanged.
		/// </exception>
		private void ValidatePath(StatePath path)
		{
			if (path == null) throw new ArgumentNullException(nameof(path));

			ValidateWorkflowGraph(path.WorkflowGraph);

			var pathEntry = this.DomainContainer.Entry(path);

			if (pathEntry.State != Grammophone.DataAccess.TrackingState.Unchanged)
				throw new LogicException(
					$"The state path must be unchanged. Any detached, added, modified or deleted state paths are not accepted.");
		}

		/// <summary>
		/// Ensure that a given workflow graph
		/// works with state transitions of type <typeparamref name="ST"/>.
		/// </summary>
		/// <param name="workflowGraph">The workflow graph to validate.</param>
		/// <exception cref="LogicException">
		/// Thrown when the <see cref="WorkflowGraph"/> where the path belongs 
		/// works with a different <see cref="WorkflowGraph.StateTransitionTypeName"/>
		/// than <typeparamref name="ST"/> or if it is not unchanged.
		/// </exception>
		private void ValidateWorkflowGraph(WorkflowGraph workflowGraph)
		{
			if (workflowGraph == null) throw new ArgumentNullException(nameof(workflowGraph));

			if (workflowGraph.StateTransitionTypeName != typeof(ST).FullName)
				throw new LogicException(
					$"The state path must work with transitions of type {workflowGraph.StateTransitionTypeName}.");
		}

		/// <summary>
		/// Add an entry to a dictionary containing parameter validations.
		/// </summary>
		/// <param name="validationDictionary">The validation dictionary.</param>
		/// <param name="parameterKey">The key of the parameter.</param>
		/// <param name="message">The validation message.</param>
		private void AddValidationEntry(
			Dictionary<string, ICollection<string>> validationDictionary,
			string parameterKey,
			string message)
		{
			ICollection<string> parameterValidationMessages;

			if (!validationDictionary.TryGetValue(parameterKey, out parameterValidationMessages))
			{
				parameterValidationMessages = new List<string>();
				validationDictionary[parameterKey] = parameterValidationMessages;
			}

			parameterValidationMessages.Add(message);
		}

		/// <summary>
		/// Get the specifications of parameters required by all pre-actions
		/// and post-actions of a state path.
		/// </summary>
		/// <param name="statePathConfiguration">The configuration of the state path.</param>
		/// <returns>
		/// Returns a dictionary of the parameter specifications having as key 
		/// the <see cref="ParameterSpecification.Key"/> property.
		/// If actions specify parametrs with overlapping keys, the last one 
		/// in the actions list overwrites the previous, first from pre-actions to
		/// post-actions.
		/// </returns>
		private static IReadOnlyDictionary<string, ParameterSpecification> GetPathParameterSpecifications(
			StatePathConfiguration<U, D, S, ST, SO> statePathConfiguration)
		{
			var parameterSpecificationsByKey = new Dictionary<string, ParameterSpecification>();

			foreach (var action in statePathConfiguration.PreActions)
			{
				var actionParameterSpecifications = action.GetParameterSpecifications();

				foreach (var parameterSpecification in actionParameterSpecifications)
				{
					parameterSpecificationsByKey[parameterSpecification.Key] = parameterSpecification;
				}
			}

			foreach (var action in statePathConfiguration.PostActions)
			{
				var actionParameterSpecifications = action.GetParameterSpecifications();

				foreach (var parameterSpecification in actionParameterSpecifications)
				{
					if (parameterSpecificationsByKey.ContainsKey(parameterSpecification.Key)) continue; // Don't overwrite pre-actions parameters.

					parameterSpecificationsByKey[parameterSpecification.Key] = parameterSpecification;
				}
			}

			return parameterSpecificationsByKey;
		}

		/// <summary>
		/// Implementation of execution of a state path.
		/// The arguments are considered to non-null and validated.
		/// </summary>
		/// <param name="stateful"></param>
		/// <param name="statePath"></param>
		/// <param name="actionArguments"></param>
		/// <returns></returns>
		private async Task<ST> ExecuteStatePathImplementationAsync(SO stateful, StatePath statePath, IDictionary<string, object> actionArguments)
		{
			if (!this.AccessResolver.CanUserExecuteStatePath(this.Session.User, stateful, statePath))
			{
				string message;

				if (this.Session.User != null)
				{
					message = $"The user with ID {this.Session.User.ID} has no rights " +
						$"to execute path '{statePath.CodeName}' against the {AccessRight.GetEntityTypeName(stateful)} with ID {stateful.ID}.";
				}
				else
				{
					message = $"The anonymous user has no rights " +
						$"to execute path '{statePath.CodeName}' against the {AccessRight.GetEntityTypeName(stateful)} with ID {stateful.ID}.";
				}

				this.ClassLogger.Log(Logging.LogLevel.Warn, message);

				throw new StatePathAccessDeniedException(statePath, stateful, message);
			}

			var validationErrors = ValidateStatePathArguments(statePath.CodeName, actionArguments);

			if (validationErrors.Count > 0)
				throw new StatePathArgumentsException(validationErrors);

			using (var transaction = this.DomainContainer.BeginTransaction())
			{
				var statefulObjectEntry = this.DomainContainer.Entry(stateful.GetBackingDomainEntity());

				switch (statefulObjectEntry.State)
				{
					case Grammophone.DataAccess.TrackingState.Unchanged: // Get the most fresh possible contents of the stateful obbject. 
						await statefulObjectEntry.ReloadAsync();
						break;
				}

				if (stateful.State != statePath.PreviousState)
					throw new UserException(String.Format(WorkflowManagerMessages.INCOMPATIBLE_STATE_PATH, statePath.Name, stateful.State.Name, stateful.ID));

				ST stateTransition = this.DomainContainer.StateTransitions.Create<ST>();

				var statePathConfiguration = GetStatePathConfiguration(statePath.CodeName);

				stateTransition.BindToStateful(stateful);

				var now = DateTime.UtcNow;

				stateTransition.Path = statePath;
				stateTransition.ChangeStampBefore = stateful.ChangeStamp;

				stateful.LastStateChangeDate = now;

				if (statePath.PreviousState.GroupID != statePath.NextState.GroupID)
				{
					stateful.LastStateGroupChangeDate = now;
				}

				await ExecuteActionsAsync(statePathConfiguration.PreActions, stateful, stateTransition, actionArguments);

				stateful.State = statePath.NextState;

				stateful.ChangeStamp &= statePath.ChangeStampANDMask;
				stateful.ChangeStamp |= statePath.ChangeStampORMask;

				stateTransition.ChangeStampAfter = stateful.ChangeStamp;

				await ExecuteActionsAsync(statePathConfiguration.PostActions, stateful, stateTransition, actionArguments);

				await transaction.CommitAsync();

				return stateTransition;
			}
		}

		/// <summary>
		/// Ensure that a state path belongs to the <see cref="StatePaths"/> set, else
		/// throw a <see cref="LogicException"/>.
		/// </summary>
		/// <param name="statePathID">The ID of the state path.</param>
		/// <exception cref="LogicException">
		/// Thrown when the <paramref name="statePathID"/> does not correspond to a path in <see cref="StatePaths"/>.
		/// </exception>
		private async Task EnsureStatePathIsInSetAsync(long statePathID)
		{
			var statePathsIDsSet = await asyncLazyStatePathIDsSet.Value;

			if (!statePathsIDsSet.Contains(statePathID))
			{
				throw new LogicException($"The state path with ID {statePathID} does not exist among the designated state paths.");
			}
		}

		#endregion
	}
}
