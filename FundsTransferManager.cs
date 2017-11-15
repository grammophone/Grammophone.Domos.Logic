using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
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
	/// <typeparam name="U">
	/// The type of the user, derived from <see cref="User"/>.
	/// </typeparam>
	/// <typeparam name="BST">
	/// The base type of the system's state transitions, derived fom <see cref="StateTransition{U}"/>.
	/// </typeparam>
	/// <typeparam name="P">
	/// The type of the postings, derived from <see cref="Posting{U}"/>.
	/// </typeparam>
	/// <typeparam name="R">
	/// The type of remittances, derived from <see cref="Remittance{U}"/>.
	/// </typeparam>
	/// <typeparam name="J">
	/// The type of accounting journals, derived from <see cref="Journal{U, ST, P, R}"/>.
	/// </typeparam>
	/// <typeparam name="D">
	/// The type of domain container, derived from <see cref="IWorkflowUsersDomainContainer{U, ST}"/>.
	/// </typeparam>
	/// <typeparam name="S">
	/// The type of session, derived from <see cref="LogicSession{U, D}"/>.
	/// </typeparam>
	/// <typeparam name="AS">
	/// The type of accounting session, derived from <see cref="AccountingSession{U, BST, P, R, J, D}"/>.
	/// </typeparam>
	public abstract class FundsTransferManager<U, BST, P, R, J, D, S, AS> : Manager<U, D, S>
		where U : User
		where BST : StateTransition<U>
		where P : Posting<U>
		where R : Remittance<U>
		where J : Journal<U, BST, P, R>
		where D : IDomosDomainContainer<U, BST, P, R, J>
		where S : LogicSession<U, D>
		where AS : AccountingSession<U, BST, P, R, J, D>
	{
		#region Construction

		/// <summary>
		/// Create.
		/// </summary>
		/// <param name="session">The logic session.</param>
		/// <param name="accountingSession">The accounting session.</param>
		protected FundsTransferManager(S session, AS accountingSession) : base(session)
		{
			if (accountingSession == null) throw new ArgumentNullException(nameof(accountingSession));

			this.AccountingSession = accountingSession;
		}

		#endregion

		#region Public properties

		/// <summary>
		/// The set of credit systems.
		/// </summary>
		public IQueryable<CreditSystem> CreditSystems => this.DomainContainer.CreditSystems;

		/// <summary>
		/// The funds transfer requests handled by this manager.
		/// </summary>
		public abstract IQueryable<FundsTransferRequest> FundsTransferRequests { get; }

		/// <summary>
		/// The funds transfer events handled by this manager.
		/// </summary>
		public virtual IQueryable<FundsTransferEvent> FundsTransferEvents => from e in this.DomainContainer.FundsTransferEvents
																																				 where this.FundsTransferRequests.Any(r => r.ID == e.RequestID)
																																				 select e;

		#endregion

		#region Protected properties

		/// <summary>
		/// The accounting session being used.
		/// </summary>
		protected AS AccountingSession { get; }

		#endregion

		#region Public methods

		/// <summary>
		/// Get the set of <see cref="FundsTransferRequest"/>s which
		/// have no response yet.
		/// </summary>
		/// <param name="creditSystem">The credit system of the transfer requests.</param>
		/// <param name="includeSubmitted">
		/// If true, include the already submitted requests in the results,
		/// else exclude the submitted requests.
		/// </param>
		public IQueryable<FundsTransferRequest> GetPendingFundsTransferRequests(
			CreditSystem creditSystem,
			bool includeSubmitted = false)
		{
			if (creditSystem == null) throw new ArgumentNullException(nameof(creditSystem));

			return AccountingSession.FilterPendingFundsTransferRequests(
				creditSystem,
				this.FundsTransferRequests,
				includeSubmitted);
		}

		#endregion

		#region Protected methods

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
		protected async Task<FundsRequestBatch> CreateFundsRequestBatchAsync(
			CreditSystem creditSystem,
			IReadOnlyCollection<FundsTransferRequest> fundsTransferRequests,
			Guid? batchID = null)
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
		protected async Task<FundsRequestBatch> CreateFundsRequestBatchAsync(
			CreditSystem creditSystem,
			IQueryable<FundsTransferRequest> fundsTransferRequestsQuery,
			Guid? batchID = null)
		{
			if (creditSystem == null) throw new ArgumentNullException(nameof(creditSystem));
			if (fundsTransferRequestsQuery == null) throw new ArgumentNullException(nameof(fundsTransferRequestsQuery));

			fundsTransferRequestsQuery = fundsTransferRequestsQuery.Where(ftr => ftr.CreditSystemID == creditSystem.ID);

			var fundsTransferRequests = await fundsTransferRequestsQuery.ToListAsync();

			return await CreateFundsRequestBatchImplAsync(creditSystem, fundsTransferRequests, batchID);
		}

		#endregion

		#region Private methods

		private async Task<FundsRequestBatch> CreateFundsRequestBatchImplAsync(
			CreditSystem creditSystem,
			IReadOnlyList<FundsTransferRequest> fundsTransferRequests,
			Guid? batchID = null)
		{
			if (creditSystem == null) throw new ArgumentNullException(nameof(creditSystem));
			if (fundsTransferRequests == null) throw new ArgumentNullException(nameof(fundsTransferRequests));

			var batch = new FundsRequestBatch(
				creditSystem.CodeName,
				DateTime.UtcNow,
				fundsTransferRequests.Count,
				batchID);

			using (var transaction = this.DomainContainer.BeginTransaction())
			{
				foreach (var fundsTransferRequest in fundsTransferRequests)
				{
					if (fundsTransferRequest.BatchID != batchID)
						fundsTransferRequest.BatchID = batchID;

					var batchItem = new FundsRequestBatchItem
					{
						Amount = fundsTransferRequest.Amount,
						TransactionID = fundsTransferRequest.TransactionID,
						BankAccountInfo = fundsTransferRequest.EncryptedBankAccountInfo.Decrypt()
					};

					// Record the submission.
					await AccountingSession.AddFundsTransferEventAsync(
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
