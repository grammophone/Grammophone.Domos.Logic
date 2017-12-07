﻿using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using Grammophone.Caching;
using Grammophone.Domos.Accounting;
using Grammophone.Domos.DataAccess;
using Grammophone.Domos.Domain;
using Grammophone.Domos.Domain.Accounting;
using Grammophone.Domos.Domain.Workflow;
using Grammophone.Domos.Logic.Models.FundsTransfer;
using Grammophone.Domos.Logic.Models.Workflow;

namespace Grammophone.Domos.Logic
{
	/// <summary>
	/// Base manager for exposrting fund transfer requests and importing
	/// fund transfer responses bound to a workflow.
	/// </summary>
	/// <typeparam name="U">The type of the user, derived from <see cref="User"/>.</typeparam>
	/// <typeparam name="BST">
	/// The base type of the system's state transitions, derived fom <see cref="StateTransition{U}"/>.
	/// </typeparam>
	/// <typeparam name="P">The type of the postings, derived from <see cref="Posting{U}"/>.</typeparam>
	/// <typeparam name="R">The type of remittances, derived from <see cref="Remittance{U}"/>.</typeparam>
	/// <typeparam name="J">
	/// The type of accounting journals, derived from <see cref="Journal{U, ST, P, R}"/>.
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
	/// <typeparam name="AS">
	/// The type of accounting session, derived from <see cref="AccountingSession{U, BST, P, R, J, D}"/>.
	/// </typeparam>
	/// <typeparam name="WM">
	/// The type of the associated workflow manager,
	/// implementing <see cref="IWorkflowManager{U, ST, SO}"/>.
	/// Any descendant class from <see cref="WorkflowManager{U, BST, D, S, ST, SO, C}"/> works.
	/// </typeparam>
	public abstract class WorkflowFundsTransferManager<U, BST, P, R, J, D, S, ST, SO, AS, WM>
		: FundsTransferManager<U, BST, P, R, J, D, S, AS>
		where U : User
		where BST : StateTransition<U>
		where P : Posting<U>
		where R : Remittance<U>
		where J : Journal<U, BST, P, R>
		where D : IDomosDomainContainer<U, BST, P, R, J>
		where S : LogicSession<U, D>
		where ST : BST, new()
		where SO : IStateful<U, ST>
		where WM : IWorkflowManager<U, ST, SO>
		where AS : AccountingSession<U, BST, P, R, J, D>
	{
		#region Private fields

		private AsyncSequentialMRUCache<string, StatePath> statePathsByCodeNameCache;

		#endregion

		#region Construction

		/// <summary>
		/// Create.
		/// </summary>
		/// <param name="session">The logic session.</param>
		/// <param name="accountingSession">The associated accounting session.</param>
		/// <param name="workflowManager">The associated workflow manager.</param>
		protected WorkflowFundsTransferManager(
			S session,
			AS accountingSession,
			WM workflowManager)
			: base(session, accountingSession)
		{
			if (workflowManager == null) throw new ArgumentNullException(nameof(workflowManager));

			this.WorkflowManager = workflowManager;

			this.statePathsByCodeNameCache =
				new AsyncSequentialMRUCache<string, StatePath>(LoadStatePathAsync);
		}

		#endregion

		#region Public properties

		/// <summary>
		/// The funds transfer requests handled by this manager.
		/// </summary>
		/// <remarks>
		/// These are implied by the state transitions of the objects returned
		/// by <see cref="IWorkflowManager{U, ST, SO}.GetManagedStatefulObjects"/>.
		/// </remarks>
		public override IQueryable<FundsTransferRequest> FundsTransferRequests
		{
			get
			{
				var requestIDs = from so in this.WorkflowManager.GetManagedStatefulObjects()
												 from st in so.StateTransitions
												 where st.FundsTransferEvent != null
												 select st.FundsTransferEvent.RequestID;

				return from r in this.DomainContainer.FundsTransferRequests
							 where requestIDs.Contains(r.ID)
							 select r;
			}
		}

		/// <summary>
		/// The funds transfer events handled by this manager.
		/// </summary>
		/// <remarks>
		/// These are implied by the state transitions of the objects returned
		/// by <see cref="IWorkflowManager{U, ST, SO}.GetManagedStatefulObjects"/>.
		/// </remarks>
		public override IQueryable<FundsTransferEvent> FundsTransferEvents => from so in this.WorkflowManager.GetManagedStatefulObjects()
																																					from st in so.StateTransitions
																																					where st.FundsTransferEvent != null
																																					select st.FundsTransferEvent;

		#endregion

		#region Protected properties

		/// <summary>
		/// The associated workflow manager.
		/// </summary>
		protected WM WorkflowManager { get; private set; }

		#endregion

		#region Public methods

		/// <summary>
		/// Get the set of <see cref="FundsTransferRequest"/>s which
		/// have no response yet.
		/// </summary>
		/// <param name="creditSystem">The credit system of the transfer requests.</param>
		/// <param name="stateCodeName">
		/// The state code name of the stateful objects corresponding to the requests.
		/// </param>
		/// <param name="includeSubmitted">
		/// If true, include the already submitted requests in the results,
		/// else exclude the submitted requests.
		/// </param>
		public IQueryable<FundsTransferRequest> GetPendingFundsTransferRequests(
			CreditSystem creditSystem,
			string stateCodeName,
			bool includeSubmitted = false)
		{
			if (creditSystem == null) throw new ArgumentNullException(nameof(creditSystem));
			if (stateCodeName == null) throw new ArgumentNullException(nameof(stateCodeName));

			return GetPendingFundsTransferRequests(
				creditSystem,
				so => so.State.CodeName == stateCodeName,
				includeSubmitted);
		}

		/// <summary>
		/// Get the set of <see cref="FundsTransferRequest"/>s which
		/// have no response yet.
		/// </summary>
		/// <param name="creditSystem">The credit system of the transfer requests.</param>
		/// <param name="statefulObjectPredicate">
		/// Criterion for selecting the related stateful objects involved in the funds transfers.
		/// </param>
		/// <param name="includeSubmitted">
		/// If true, include the already submitted requests in the results,
		/// else exclude the submitted requests.
		/// </param>
		public IQueryable<FundsTransferRequest> GetPendingFundsTransferRequests(
			CreditSystem creditSystem,
			Expression<Func<SO, bool>> statefulObjectPredicate,
			bool includeSubmitted = false)
		{
			if (creditSystem == null) throw new ArgumentNullException(nameof(creditSystem));
			if (statefulObjectPredicate == null) throw new ArgumentNullException(nameof(statefulObjectPredicate));

			var query = from so in this.WorkflowManager.GetManagedStatefulObjects().Where(statefulObjectPredicate)
									let lastTransition = so.StateTransitions.OrderByDescending(st => st.CreationDate).FirstOrDefault()
									where lastTransition != null //&& lastTransition.Path.NextState.CodeName == stateCodeName
																							 // Only needed for double-checking.
									let e = lastTransition.FundsTransferEvent
									where e != null
									select e.Request;

			return this.AccountingSession.FilterPendingFundsTransferRequests(creditSystem, query, includeSubmitted);
		}

		/// <summary>
		/// Accepts a funds transfer response batch from a credit system
		/// and execute the appropriate state paths 
		/// asp specified by the <see cref="GetNextStatePathCodeName(string, FundsResponseFileItem)"/> method
		/// on the corresponding stateful objects.
		/// </summary>
		/// <param name="batch">The response batch to accept.</param>
		/// <returns>
		/// Returns a collection of results describing the execution outcome of the
		/// contents of the <paramref name="batch"/>.
		/// </returns>
		/// <remarks>
		/// Each action in paths being executed will receive an arguments dictionary
		/// containing at least a key 'batchItem' with value
		/// of type <see cref="FundsResponseFileItem"/>.
		/// </remarks>
		public async Task<IReadOnlyCollection<FundsResponseResult<SO, ST>>> AcceptFundsTransferResponseBatchAsync(
			FundsResponseFile batch)
		{
			if (batch == null) throw new ArgumentNullException(nameof(batch));

			if (String.IsNullOrWhiteSpace(batch.CreditSystemCodeName))
				throw new ArgumentException(
					$"The {nameof(batch.CreditSystemCodeName)} property of the batch is not set.",
					nameof(batch));

			var creditSystem = 
				await this.DomainContainer.CreditSystems.SingleAsync(cs => cs.CodeName == batch.CreditSystemCodeName);

			var date = batch.Date;

			string[] transactionIDs = batch.Items.Select(i => i.TransactionID).ToArray();

			var statefulObjectsQuery = from so in this.WorkflowManager.GetManagedStatefulObjects()
																 from st in so.StateTransitions
																 where st.FundsTransferEvent != null
																 let ftr = st.FundsTransferEvent.Request
																 where transactionIDs.Contains(ftr.TransactionID)
																 select new
																 {
																	 StatefulObject = so,
																	 State = so.State, // Force including the State property.
																	 TransactionID = ftr.TransactionID
																 };

			var statefulObjectsByTransactionID = await statefulObjectsQuery
				.ToDictionaryAsync(r => r.TransactionID, r => r.StatefulObject);

			var responseResults = new List<FundsResponseResult<SO, ST>>(batch.Items.Count);

			foreach (var item in batch.Items)
			{
				var fundsResponseResult = 
					await AcceptFundsTransferResponseItemAsync(batch, statefulObjectsByTransactionID, item);

				responseResults.Add(fundsResponseResult);
			}

			return responseResults;
		}

		#endregion

		#region Protected methods

		/// <summary>
		/// Decide the state path to execute on a stateful object
		/// when a <see cref="FundsResponseFileItem"/> arrives for it.
		/// </summary>
		/// <param name="stateCodeName">The code name of the current state.</param>
		/// <param name="fundsResponseBatchItem">The batch line arriving for the stateful object.</param>
		/// <returns>Returns the code name of the path to execute or null to execute none.</returns>
		protected abstract string GetNextStatePathCodeName(
			string stateCodeName,
			FundsResponseFileItem fundsResponseBatchItem);

		#endregion

		#region Private methods

		/// <summary>
		/// Supports the cache miss of <see cref="statePathsByCodeNameCache"/>.
		/// </summary>
		private async Task<StatePath> LoadStatePathAsync(string statePathCodeName)
			=> await this.DomainContainer.StatePaths
			.Include(sp => sp.NextState)
			.Include(sp => sp.PreviousState)
			.Include(sp => sp.WorkflowGraph)
			.SingleAsync(sp => sp.CodeName == statePathCodeName);

		private async Task<FundsResponseResult<SO, ST>> AcceptFundsTransferResponseItemAsync(
			FundsResponseFile file,
			Dictionary<string, SO> statefulObjectsByTransactionID,
			FundsResponseFileItem item)
		{
			if (statefulObjectsByTransactionID == null) throw new ArgumentNullException(nameof(statefulObjectsByTransactionID));
			if (item == null) throw new ArgumentNullException(nameof(item));

			var actionArguments = new Dictionary<string, object>
			{
				[StandardArgumentKeys.BillingItem] = item
			};

			var fundsResponseResult = new FundsResponseResult<SO, ST>
			{
				BatchItem = item,
				ExecutionResult = new ExecutionResult<SO, ST>()
			};

			try
			{
				if (statefulObjectsByTransactionID.TryGetValue(item.TransactionID, out SO statefulObject))
				{
					fundsResponseResult.ExecutionResult.StatefulObject = statefulObject;

					string currentStateCodeName = statefulObject.State.CodeName;

					string nextStatePathCodeName = GetNextStatePathCodeName(currentStateCodeName, item);

					if (nextStatePathCodeName == null)
					{
						fundsResponseResult.ExecutionResult.Exception =
							new LogicException(
								$"No next path is defined from state '{currentStateCodeName}' when batch item response type is '{item.Status}'.");
					}
					else
					{
						var statePath = await statePathsByCodeNameCache.Get(nextStatePathCodeName);

						var transition = await WorkflowManager.ExecuteStatePathAsync(
							statefulObject,
							statePath,
							actionArguments);

						fundsResponseResult.ExecutionResult.StateTransition = transition;
					}
				}
				else
				{
					fundsResponseResult.ExecutionResult.Exception =
						new LogicException($"No stateful object is associated with transaction ID '{item.TransactionID}'");
				}
			}
			catch (Exception ex)
			{
				fundsResponseResult.ExecutionResult.Exception = ex;
			}

			return fundsResponseResult;
		}

		#endregion
	}
}
