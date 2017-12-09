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
		public virtual IQueryable<CreditSystem> CreditSystems => this.DomainContainer.CreditSystems;

		/// <summary>
		/// The funds transfer request batches in the system.
		/// </summary>
		public virtual IQueryable<FundsTransferRequestBatch> FundsTransferRequestBatches => this.DomainContainer.FundsTransferRequestBatches;

		/// <summary>
		/// The funds transfer event collations in the system.
		/// </summary>
		public virtual IQueryable<FundsTransferEventCollation> FundsTransferEventCollations => this.DomainContainer.FundsTransferEventCollations;

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

		/// <summary>
		/// Accept a funds response file
		/// and execute per line any accounting or workflow associated with this manager.
		/// </summary>
		/// <param name="file">The file to digest.</param>
		/// <returns>
		/// Returns a collection of results describing the execution outcome of the
		/// contents of the <paramref name="file"/>.
		/// </returns>
		public virtual async Task<IReadOnlyCollection<FundsResponseResult>> AcceptResponseFileAsync(
			FundsResponseFile file)
		{
			if (file == null) throw new ArgumentNullException(nameof(file));

			await ValidateCreditSystemAsync(file);

			var collation = await CreateFundsTransferEventCollationAsync(file);

			if (file.Items.Count == 0) return new FundsResponseResult[0];

			return await DigestResponseFileAsync(file);
		}

		#endregion

		#region Protected methods

		/// <summary>
		/// Digest a funds response file.
		/// The existence of the credit system and
		/// the collation specified in <paramref name="file"/> is assumed.
		/// </summary>
		/// <param name="file">The file to digest.</param>
		/// <returns>
		/// Returns a collection of results describing the execution outcome of the
		/// contents of the <paramref name="file"/>.
		/// </returns>
		protected virtual async Task<IReadOnlyCollection<FundsResponseResult>> DigestResponseFileAsync(
			FundsResponseFile file)
		{
			if (file == null) throw new ArgumentNullException(nameof(file));

			string[] transactionIDs = file.Items.Select(i => i.TransactionID).ToArray();

			var results = new List<FundsResponseResult>(file.Items.Count);

			var fundsTransferRequestsQuery = from r in this.FundsTransferRequests
																			 where transactionIDs.Contains(r.TransactionID) && r.BatchID == file.BatchID
																			 select r;

			var requestsByTransactionID = await fundsTransferRequestsQuery.ToDictionaryAsync(r => r.TransactionID);

			if (requestsByTransactionID.Count == 0)
				throw new UserException(FundsTransferManagerMessages.FILE_NOT_APPLICABLE);

			foreach (var item in file.Items)
			{
				FundsResponseResult result = await AcceptResponseItemAsync(file, item, requestsByTransactionID);

				results.Add(result);
			}

			return results;
		}

		/// <summary>
		/// Pack a collection of <see cref="FundsTransferRequest"/>s into a batch.
		/// </summary>
		/// <param name="creditSystem">The credit system filtering the requests.</param>
		/// <param name="fundsTransferRequests">The funds transfer requests.</param>
		/// <param name="batchID">The optional ID of the patch.</param>
		/// <returns>
		/// Returns a <see cref="FundsRequestFile"/> containing the requests
		/// which belong to the given <paramref name="creditSystem"/>.
		/// </returns>
		protected async Task<FundsRequestFile> CreateFundsRequestBatchAsync(
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
		/// Returns a <see cref="FundsRequestFile"/> containing the requests
		/// which belong to the given <paramref name="creditSystem"/>.
		/// </returns>
		protected async Task<FundsRequestFile> CreateFundsRequestBatchAsync(
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

		/// <summary>
		/// Create a collation with ID as set in property <see cref="FundsResponseFile.CollationID"/>
		/// of a <paramref name="file"/>.
		/// </summary>
		/// <param name="file">The funds response file specifying the collation ID.</param>
		/// <returns>Returns the created collation.</returns>
		/// <exception cref="UserException">
		/// Thrown when a collation with the same ID as in <see cref="FundsResponseFile.CollationID"/>
		/// already exists.
		/// </exception>
		private async Task<FundsTransferEventCollation> CreateFundsTransferEventCollationAsync(FundsResponseFile file)
		{
			if (file == null) throw new ArgumentNullException(nameof(file));

			using (var transaction = this.DomainContainer.BeginTransaction())
			{
				bool collationAlreadyExists =
					await this.DomainContainer.FundsTransferEventCollations.AnyAsync(c => c.ID == file.CollationID);

				if (collationAlreadyExists)
					throw new UserException(FundsTransferManagerMessages.COLLATION_ALREADY_EXISTS);

				return await this.AccountingSession.CreateFundsTransferEventCollationAsync(file.CollationID);
			}
		}

		/// <summary>
		/// Validate the credit system implied in a <see cref="FundsResponseFile"/>.
		/// It must be among the ones defined in property <see cref="CreditSystems"/> of this manager.
		/// </summary>
		/// <param name="file"></param>
		/// <returns>
		/// Returns the <see cref="CreditSystem"/>.
		/// </returns>
		/// <exception cref="ArgumentException">
		/// Thrown when the <see cref="FundsResponseFile.CreditSystemCodeName"/> property
		/// of the <paramref name="file"/> is not set.
		/// </exception>
		/// <exception cref="UserException">
		/// Thrown when no credit system exists
		/// in those defined in <see cref="CreditSystems"/> property
		/// with <see cref="CreditSystem.CodeName"/>
		/// equal to the property <see cref="FundsResponseFile.CreditSystemCodeName"/>
		/// of the <paramref name="file"/>.
		/// </exception>
		private async Task ValidateCreditSystemAsync(FundsResponseFile file)
		{
			if (file == null) throw new ArgumentNullException(nameof(file));

			if (String.IsNullOrWhiteSpace(file.CreditSystemCodeName))
				throw new ArgumentException(
					$"The {nameof(file.CreditSystemCodeName)} property of the batch is not set.",
					nameof(file));

			if (!await this.CreditSystems.AnyAsync(cs => cs.CodeName == file.CreditSystemCodeName))
				throw new UserException(FundsTransferManagerMessages.CREDIT_SYSTEM_NOT_AVAILABLE);
		}

		private Task<FundsResponseResult> AcceptResponseItemAsync(
			FundsResponseFile file,
			FundsResponseFileItem item,
			Dictionary<string, FundsTransferRequest> requestsByTransactionID)
		{
			throw new NotImplementedException();
		}

		private async Task<FundsRequestFile> CreateFundsRequestBatchImplAsync(
			CreditSystem creditSystem,
			IReadOnlyList<FundsTransferRequest> fundsTransferRequests,
			Guid? batchID = null)
		{
			if (creditSystem == null) throw new ArgumentNullException(nameof(creditSystem));
			if (fundsTransferRequests == null) throw new ArgumentNullException(nameof(fundsTransferRequests));

			var batch = new FundsRequestFile(
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

					var batchItem = new FundsRequestFileItem
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
