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
using Microsoft.Practices.Unity;

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
	/// The type of session, derived from <see cref="Session{U, D}"/>.
	/// </typeparam>
	/// <typeparam name="ST">
	/// The type of state transition, derived from <typeparamref name="BST"/>.
	/// </typeparam>
	/// <typeparam name="SO">
	/// The type of stateful being managed, derived from <see cref="IStateful{U, ST}"/>.
	/// </typeparam>
	/// <remarks>
	/// This manager expects a dedicated Unity DI container for workflow, where at least there 
	/// are <see cref="StatePathConfiguration{U, D, S, ST}"/> instances named 
	/// after <see cref="StatePath.CodeName"/> for every <see cref="StatePath"/> in the system.
	/// </remarks>
	public abstract class WorkflowManager<U, BST, D, S, ST, SO> : ConfiguredManager<U, D, S>
		where U : User
		where BST : StateTransition<U>
		where D : IWorkflowUsersDomainContainer<U, BST>
		where S : Session<U, D>
		where ST : BST, new()
		where SO : IStateful<U, ST>
	{
		#region Construction

		/// <summary>
		/// Create.
		/// </summary>
		/// <param name="session">The owning session.</param>
		/// <param name="configurationSectionName">The name of the Unity configuration section dedicated for workflow.</param>
		protected WorkflowManager(S session, string configurationSectionName)
			: base(session, configurationSectionName)
		{
		}

		#endregion

		#region Public properties

		/// <summary>
		/// The states in the system.
		/// </summary>
		public IQueryable<State> States => this.DomainContainer.States;

		/// <summary>
		/// The state groups in the system.
		/// </summary>
		public IQueryable<StateGroup> StateGroups => this.DomainContainer.StateGroups;

		/// <summary>
		/// The workflow graphs in the system.
		/// </summary>
		public IQueryable<WorkflowGraph> WorkflowGraphs => this.DomainContainer.WorkflowGraphs;

		/// <summary>
		/// The state paths in the system.
		/// </summary>
		public IQueryable<StatePath> StatePaths => this.DomainContainer.StatePaths;

		/// <summary>
		/// The state transitions
		/// of type <typeparamref name="ST"/> in the system.
		/// </summary>
		public IQueryable<ST> StateTransitions => this.DomainContainer.StateTransitions.OfType<ST>();

		#endregion

		#region Public methods

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
		/// <exception cref="WorkflowActionValidationException">
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
		public async Task<ST> ExecuteStatePathAsync(
			SO stateful, 
			string pathCodeName, 
			IDictionary<string, object> actionArguments)
		{
			if (stateful == null) throw new ArgumentNullException(nameof(stateful));
			if (pathCodeName == null) throw new ArgumentNullException(nameof(pathCodeName));
			if (actionArguments == null) throw new ArgumentNullException(nameof(actionArguments));

			var path = await FindStatePathAsync(pathCodeName);

			return await ExecuteStatePathAsync(stateful, path, actionArguments);
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
		/// <exception cref="WorkflowActionValidationException">
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
		public async Task<ST> ExecuteStatePathAsync(
			SO stateful,
			long pathID,
			IDictionary<string, object> actionArguments)
		{
			if (stateful == null) throw new ArgumentNullException(nameof(stateful));
			if (actionArguments == null) throw new ArgumentNullException(nameof(actionArguments));

			var path = await FindStatePathAsync(pathID);

			return await ExecuteStatePathAsync(stateful, path, actionArguments);
		}

		/// <summary>
		/// Execute a state path against a stateful instance.
		/// </summary>
		/// <param name="stateful">The stateful instance to execute the transition upon.</param>
		/// <param name="path">The state path.</param>
		/// <param name="actionArguments">A dictinary of arguments to be passed to the path actions.</param>
		/// <returns>Returns the state transition created.</returns>
		/// <exception cref="AccessDeniedDomainException">
		/// Thrown when the session user has no right to execute the state path.
		/// </exception>
		/// <exception cref="WorkflowActionValidationException">
		/// Thrown when the <paramref name="actionArguments"/> are not valid
		/// against the parameter specifications of the path actions.
		/// </exception>
		/// <exception cref="LogicException">
		/// Thrown when the specified path 
		/// is not available for the current state of the stateful object.
		/// </exception>
		public async Task<ST> ExecuteStatePathAsync(
			SO stateful,
			StatePath path,
			IDictionary<string, object> actionArguments)
		{
			if (stateful == null) throw new ArgumentNullException(nameof(stateful));
			if (path == null) throw new ArgumentNullException(nameof(path));
			if (actionArguments == null) throw new ArgumentNullException(nameof(actionArguments));

			if (stateful.State != path.PreviousState)
				throw new LogicException(
					$"The specified path '{path.CodeName}' is not available for the current state of the stateful object.");

			if (!this.AccessResolver.CanExecuteStatePath(this.Session.User, stateful, path))
				throw new AccessDeniedDomainException(
					$"The user with ID {this.Session.User.ID} has no rights " +
					$"to execute path '{path.CodeName}' against the {AccessRight.GetEntityTypeName(stateful)} with ID {stateful.ID}.",
					stateful);

			var validationErrors = ValidateStatePathArguments(path.CodeName, actionArguments);

			if (validationErrors.Count > 0)
				throw new WorkflowActionValidationException(validationErrors);

			ST stateTransition = this.DomainContainer.StateTransitions.Create<ST>();

			var statePathConfiguration = GetStatePathConfiguration(path.CodeName);

			stateTransition.BindToStateful(stateful);

			using (var transaction = this.DomainContainer.BeginTransaction())
			{
				stateTransition.OwningUsers.Add(this.Session.User);

				stateTransition.Path = path;
				stateTransition.ChangeStampBefore = stateful.ChangeStamp;

				await ExecuteActionsAsync(statePathConfiguration.PreActions, stateful, stateTransition, actionArguments);

				stateful.State = path.NextState;

				stateful.ChangeStamp &= path.ChangeStampANDMask;
				stateful.ChangeStamp |= path.ChangeStampORMask;

				stateTransition.ChangeStampAfter = stateful.ChangeStamp;

				await ExecuteActionsAsync(statePathConfiguration.PostActions, stateful, stateTransition, actionArguments);

				await transaction.CommitAsync();
			}

			return stateTransition;
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

			var parameterSpecificationsByKey = new Dictionary<string, ParameterSpecification>();

			foreach (var action in statePathConfiguration.PreActions)
			{
				var actionParameterSpecifications = action.GetParameterSpecifications();

				foreach (var parameterSpecification in actionParameterSpecifications)
				{
					parameterSpecificationsByKey[parameterSpecification.Key] = parameterSpecification;
				}
			}

			return parameterSpecificationsByKey;
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
			var statePath = await FindStatePathAsync(statePathID);

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
					if (value != null)
					{
						if (parameterSpecification.IsRequired)
						{
							AddValidationEntry(validationDictionary, entry.Key, WorkflowManagerMessages.PARAMETER_IS_REQUIRED);
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

			var statePath = await FindStatePathAsync(statePathID);

			return ValidateStatePathArguments(statePath.CodeName, actionArguments);
		}

		/// <summary>
		/// Get the set of all the possible next state paths which can 
		/// be executed on a stateful object.
		/// Use <see cref="FilterAllowedStatePaths(SO, IEnumerable{StatePath})"/>
		/// to narrow the result to the paths which can be executed by the 
		/// current session user.
		/// </summary>
		/// <param name="statefulID">The ID of the stateful object.</param>
		/// <returns>
		/// Returns all the next paths available to the state of a claim,
		/// irrespective of user rights.
		/// Use <see cref="FilterAllowedStatePaths(SO, IEnumerable{StatePath})"/>
		/// to only select the ones allowed by the current session user.
		/// </returns>
		public IQueryable<StatePath> GetNextPaths(long statefulID)
		{
			return from c in this.GetManagedStatefulObjects()
						 where c.ID == statefulID
						 from sp in this.StatePaths
						 where c.State.ID == sp.PreviousStateID
						 select sp;
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
		/// Returns all the next paths available to the state of a claim,
		/// irrespective of user rights.
		/// Use <see cref="FilterAllowedStatePaths(SO, IEnumerable{StatePath})"/>
		/// to only select the ones allowed by the current session user.
		/// </returns>
		public IQueryable<StatePath> GetNextPaths(SO stateful)
		{
			if (stateful == null) throw new ArgumentNullException(nameof(stateful));

			return from sp in this.StatePaths
						 where stateful.State.ID == sp.PreviousStateID
						 select sp;
		}

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
		public IEnumerable<StatePath> FilterAllowedStatePaths(SO stateful, IEnumerable<StatePath> statePaths)
		{
			if (statePaths == null) throw new ArgumentNullException(nameof(statePaths));

			return statePaths
				.Where(sp => this.AccessResolver.CanExecuteStatePath(this.Session.User, stateful, sp));
		}

		/// <summary>
		/// Get the set of state transitions of a stateful object.
		/// </summary>
		/// <param name="statefulID">The ID of the stateful object.</param>
		public IQueryable<ST> GetStateTransitions(long statefulID)
		{
			return from s in GetManagedStatefulObjects()
						 where s.ID == statefulID
						 from st in s.StateTransitions
						 select st;
		}

		/// <summary>
		/// Get the set of state transitions of a stateful object.
		/// </summary>
		/// <param name="stateful">The stateful object.</param>
		public IQueryable<ST> GetStateTransitions(SO stateful)
		{
			if (stateful == null) throw new ArgumentNullException(nameof(stateful));

			return from s in GetManagedStatefulObjects()
						 where s.ID == stateful.ID
						 from st in s.StateTransitions
						 select st;
		}

		/// <summary>
		/// Get the set of state paths leading to a given next state
		/// from the current state of a stateful object.
		/// </summary>
		/// <param name="stateful">The stateful object.</param>
		/// <param name="nextStateCodeName">The code name of the next state.</param>
		public IQueryable<StatePath> GetPathsToState(SO stateful, string nextStateCodeName)
		{
			return from sp in this.StatePaths
						 where sp.PreviousStateID == stateful.State.ID
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
			return from sp in this.StatePaths
						 where sp.PreviousStateID == stateful.State.ID
						 && sp.NextStateID == nextStateID
						 select sp;
		}

		/// <summary>
		/// Get the first <see cref="StatePath"/> which leads to a given state 
		/// and can be executed on a given stateful object.
		/// </summary>
		/// <param name="stateful">The stateful object.</param>
		/// <param name="nextStateCodeName">The code name of the next state.</param>
		/// <returns>Returns the first allowed path or null if no such path exists.</returns>
		public async Task<StatePath> GetAllowedPathToStateAsync(SO stateful, string nextStateCodeName)
		{
			var nextPaths = await
				GetPathsToState(stateful, nextStateCodeName)
				.Include(sp => sp.PreviousState)
				.Include(sp => sp.NextState)
				.Include(sp => sp.WorkflowGraph)
				.ToArrayAsync();

			return nextPaths.FirstOrDefault(
				sp => this.AccessResolver.CanExecuteStatePath(this.Session.User, stateful, sp));
		}

		/// <summary>
		/// Get the first <see cref="StatePath"/> which leads to a given state 
		/// and can be executed on a given stateful object.
		/// </summary>
		/// <param name="stateful">The stateful object.</param>
		/// <param name="nextStateID">The ID of the next state.</param>
		/// <returns>Returns the first allowed path or null if no such path exists.</returns>
		public async Task<StatePath> GetAllowedPathToStateAsync(SO stateful, long nextStateID)
		{
			var nextPaths = await
				GetPathsToState(stateful, nextStateID)
				.Include(sp => sp.PreviousState)
				.Include(sp => sp.NextState)
				.Include(sp => sp.WorkflowGraph)
				.ToArrayAsync();

			return nextPaths.FirstOrDefault(
				sp => this.AccessResolver.CanExecuteStatePath(this.Session.User, stateful, sp));
		}

		#endregion

		#region Protected methods

		/// <summary>
		/// Gets the set of stateful objects which can be managed by this manager.
		/// </summary>
		protected abstract IQueryable<SO> GetManagedStatefulObjects();

		#endregion

		#region Private methods

		/// <summary>
		/// Execute a collection of workflow actions.
		/// </summary>
		/// <param name="actions">The actions to execute.</param>
		/// <param name="stateful">The stateful instance against which the actions are executed.</param>
		/// <param name="stateTransition">The state transition.</param>
		/// <param name="actionArguments">The arguments to the actions.</param>
		/// <returns>Returns a task completing the actions.</returns>
		private async Task ExecuteActionsAsync(
			IEnumerable<IWorkflowAction<U, D, S, ST>> actions, 
			IStateful<U, ST> stateful,
			ST stateTransition, 
			IDictionary<string, object> actionArguments)
		{
			if (actions == null) throw new ArgumentNullException(nameof(actions));

			foreach (var action in actions)
			{
				await action.ExecuteAsync(this.Session, stateful, stateTransition, actionArguments);
			}
		}

		/// <summary>
		/// Get the pre and post actions for a state paths.
		/// </summary>
		/// <param name="pathCodeName">The <see cref="StatePath.CodeName"/> of the path.</param>
		/// <returns>Returns the actions specifications.</returns>
		private StatePathConfiguration<U, D, S, ST> GetStatePathConfiguration(string pathCodeName)
		{
			if (pathCodeName == null) throw new ArgumentNullException(nameof(pathCodeName));

			return this.ManagerDIContainer.Resolve<StatePathConfiguration<U, D, S, ST>>(pathCodeName);
		}

		/// <summary>
		/// Find the <see cref="StatePath"/> having the given code name.
		/// </summary>
		/// <returns>Returns the path found.</returns>
		/// <exception cref="LogicException">
		/// Thrown when no <see cref="StatePath"/> exists having the given code name
		/// or when the <see cref="WorkflowGraph"/> where the path belongs 
		/// works with a different <see cref="WorkflowGraph.StateTransitionTypeName"/>
		/// than <typeparamref name="ST"/>.
		/// </exception>
		private async Task<StatePath> FindStatePathAsync(string pathCodeName)
		{
			var pathQuery = this.DomainContainer.StatePaths
				.Where(sp => sp.CodeName == pathCodeName);

			return await FindStatePathAsync(pathQuery);
		}

		/// <summary>
		/// Find the <see cref="StatePath"/> having the given ID.
		/// </summary>
		/// <returns>Returns the path found.</returns>
		/// <exception cref="LogicException">
		/// Thrown when no <see cref="StatePath"/> exists having the given ID
		/// or when the <see cref="WorkflowGraph"/> where the path belongs 
		/// works with a different <see cref="WorkflowGraph.StateTransitionTypeName"/>
		/// than <typeparamref name="ST"/>.
		/// </exception>
		private async Task<StatePath> FindStatePathAsync(long pathID)
		{
			var pathQuery = this.DomainContainer.StatePaths
				.Where(sp => sp.ID == pathID);

			return await FindStatePathAsync(pathQuery);
		}

		/// <summary>
		/// Find a <see cref="StatePath"/> via a query.
		/// </summary>
		/// <param name="pathQuery">The query to execite.</param>
		/// <returns>Returns the path found.</returns>
		/// <exception cref="LogicException">
		/// Thrown when no <see cref="StatePath"/> exists in the query
		/// or when the <see cref="WorkflowGraph"/> where the path belongs 
		/// works with a different <see cref="WorkflowGraph.StateTransitionTypeName"/>
		/// than <typeparamref name="ST"/>.
		/// </exception>
		private async Task<StatePath> FindStatePathAsync(IQueryable<StatePath> pathQuery)
		{
			if (pathQuery == null) throw new ArgumentNullException(nameof(pathQuery));

			pathQuery = pathQuery
				.Include(sp => sp.PreviousState)
				.Include(sp => sp.NextState)
				.Include(sp => sp.WorkflowGraph);

			var path = await pathQuery.SingleOrDefaultAsync();

			if (path == null)
				throw new LogicException(
					$"The state path does not exist.");

			if (path.WorkflowGraph.StateTransitionTypeName != typeof(ST).FullName)
				throw new LogicException(
					$"The state path must work with transitions of type {path.WorkflowGraph.StateTransitionTypeName}.");

			return path;
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

		#endregion
	}
}
