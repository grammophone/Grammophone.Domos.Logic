using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using Grammophone.Domos.Accounting;
using Grammophone.Domos.DataAccess;
using Grammophone.Domos.Domain;
using Grammophone.Domos.Domain.Accounting;
using Grammophone.Domos.Domain.Workflow;
using Grammophone.Domos.Logic.Models.FundsTransfer;

namespace Grammophone.Domos.Logic
{
	/// <summary>
	/// Base manager for exposrting fund transfer requests and importing
	/// fund transfer responses.
	/// </summary>
	/// <typeparam name="U">The type of the user, derived from <see cref="User"/>.</typeparam>
	/// <typeparam name="BST">
	/// The base type of the system's state transitions, derived fom <see cref="StateTransition{U}"/>.
	/// </typeparam>
	/// <typeparam name="A">The type of accounts, derived from <see cref="Account{U}"/>.</typeparam>
	/// <typeparam name="P">The type of the postings, derived from <see cref="Posting{U, A}"/>.</typeparam>
	/// <typeparam name="R">The type of remittances, derived from <see cref="Remittance{U, A}"/>.</typeparam>
	/// <typeparam name="J">
	/// The type of accounting journals, derived from <see cref="Journal{U, ST, A, P, R}"/>.
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
	public abstract class FundsTransferManager<U, BST, A, P, R, J, D, S, ST, SO> : Manager<U, D, S>
		where U : User
		where BST : StateTransition<U>
		where A : Account<U>
		where P : Posting<U, A>
		where R : Remittance<U, A>
		where J : Journal<U, BST, A, P, R>
		where D : IDomosDomainContainer<U, BST, A, P, R, J>
		where S : Session<U, D>
		where ST : BST, new()
		where SO : IStateful<U, ST>
	{
		#region Private fields

		private AccountingSession<U, BST, A, P, R, J, D> accountingSession;

		private WorkflowManager<U, BST, D, S, ST, SO> workflowManager;

		#endregion

		#region Construction

		/// <summary>
		/// Create.
		/// </summary>
		/// <param name="session">The logic session.</param>
		/// <param name="accountingSession">The accounting session.</param>
		/// <param name="workflowManager">The workflow manager.</param>
		protected FundsTransferManager(
			S session, 
			AccountingSession<U, BST, A, P, R, J, D> accountingSession,
			WorkflowManager<U, BST, D, S, ST, SO> workflowManager) 
			: base(session)
		{
			if (accountingSession == null) throw new ArgumentNullException(nameof(accountingSession));
			if (workflowManager == null) throw new ArgumentNullException(nameof(workflowManager));

			this.accountingSession = accountingSession;
			this.workflowManager = workflowManager;
		}

		#endregion

		#region Public properties

		/// <summary>
		/// The set of credit systems.
		/// </summary>
		public IQueryable<CreditSystem> CreditSystems => this.DomainContainer.CreditSystems;

		#endregion

		#region Public methods

		/// <summary>
		/// Pack a collection of <see cref="FundsTransferRequest"/>s into a batch.
		/// </summary>
		/// <param name="creditSystem">The credit system filtering the requests.</param>
		/// <param name="fundsTransferRequests">The funds transfer requests.</param>
		/// <param name="batchID">The optional ID of the patch.</param>
		/// <returns>
		/// Returns a <see cref="FundsRequestBatch"/> containing the requests
		/// which belong to the given <paramref name="creditSystem"/>.
		/// </returns>
		public async Task<FundsRequestBatch> CreateFundsRequestBatchAsync(
			CreditSystem creditSystem,
			IReadOnlyCollection<FundsTransferRequest> fundsTransferRequests,
			string batchID = null)
		{
			if (creditSystem == null) throw new ArgumentNullException(nameof(creditSystem));
			if (fundsTransferRequests == null) throw new ArgumentNullException(nameof(fundsTransferRequests));

			var filteredRequests = fundsTransferRequests.Where(ftr => ftr.CreditSystemID == creditSystem.ID).ToList();

			return await CreateFundsRequestBatchImplAsync(creditSystem, filteredRequests, batchID);
		}

		/// <summary>
		/// Pack a collection of <see cref="FundsTransferRequest"/>s into a batch.
		/// </summary>
		/// <param name="creditSystem">The credit system filtering the requests.</param>
		/// <param name="fundsTransferRequestsQuery">The set of funds transfer requests.</param>
		/// <param name="batchID">The optional ID of the patch.</param>
		/// <returns>
		/// Returns a <see cref="FundsRequestBatch"/> containing the requests
		/// which belong to the given <paramref name="creditSystem"/>.
		/// </returns>
		public async Task<FundsRequestBatch> CreateFundsRequestBatchAsync(
			CreditSystem creditSystem,
			IQueryable<FundsTransferRequest> fundsTransferRequestsQuery,
			string batchID = null)
		{
			if (creditSystem == null) throw new ArgumentNullException(nameof(creditSystem));
			if (fundsTransferRequestsQuery == null) throw new ArgumentNullException(nameof(fundsTransferRequestsQuery));

			fundsTransferRequestsQuery = fundsTransferRequestsQuery.Where(ftr => ftr.CreditSystemID == creditSystem.ID);

			var fundsTransferRequests = await fundsTransferRequestsQuery.ToListAsync();

			return await CreateFundsRequestBatchImplAsync(creditSystem, fundsTransferRequests, batchID);
		}

		/// <summary>
		/// Get the set of <see cref="FundsTransferRequest"/>s which
		/// have no response yet.
		/// </summary>
		/// <param name="creditSystemID">The ID of the credit system of the transfer requests.</param>
		/// <param name="includeSubmitted">
		/// If true, include the already submitted requests in the results,
		/// else exclude the submitted requests.
		/// </param>
		public IQueryable<FundsTransferRequest> GetPendingFundTransferRequests(
			long creditSystemID,
			bool includeSubmitted = false)
		{
			return accountingSession.FilterPendingFundsTransferRequests(
				creditSystemID,
				this.DomainContainer.FundsTransferRequests,
				includeSubmitted);
		}

		/// <summary>
		/// Get the set of <see cref="FundsTransferRequest"/>s which
		/// have no response yet.
		/// </summary>
		/// <param name="creditSystemID">The ID of the credit system of the transfer requests.</param>
		/// <param name="stateCodeName">
		/// The state code name of the stateful objects corresponding to the requests.
		/// </param>
		/// <param name="includeSubmitted">
		/// If true, include the already submitted requests in the results,
		/// else exclude the submitted requests.
		/// </param>
		public IQueryable<FundsTransferRequest> GetPendingFundsTransferRequests(
			long creditSystemID,
			string stateCodeName,
			bool includeSubmitted = false)
		{
			var query = from so in this.workflowManager.GetManagedStatefulObjects()
									where so.State.CodeName == stateCodeName
									let lastTransition = so.StateTransitions.OrderByDescending(st => st.ID).FirstOrDefault()
									where lastTransition != null //&& lastTransition.Path.NextState.CodeName == stateCodeName
									// Only needed for double-checking.
									let e = lastTransition.FundsTransferEvent
									where e != null
									select e.Request;

			return accountingSession.FilterPendingFundsTransferRequests(creditSystemID, query, includeSubmitted);
		}

		#endregion

		#region Protected methods

		#endregion

		#region Private methods

		private async Task<FundsRequestBatch> CreateFundsRequestBatchImplAsync(
			CreditSystem creditSystem,
			IReadOnlyList<FundsTransferRequest> fundsTransferRequests,
			string batchID = null)
		{
			if (creditSystem == null) throw new ArgumentNullException(nameof(creditSystem));
			if (fundsTransferRequests == null) throw new ArgumentNullException(nameof(fundsTransferRequests));

			var batch = new FundsRequestBatch(fundsTransferRequests.Count);

			batch.CreditSystemCodeName = creditSystem.CodeName;
			batch.BatchID = batchID;
			batch.Date = DateTime.UtcNow;

			using (var transaction = this.DomainContainer.BeginTransaction())
			{
				foreach (var fundsTransferRequest in fundsTransferRequests)
				{
					var batchItem = new FundsRequestBatchItem
					{
						Amount = fundsTransferRequest.Amount,
						TransactionID = fundsTransferRequest.TransactionID,
						BankAccountInfo = fundsTransferRequest.EncryptedBankAccountInfo.Decrypt()
					};

					// Record the submission.
					await accountingSession.AddFundsTransferEventAsync(
						fundsTransferRequest,
						batch.Date,
						FundsTransferEventType.Submitted);

					batch.Items.Add(batchItem);
				}

				await transaction.CommitAsync();
			}

			return batch;
		}

		#endregion
	}
}
