using System;
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
		/// <param name="stateCodeName">
		/// The state code name of the stateful objects corresponding to the requests.
		/// </param>
		/// <param name="includeSubmitted">
		/// If true, include the already submitted requests in the results,
		/// else exclude the submitted requests.
		/// </param>
		public IQueryable<FundsTransferRequest> GetPendingFundsTransferRequests(
			string stateCodeName,
			bool includeSubmitted = false)
		{
			if (stateCodeName == null) throw new ArgumentNullException(nameof(stateCodeName));

			return GetPendingFundsTransferRequests(
				so => so.State.CodeName == stateCodeName,
				includeSubmitted);
		}

		/// <summary>
		/// Get the set of <see cref="FundsTransferRequest"/>s which
		/// have no response yet.
		/// </summary>
		/// <param name="statefulObjectPredicate">
		/// Criterion for selecting the related stateful objects involved in the funds transfers.
		/// </param>
		/// <param name="includeSubmitted">
		/// If true, include the already submitted requests in the results,
		/// else exclude the submitted requests.
		/// </param>
		public IQueryable<FundsTransferRequest> GetPendingFundsTransferRequests(
			Expression<Func<SO, bool>> statefulObjectPredicate,
			bool includeSubmitted = false)
		{
			if (statefulObjectPredicate == null) throw new ArgumentNullException(nameof(statefulObjectPredicate));

			var query = from so in this.WorkflowManager.GetManagedStatefulObjects().Where(statefulObjectPredicate)
									let lastTransition = so.StateTransitions.OrderByDescending(st => st.CreationDate).FirstOrDefault()
									where lastTransition != null //&& lastTransition.Path.NextState.CodeName == stateCodeName
																							 // Only needed for double-checking.
									let e = lastTransition.FundsTransferEvent
									where e != null
									select e.Request;

			return this.AccountingSession.FilterPendingFundsTransferRequests(query, includeSubmitted);
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

		/// <summary>
		/// Digest a funds transfer response file from a credit system
		/// and execute the appropriate state paths 
		/// as specified by the <see cref="GetNextStatePathCodeName(string, FundsResponseFileItem)"/> method
		/// on the corresponding stateful objects.
		/// The existence of the credit system and
		/// the collation specified in <paramref name="file"/> is assumed.
		/// </summary>
		/// <param name="file">The file to digest.</param>
		/// <param name="responseBatchMessage">The batch message where the generated funds transfer events will be assigned.</param>
		/// <returns>
		/// Returns a collection of results describing the execution outcome of the
		/// contents of the <paramref name="file"/>.
		/// </returns>
		/// <remarks>
		/// Each action in paths being executed will receive an arguments dictionary
		/// containing at least the key <see cref="StandardArgumentKeys.BillingItem"/> set with value
		/// of type <see cref="FundsResponseLine"/>.
		/// </remarks>
		protected override async Task<IReadOnlyCollection<FundsResponseResult>> DigestResponseFileAsync(
			FundsResponseFile file,
			FundsTransferBatchMessage responseBatchMessage)
		{
			if (file == null) throw new ArgumentNullException(nameof(file));

			var responseResults = new List<FundsResponseResult>(file.Items.Count);

			long[] requestIDs = file.Items.Select(i => i.RequestID).ToArray();

			var statefulObjectsQuery = from so in this.WorkflowManager.GetManagedStatefulObjects()
																 from st in so.StateTransitions
																 where st.FundsTransferEvent != null
																 let ftr = st.FundsTransferEvent.Request
																 where requestIDs.Contains(ftr.ID)
																 select new
																 {
																	 StatefulObject = so,
																	 so.State, // Force including the State property.
																	 RequestID = ftr.ID
																 };

			var statefulObjectsByRequestID = await statefulObjectsQuery
				.ToDictionaryAsync(r => r.RequestID, r => r.StatefulObject);

			if (statefulObjectsByRequestID.Count == 0)
				throw new UserException(FundsTransferManagerMessages.FILE_NOT_APPLICABLE);

			foreach (var item in file.Items)
			{
				var fundsResponseResult =
					await AcceptResponseItemAsync(file, item, statefulObjectsByRequestID);

				responseResults.Add(fundsResponseResult);
			}

			return responseResults;
		}

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

		private async Task<FundsResponseResult> AcceptResponseItemAsync(
			FundsResponseFile file,
			FundsResponseFileItem item,
			Dictionary<long, SO> statefulObjectsByRequestID)
		{
			if (statefulObjectsByRequestID == null) throw new ArgumentNullException(nameof(statefulObjectsByRequestID));
			if (item == null) throw new ArgumentNullException(nameof(item));

			var line = new FundsResponseLine(file, item);

			var actionArguments = new Dictionary<string, object>
			{
				[StandardArgumentKeys.BillingItem] = line
			};

			var fundsResponseResult = new FundsResponseResult
			{
				FileItem = item
			};

			Exception exception = null;

			try
			{
				if (statefulObjectsByRequestID.TryGetValue(item.RequestID, out SO statefulObject))
				{
					string currentStateCodeName = statefulObject.State.CodeName;

					string nextStatePathCodeName = GetNextStatePathCodeName(currentStateCodeName, item);

					if (nextStatePathCodeName == null)
					{
						exception = new LogicException(
							$"No next path is defined from state '{currentStateCodeName}' when batch item response type is '{item.Status}'.");
					}
					else
					{
						var statePath = await statePathsByCodeNameCache.Get(nextStatePathCodeName);

						var transition = await WorkflowManager.ExecuteStatePathAsync(
							statefulObject,
							statePath,
							actionArguments);

						fundsResponseResult.Event = transition.FundsTransferEvent;
					}
				}
				else
				{
					exception =
						new LogicException($"No stateful object is associated with request ID '{item.RequestID}'.");
				}
			}
			catch (Exception ex)
			{
				exception = ex;
			}

			if (exception != null)
			{
				var fundsTransferRequest = await 
					this.FundsTransferRequests.SingleOrDefaultAsync(r => r.ID == item.RequestID);

				if (fundsTransferRequest == null)
					throw new LogicException($"No funds transfer request is found having ID '{item.RequestID}'.");

				var errorActionResult = await this.AccountingSession.AddFundsTransferEventAsync(
					fundsTransferRequest,
					file.Time,
					FundsTransferEventType.WorkflowFailed,
					batchMessageID: file.BatchMessageID,
					exception: exception);

				fundsResponseResult.Event = errorActionResult.FundsTransferEvent;
			}

			return fundsResponseResult;
		}

		#endregion
	}
}
