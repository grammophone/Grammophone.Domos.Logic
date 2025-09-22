using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using Grammophone.Caching;
using Grammophone.DataAccess;
using Grammophone.Domos.DataAccess;
using Grammophone.Domos.Domain;
using Grammophone.Domos.Domain.Workflow;

namespace Grammophone.Domos.Logic.WorkflowActions
{
	/// <summary>
	/// Base for <see cref="IWorkflowAction{U, D, S, ST, SO}"/> implementations
	/// with additional access rights elevation methods.
	/// </summary>
	/// <typeparam name="U">The type of the user, derived from <see cref="User"/>.</typeparam>
	/// <typeparam name="BST">The base class for the state transitions in the system.</typeparam>
	/// <typeparam name="D">The type of domain container, derived from <see cref="IWorkflowUsersDomainContainer{U, ST}"/>.</typeparam>
	/// <typeparam name="S">The type of session, derived from <see cref="LogicSession{U, D}"/>.</typeparam>
	/// <typeparam name="ST">The type of state transition, derived from <typeparamref name="BST"/>.</typeparam>
	/// <typeparam name="SO">The type of stateful object, derived from <see cref="IStateful{U, ST}"/>.</typeparam>
	public abstract class WorkflowAction<U, BST, D, S, ST, SO> : IWorkflowAction<U, D, S, ST, SO>
		where U : User
		where BST : StateTransition<U>
		where D : class, IWorkflowUsersDomainContainer<U, BST>
		where S : LogicSession<U, D>
		where ST : BST
		where SO : IStateful<U, ST>
	{
		#region Private fields

		private static readonly AsyncWeakRepositoryCache<D, string, StatePath> statePathsByCodeNameCache;

		private IReadOnlyDictionary<string, ParameterSpecification> parameterSpecificationsByKey;

		#endregion

		#region Construction

		static WorkflowAction()
		{
			statePathsByCodeNameCache = new AsyncWeakRepositoryCache<D, string, StatePath>(LoadStatePathAsync, false, 4096);
		}

		#endregion

		#region Public properties

		/// <summary>
		/// Dictionary of <see cref="ParameterSpecification"/>s
		/// defined by <see cref="GetParameterSpecifications"/>,
		/// indexed by their <see cref="ParameterSpecification.Key"/>
		/// </summary>
		public IReadOnlyDictionary<string, ParameterSpecification> ParameterSpecificationsByKey
		{
			get
			{
				if (parameterSpecificationsByKey == null)
				{
					parameterSpecificationsByKey = GetParameterSpecifications().ToDictionary(s => s.Key);
				}

				return parameterSpecificationsByKey;
			}
		}

		#endregion

		#region Public methods

		/// <summary>
		/// Execute the action.
		/// </summary>
		/// <param name="session">The session.</param>
		/// <param name="domainContainer">The domain container used by the session.</param>
		/// <param name="stateful">The stateful instance to execute upon.</param>
		/// <param name="stateTransition">The state transition being executed.</param>
		/// <param name="actionArguments">The arguments to the action.</param>
		/// <returns>Returns a task completing the operation.</returns>
		/// <param name="context"></param>
		public abstract Task ExecuteAsync(
			S session,
			D domainContainer,
			SO stateful,
			ST stateTransition,
			IDictionary<string, object> actionArguments,
			IDictionary<string, object> context);

		/// <summary>
		/// Get the specifications of parameters expected in the
		/// parameters dictionary used 
		/// by <see cref="ExecuteAsync(S, D, SO, ST, IDictionary{string, object}, IDictionary{string, object})"/>
		/// method.
		/// </summary>
		/// <returns>Returns a collection of parameter specifications.</returns>
		public abstract IEnumerable<ParameterSpecification> GetParameterSpecifications();

		#endregion

		#region Protected methods

		/// <summary>
		/// Get a scope of elevated access, taking care of nesting.
		/// Please ensure that <see cref="ElevatedAccessScope.Dispose"/> is called in all cases.
		/// </summary>
		/// <param name="session">The session whose rights are elevated.</param>
		/// <remarks>
		/// Until all nested of elevated access scopes are disposed,
		/// no security checks are performed by the session.
		/// </remarks>
		protected static ElevatedAccessScope GetElevatedAccessScope(S session) 
			=> session.GetElevatedAccessScope();

		/// <summary>
		/// Elevate access to all entities for the duration of a <paramref name="transaction"/>,
		/// taking care of any nesting.
		/// </summary>
		/// <param name="session">The session whose rights are elevated.</param>
		/// <param name="transaction">The transaction.</param>
		/// <remarks>
		/// This is suitable for domain containers having <see cref="TransactionMode.Deferred"/>,
		/// where the saving takes place at the topmost transaction.
		/// The <see cref="GetElevatedAccessScope"/> method for elevating access rights might 
		/// restore them too soon.
		/// </remarks>
		protected static void ElevateTransactionAccessRights(S session, ITransaction transaction)
			=> session.ElevateTransactionAccessRights(transaction);

		/// <summary>
		/// Create an impersonation scope. Call <see cref="ImpersonationScope{U}.Dispose"/> to restore the original user.
		/// </summary>
		/// <param name="session">The session for user impersonation.</param>
		/// <param name="impersonatedUser">The user to impersonate.</param>
		/// <returns>Returns a scope for impersonating a user until <see cref="ImpersonationScope{U}.Dispose"/> is called to restore the original user.</returns>
		protected ImpersonationScope<U> GetImpoersonationScope(S session, U impersonatedUser)
			=> session.GetImpersonationScope(impersonatedUser);

		/// <summary>
		/// Create an impersonation scope. Call <see cref="ImpersonationScope{U}.Dispose"/> to restore the original user.
		/// </summary>
		/// <param name="session">The session for user impersonation.</param>
		/// <param name="userQuery">A query to uniquely identify the impersonated user.</param>
		/// <returns>Returns a scope for impersonating a user until <see cref="ImpersonationScope{U}.Dispose"/> is called to restore the original user.</returns>
		/// <exception cref="InvalidOperationException">Thrown if the <paramref name="userQuery"/> does not uniquely identity a user.</exception>
		protected internal Task<ImpersonationScope<U>> GetImpersonationScopeAsync(S session, IQueryable<U> userQuery)
			=> session.GetImpersonationScopeAsync(userQuery);

		/// <summary>
		/// Create an impersonation scope. Call <see cref="ImpersonationScope{U}.Dispose"/> to restore the original user.
		/// </summary>
		/// <param name="session">The session for user impersonation.</param>
		/// <param name="userPickPredicate">A predicate to uniquely identify the impersonated user.</param>
		/// <returns>Returns a scope for impersonating a user until <see cref="ImpersonationScope{U}.Dispose"/> is called to restore the original user.</returns>
		/// <exception cref="InvalidOperationException">Thrown if the <paramref name="userPickPredicate"/> does not uniquely identity a user.</exception>
		protected Task<ImpersonationScope<U>> GetImpersonationScopeAsync(S session, Expression<Func<U, bool>> userPickPredicate)
			=> session.GetImpersonationScopeAsync(userPickPredicate);

		/// <summary>
		/// Get the value of a parameter inside the action arguments.
		/// </summary>
		/// <typeparam name="T">The type of the value.</typeparam>
		/// <param name="actionArguments">The action arguments.</param>
		/// <param name="parameterKey">The key of the parameter.</param>
		/// <param name="isRequired">If true, treat the parameter as a required one.</param>
		/// <returns>
		/// Returns the value found in <paramref name="actionArguments"/> or
		/// the default value of <typeparamref name="T"/> if <paramref name="isRequired"/> is false.
		/// </returns>
		/// <exception cref="ArgumentException">
		/// Thrown when the parameter is not found in <paramref name="actionArguments"/>
		/// and the <paramref name="isRequired"/> is true.
		/// </exception>
		protected T GetParameterObject<T>(IDictionary<string, object> actionArguments, string parameterKey, bool isRequired)
			where T : class
		{
			if (actionArguments == null) throw new ArgumentNullException(nameof(actionArguments));
			if (parameterKey == null) throw new ArgumentNullException(nameof(parameterKey));

			if (actionArguments.TryGetValue(parameterKey, out object valueObject))
			{
				return (T)valueObject;
			}
			else
			{
				if (isRequired)
					throw new ArgumentException(
						$"The required parameter '{parameterKey}' does not exist in the action arguments.",
						nameof(actionArguments));

				return default;
			}
		}

		/// <summary>
		/// Attempt to get the value of a parameter inside the action arguments.
		/// </summary>
		/// <typeparam name="T">The type of the value, which must be a reference type.</typeparam>
		/// <param name="actionArguments">The action arguments.</param>
		/// <param name="parameterKey">The key of the parameter.</param>
		/// <returns>
		/// Returns the value found in <paramref name="actionArguments"/> or
		/// null if the parameter is optional and not found.
		/// </returns>
		/// <exception cref="ArgumentException">
		/// Thrown when the parameter is not defined by <see cref="GetParameterSpecifications"/> or
		/// when not found in <paramref name="actionArguments"/>
		/// while specified as required.
		/// </exception>
		protected T GetParameterValue<T>(IDictionary<string, object> actionArguments, string parameterKey)
		{
			if (actionArguments == null) throw new ArgumentNullException(nameof(actionArguments));
			if (parameterKey == null) throw new ArgumentNullException(nameof(parameterKey));

			var parameterSpecification = GetParameterSpecification(parameterKey);

			if (actionArguments.TryGetValue(parameterKey, out object valueObject))
			{
				return (T)valueObject;
			}
			else
			{
				if (parameterSpecification.IsRequired)
					throw new ArgumentException(
						$"The required parameter '{parameterKey}' does not exist in the action arguments.",
						nameof(actionArguments));

				return default;
			}
		}

		/// <summary>
		/// Attempt to get the value of a parameter inside the action arguments.
		/// </summary>
		/// <typeparam name="V">The type of the value, which must be a value type.</typeparam>
		/// <param name="actionArguments">The action arguments.</param>
		/// <param name="parameterKey">The key of the parameter.</param>
		/// <returns>
		/// Returns the value found in <paramref name="actionArguments"/> or
		/// null if the parameter is optional and not found.
		/// </returns>
		/// <exception cref="ArgumentException">
		/// Thrown when the parameter is not defined by <see cref="GetParameterSpecifications"/> or
		/// when not found in <paramref name="actionArguments"/>
		/// while specified as required.
		/// </exception>
		protected V? GetOptionalParameterValue<V>(IDictionary<string, object> actionArguments, string parameterKey)
			where V : struct
		{
			if (actionArguments == null) throw new ArgumentNullException(nameof(actionArguments));
			if (parameterKey == null) throw new ArgumentNullException(nameof(parameterKey));

			var parameterSpecification = GetParameterSpecification(parameterKey);

			if (actionArguments.TryGetValue(parameterKey, out object valueObject))
			{
				return (V?)valueObject;
			}
			else
			{
				if (parameterSpecification.IsRequired)
					throw new ArgumentException(
						$"The required parameter '{parameterKey}' does not exist in the action arguments.",
						nameof(actionArguments));

				return null;
			}
		}

		/// <summary>
		/// Get a state path by code name efficiently.
		/// </summary>
		/// <param name="domainContainer">The domain container to load the state path from.</param>
		/// <param name="statePathCodeName">The <see cref="StatePath.CodeName"/> of the state path.</param>
		/// <returns>Returns the state path found.</returns>
		/// <exception cref="InvalidOperationException">
		/// Thrown if no state path with the given <paramref name="statePathCodeName"/> exists in <paramref name="domainContainer"/>.
		/// </exception>
		protected async Task<StatePath> GetStatePathAsync(D domainContainer, string statePathCodeName)
			=> await statePathsByCodeNameCache.Get(domainContainer, statePathCodeName);

		/// <summary>
		/// Follow a state path for a stateful object, without executing the configured actions for the state path.
		/// </summary>
		/// <param name="domainContainer">The domain container.</param>
		/// <param name="stateful">The stateful object to follow the state path on.</param>
		/// <param name="statePath">The state path to be followed.</param>
		/// <returns>The created state transition of type <typeparamref name="ST"/>.</returns>
		/// <exception cref="UserException">
		/// Thrown when the currrent state of the <paramref name="stateful"/> is incompatible with the <paramref name="statePath"/>.
		/// </exception>
		protected ST FollowStatePath(D domainContainer, SO stateful, StatePath statePath)
		{
			if (domainContainer == null) throw new ArgumentNullException(nameof(domainContainer));
			if (stateful == null) throw new ArgumentNullException(nameof(stateful));
			if (statePath == null) throw new ArgumentNullException(nameof(statePath));

			if (stateful.State != statePath.PreviousState)
			{
				throw new UserException(String.Format(WorkflowManagerMessages.INCOMPATIBLE_STATE_PATH, statePath.Name, stateful.State.Name, stateful.ID));
			}

			var now = DateTime.UtcNow;

			ST stateTransition = domainContainer.Create<ST>();

			stateTransition.BindToStateful(stateful);

			stateTransition.Path = statePath;
			stateTransition.ChangeStampBefore = stateful.ChangeStamp;

			stateful.LastStateChangeDate = now;

			if (statePath.PreviousState.GroupID != statePath.NextState.GroupID)
			{
				stateful.LastStateGroupChangeDate = now;
			}

			stateful.State = statePath.NextState;

			stateful.ChangeStamp &= statePath.ChangeStampANDMask;
			stateful.ChangeStamp |= statePath.ChangeStampORMask;

			stateTransition.ChangeStampAfter = stateful.ChangeStamp;

			return stateTransition;
		}

		/// <summary>
		/// Follow a state path for a stateful object, without executing the configured actions for the state path.
		/// </summary>
		/// <param name="domainContainer">The domain container.</param>
		/// <param name="stateful">The stateful object to follow the state path on.</param>
		/// <param name="statePathCodeName">The <see cref="StatePath.CodeName"/> of the state path.</param>
		/// <returns>The created state transition of type <typeparamref name="ST"/>.</returns>
		/// <exception cref="UserException">
		/// Thrown when the currrent state of the <paramref name="stateful"/> is incompatible with the specified state path.
		/// </exception>
		protected async Task<ST> FollowStatePathAsync(D domainContainer, SO stateful, string statePathCodeName)
		{
			if (domainContainer == null) throw new ArgumentNullException(nameof(domainContainer));
			if (stateful == null) throw new ArgumentNullException(nameof(stateful));
			if (statePathCodeName == null) throw new ArgumentNullException(nameof(statePathCodeName));

			var statePath = await GetStatePathAsync(domainContainer, statePathCodeName);

			return FollowStatePath(domainContainer, stateful, statePath);
		}

		#endregion

		#region Private methods

		private ParameterSpecification GetParameterSpecification(string parameterKey)
		{
			ParameterSpecification parameterSpecification;

			if (!this.ParameterSpecificationsByKey.TryGetValue(parameterKey, out parameterSpecification))
			{
				throw new ArgumentException($"The parameter with key '{parameterKey}' does not exist in the defined parameters.");
			}

			return parameterSpecification;
		}

		private static async Task<StatePath> LoadStatePathAsync(D domainContainer, string statePathCodeName)
		{
			if (domainContainer == null) throw new ArgumentNullException(nameof(domainContainer));
			if (statePathCodeName == null) throw new ArgumentNullException(nameof(statePathCodeName));

			return await
				domainContainer.StatePaths
				.Include(sp => sp.PreviousState.Group.WorkflowGraph)
				.Include(sp => sp.NextState.Group.WorkflowGraph)
				.SingleAsync(sp => sp.CodeName == statePathCodeName);
		}

		#endregion
	}
}
