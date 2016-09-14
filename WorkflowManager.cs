using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Grammophone.Caching;
using Grammophone.Domos.DataAccess;
using Grammophone.Domos.Domain;
using Grammophone.Domos.Domain.Workflow;
using Microsoft.Practices.Unity;

namespace Grammophone.Domos.Logic
{
	/// <summary>
	/// Base manager for querying and executing workflow actions.
	/// </summary>
	/// <typeparam name="U">The type of the user, derived from <see cref="User"/>.</typeparam>
	/// <typeparam name="ST">The type of the state transition, derived fom <see cref="StateTransition{U}"/>.</typeparam>
	/// <typeparam name="D">The type of domain container, derived from <see cref="IWorkflowUsersDomainContainer{U, ST}"/>.</typeparam>
	/// <typeparam name="S">The type of session, derived from <see cref="Session{U, D}"/>.</typeparam>
	/// <remarks>
	/// This manager expects a dedicated Unity DI container for workflow, where at least there 
	/// are <see cref="StatePathConfiguration{U, ST, D, S}"/> instances named 
	/// after <see cref="StatePath.Code"/> for every <see cref="StatePath"/> in the system.
	/// </remarks>
	public abstract class WorkflowManager<U, ST, D, S> : Manager<U, D, S>
		where U : User
		where ST : StateTransition<U>
		where D : IWorkflowUsersDomainContainer<U, ST>
		where S : Session<U, D>
	{
		#region Constants

		/// <summary>
		/// The size of <see cref="workflowDIContainersCache"/>.
		/// </summary>
		private const int WorkflowDIContainersCacheSize = 128;

		#endregion

		#region Private fields

		/// <summary>
		/// Cache of DI conainers by configuration section names.
		/// </summary>
		private static MRUCache<string, IUnityContainer> workflowDIContainersCache;

		#endregion

		#region Construction

		/// <summary>
		/// Static initialization.
		/// </summary>
		static WorkflowManager()
		{
			workflowDIContainersCache = new MRUCache<string, IUnityContainer>(
				configurationSectionName => Session<U, D>.CreateDIContainer(configurationSectionName), 
				WorkflowDIContainersCacheSize);
		}

		/// <summary>
		/// Create.
		/// </summary>
		/// <param name="configurationSectionName">The name of the Unity configuration section dedicated for workflow.</param>
		/// <param name="session">The owning session.</param>
		protected WorkflowManager(string configurationSectionName, S session)
			: base(session)
		{
			if (configurationSectionName == null) throw new ArgumentNullException(nameof(configurationSectionName));

			this.WorkflowDIContainer = workflowDIContainersCache.Get(configurationSectionName);
		}

		#endregion

		#region Protected properties

		/// <summary>
		/// The Unity configuration section dedicated for workflow.
		/// </summary>
		protected IUnityContainer WorkflowDIContainer { get; private set; }

		#endregion

		#region Public methods

		/// <summary>
		/// Execute a state path against a stateful instance.
		/// </summary>
		/// <typeparam name="T">The type of state transition, derived from <typeparamref name="ST"/>.</typeparam>
		/// <param name="stateful">The stateful instance to execute the transition upon.</param>
		/// <param name="pathCodeName">The <see cref="StatePath.Code"/> of the state path.</param>
		/// <param name="actionArguments">A dictinary of arguments to be passed to the path actions.</param>
		/// <returns>Returns the state transition created.</returns>
		public async Task<T> ExecuteStatePathAsync<T>(
			IStateful<U> stateful, 
			string pathCodeName, 
			IDictionary<string, object> actionArguments)
			where T : ST, new()
		{
			if (stateful == null) throw new ArgumentNullException(nameof(stateful));
			if (pathCodeName == null) throw new ArgumentNullException(nameof(pathCodeName));
			if (actionArguments == null) throw new ArgumentNullException(nameof(actionArguments));

			var path = await this.DomainContainer.StatePaths
				.Include(sp => sp.ToState)
				.FirstOrDefaultAsync(sp => sp.Code == pathCodeName);

			if (path == null)
				throw new LogicException($"The state path with code '{pathCodeName}' does not exist.");

			if (stateful.State != path.FromState)
				throw new LogicException(
					$"The specified path '{pathCodeName}' is not available for the current state of the stateful object.");

			T stateTransition = this.DomainContainer.Create<T>();

			var statePathConfiguration = GetStatePathConfiguration(pathCodeName);

			stateTransition.BindToStateful(stateful);

			using (var transaction = this.DomainContainer.BeginTransaction())
			{
				stateTransition.OwningUsers.Add(this.Session.User);

				stateTransition.Path = path;
				stateTransition.ChangeStampBefore = stateful.ChangeStamp;

				await ExecuteActions(statePathConfiguration.PreActions, stateful, stateTransition, actionArguments);

				stateful.State = path.ToState;

				stateful.ChangeStamp &= path.ChangeStampANDMask;
				stateful.ChangeStamp |= path.ChangeStampORMask;

				stateTransition.ChangeStampAfter = stateful.ChangeStamp;

				await ExecuteActions(statePathConfiguration.PostActions, stateful, stateTransition, actionArguments);

				await transaction.CommitAsync();
			}

			return stateTransition;
		}

		#endregion

		#region Private methods

		/// <summary>
		/// Execute a collection of workflow actions.
		/// </summary>
		/// <param name="actions">The actions to execute.</param>
		/// <param name="stateful">The stateful instance against which the actions are executed.</param>
		/// <param name="stateTransition">The state transition.</param>
		/// <param name="actionArguments">The arguments to the actions.</param>
		/// <returns></returns>
		private async Task ExecuteActions(
			IEnumerable<IWorkflowAction<U, ST, D, S>> actions, 
			IStateful<U> stateful,
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
		/// <param name="pathCodeName">The <see cref="StatePath.Code"/> of the path.</param>
		/// <returns>Returns the actions specifications.</returns>
		private StatePathConfiguration<U, ST, D, S> GetStatePathConfiguration(string pathCodeName)
		{
			if (pathCodeName == null) throw new ArgumentNullException(nameof(pathCodeName));

			return this.WorkflowDIContainer.Resolve<StatePathConfiguration<U, ST, D, S>>(pathCodeName);
		}

		#endregion
	}
}
