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
		/// The set of credit systems handled by the manager.
		/// </summary>
		/// <remarks>
		/// The default implementation yields all credit systems in the system.
		/// </remarks>
		public virtual IQueryable<CreditSystem> CreditSystems => this.DomainContainer.CreditSystems;

		/// <summary>
		/// The funds transfer batches handled by this manager.
		/// </summary>
		/// <remarks>
		/// The default implementation is implied from the <see cref="FundsTransferRequests"/> property.
		/// </remarks>
		public virtual IQueryable<FundsTransferBatch> FundsTransferBatches => from b in this.DomainContainer.FundsTransferBatches
																																					where this.FundsTransferRequests.Any(r => r.BatchID == b.ID)
																																					select b;

		/// <summary>
		/// The funds transfer batch messages collations in the system.
		/// </summary>
		/// <remarks>
		/// The default implementation is implied from the <see cref="FundsTransferBatches"/> property.
		/// </remarks>
		public virtual IQueryable<FundsTransferBatchMessage> FundsTransferBatchMessages => from m in this.DomainContainer.FundsTransferBatchMessages
																																											 where this.FundsTransferBatches.Any(b => m.BatchID == b.ID)
																																											 select m;

		/// <summary>
		/// The funds transfer requests handled by this manager.
		/// </summary>
		public abstract IQueryable<FundsTransferRequest> FundsTransferRequests { get; }

		/// <summary>
		/// The funds transfer requests handled by this manager which are not enrolled in a batch.
		/// </summary>
		/// <remarks>
		/// The default implementation is implied from the <see cref="FundsTransferBatches"/> property.
		/// </remarks>
		public virtual IQueryable<FundsTransferRequest> UnbatchedFundsTransferRequests => from r in this.FundsTransferRequests
																																											let latestEvent = r.Events.OrderByDescending(e => e.Time).FirstOrDefault()
																																											where latestEvent.Type == FundsTransferEventType.Pending && r.Batch == null
																																											select r;


		/// <summary>
		/// The funds transfer events handled by this manager.
		/// </summary>
		/// <remarks>
		/// The default implementation is implied from the <see cref="FundsTransferRequests"/> property.
		/// </remarks>
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
		/// From a set of <see cref="FundsTransferRequests"/>, filter those which
		/// have no response yet.
		/// </summary>
		/// <param name="includeSubmitted">
		/// If true, include the already submitted requests in the results,
		/// else exclude the submitted requests.
		/// </param>
		public IQueryable<FundsTransferRequest> GetPendingRequests(
			bool includeSubmitted = false)
		{
			return AccountingSession.FilterPendingFundsTransferRequests(
				this.FundsTransferRequests,
				includeSubmitted);
		}

		/// <summary>
		/// From the set of <see cref="FundsTransferRequests"/>, filter those whose
		/// last event matches a predicate.
		/// </summary>
		/// <param name="latestEventPredicate">The predicate to apply to the last event of each request.</param>
		/// <returns>Returns the set of filtered requests.</returns>
		public IQueryable<FundsTransferRequest> FilterRequests(Expression<Func<FundsTransferEvent, bool>> latestEventPredicate)
			=> this.AccountingSession.FilterFundsTransferRequestsByLatestEvent(this.FundsTransferRequests, latestEventPredicate);

		/// <summary>
		/// From the set of <see cref="FundsTransferBatches"/>, filter those whose
		/// last message matches a predicate.
		/// </summary>
		/// <param name="latestMessagePredicate">The predicate to apply to the last message of each batch.</param>
		/// <returns>Returns the set of filtered batches.</returns>
		public IQueryable<FundsTransferBatch> FilterBatches(Expression<Func<FundsTransferBatchMessage, bool>> latestMessagePredicate)
			=> this.AccountingSession.FilterFundsTransferBatchesByLatestMessage(this.FundsTransferBatches, latestMessagePredicate);

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

			if (file.Items.Count == 0) return new FundsResponseResult[0];

			var firstItem = file.Items.First();

			var batchQuery = from r in this.FundsTransferRequests
											 where r.ID == firstItem.RequestID
											 select r.Batch;

			var batch = await batchQuery.Include(b => b.Messages).SingleAsync();

			long[] requestIDs = file.Items.Select(i => i.RequestID).ToArray();

			bool allRequestsAreInTheSameBatch = await
				this.FundsTransferRequests.Where(r => requestIDs.Contains(r.ID)).AllAsync(r => r.BatchID == batch.ID);

			if (!allRequestsAreInTheSameBatch)
			{
				throw new UserException(FundsTransferManagerMessages.REQUESTS_NOT_IN_SAME_BATCH);
			}

			var responseBatchMessage = await this.AccountingSession.AddFundsTransferBatchMessageAsync(
				batch,
				FundsTransferBatchMessageType.Responded,
				file.Time,
				file.BatchMessageID);

			return await DigestResponseFileAsync(file, responseBatchMessage);
		}

		/// <summary>
		/// Enroll a set of funds transfer requests into a new <see cref="FundsTransferBatch"/>.
		/// The requests must not be already under an existing batch.
		/// </summary>
		/// <param name="creditSystemCodeName">The code name of the one among <see cref="CreditSystems"/> to be assigned to the batch.</param>
		/// <param name="requests">The set of dunds transfer requests.</param>
		/// <returns>Returns the pending message of the created batch, where the requests are attached.</returns>
		/// <exception cref="AccountingException">
		/// Thrown when at least one request is already assigned to a batch.
		/// </exception>
		/// <exception cref="UserException">
		/// When the <paramref name="creditSystemCodeName"/> does not refer to a credit
		/// system among <see cref="CreditSystems"/>.
		/// </exception>
		public async Task<FundsTransferBatchMessage> EnrollRequestsIntoBatchAsync(string creditSystemCodeName, IQueryable<FundsTransferRequest> requests)
		{
			if (creditSystemCodeName == null) throw new ArgumentNullException(nameof(creditSystemCodeName));
			if (requests == null) throw new ArgumentNullException(nameof(requests));

			var creditSystem = await GetCreditSystemAsync(creditSystemCodeName);

			return await this.AccountingSession.EnrollRequestsIntoBatchAsync(creditSystem, requests);
		}

		/// <summary>
		/// Enroll a set of funds transfer requests into a new <see cref="FundsTransferBatch"/>.
		/// The requests must not be already under an existing batch.
		/// </summary>
		/// <param name="creditSystemID">The ID of the one among <see cref="CreditSystems"/> to be assigned to the batch.</param>
		/// <param name="requests">The set of dunds transfer requests.</param>
		/// <returns>Returns the pending message of the created batch, where the requests are attached.</returns>
		/// <exception cref="AccountingException">
		/// Thrown when at least one request is already assigned to a batch.
		/// </exception>
		/// <exception cref="UserException">
		/// When the <paramref name="creditSystemID"/> does not refer to a credit
		/// system among <see cref="CreditSystems"/>.
		/// </exception>
		public async Task<FundsTransferBatchMessage> EnrollRequestsIntoBatchAsync(long creditSystemID, IQueryable<FundsTransferRequest> requests)
		{
			if (requests == null) throw new ArgumentNullException(nameof(requests));

			var creditSystem = await GetCreditSystemAsync(creditSystemID);

			return await this.AccountingSession.EnrollRequestsIntoBatchAsync(creditSystem, requests);
		}

		/// <summary>
		/// Create a funds request file for a batch.
		/// </summary>
		/// <param name="pendingBatchMessage">The 'pending' message for the batch.</param>
		/// <returns>Returns the funds request file.</returns>
		/// <remarks>
		/// For best performance, eager fetch the Batch.CreditSystem and Events.Request
		/// relationships of the <paramref name="pendingBatchMessage"/>.
		/// </remarks>
		public FundsRequestFile ExportRequestFile(FundsTransferBatchMessage pendingBatchMessage)
			=> new FundsRequestFile(pendingBatchMessage);

		/// <summary>
		/// Create a funds request file for a batch.
		/// </summary>
		/// <param name="batchID">The ID of the batch.</param>
		/// <returns>Returns the funds request file.</returns>
		public async Task<FundsRequestFile> ExportRequestFile(Guid batchID)
			=> await FundsRequestFile.CreateAsync(batchID, this.FundsTransferBatchMessages);

		/// <summary>
		/// Get the one among <see cref="CreditSystems"/>
		/// having a specified <see cref="CreditSystem.CodeName"/>.
		/// </summary>
		/// <param name="creditSystemCodeName">The code name of the credit system.</param>
		/// <returns>
		/// Returns the <see cref="CreditSystem"/> found.
		/// </returns>
		/// <exception cref="UserException">
		/// When the <paramref name="creditSystemCodeName"/> does not refer to a credit
		/// system among <see cref="CreditSystems"/>.
		/// </exception>
		public async Task<CreditSystem> GetCreditSystemAsync(string creditSystemCodeName)
		{
			var creditSystem = await this.CreditSystems.SingleOrDefaultAsync(cs => cs.CodeName == creditSystemCodeName);

			if (creditSystem == null)
				throw new UserException(FundsTransferManagerMessages.CREDIT_SYSTEM_NOT_AVAILABLE);

			return creditSystem;
		}

		/// <summary>
		/// Get the one among <see cref="CreditSystems"/>
		/// having a specified <see cref="CreditSystem.CodeName"/>.
		/// </summary>
		/// <param name="creditSystemID">The ID of the credit system.</param>
		/// <returns>
		/// Returns the <see cref="CreditSystem"/> found.
		/// </returns>
		/// <exception cref="UserException">
		/// When the <paramref name="creditSystemID"/> does not refer to a credit
		/// system among <see cref="CreditSystems"/>.
		/// </exception>
		public async Task<CreditSystem> GetCreditSystemAsync(long creditSystemID)
		{
			var creditSystem = await this.CreditSystems.SingleOrDefaultAsync(cs => cs.ID == creditSystemID);

			if (creditSystem == null)
				throw new UserException(FundsTransferManagerMessages.CREDIT_SYSTEM_NOT_AVAILABLE);

			return creditSystem;
		}

		#endregion

		#region Protected methods

		/// <summary>
		/// Digest a funds response file.
		/// The existence of the credit system and
		/// the collation specified in <paramref name="file"/> is assumed.
		/// </summary>
		/// <param name="file">The file to digest.</param>
		/// <param name="responseBatchMessage">The batch message where the generated funds transfer events will be assigned.</param>
		/// <returns>
		/// Returns a collection of results describing the execution outcome of the
		/// contents of the <paramref name="file"/>.
		/// </returns>
		protected virtual async Task<IReadOnlyCollection<FundsResponseResult>> DigestResponseFileAsync(
			FundsResponseFile file,
			FundsTransferBatchMessage responseBatchMessage)
		{
			if (file == null) throw new ArgumentNullException(nameof(file));
			if (responseBatchMessage == null) throw new ArgumentNullException(nameof(responseBatchMessage));

			long[] requestIDs = file.Items.Select(i => i.RequestID).ToArray();

			var results = new List<FundsResponseResult>(file.Items.Count);

			var fundsTransferRequestsQuery = from r in this.FundsTransferRequests.Include(r => r.Batch.CreditSystem)
																			 where requestIDs.Contains(r.ID)
																			 select r;

			var requestsByID = await fundsTransferRequestsQuery.ToDictionaryAsync(r => r.ID);

			if (requestsByID.Count == 0)
				throw new UserException(FundsTransferManagerMessages.FILE_NOT_APPLICABLE);

			foreach (var item in file.Items)
			{
				FundsResponseResult result =
					await AcceptResponseItemAsync(file, item, requestsByID[item.RequestID], responseBatchMessage);

				results.Add(result);
			}

			return results;
		}

		/// <summary>
		/// Translate the <see cref="FundsResponseFileItem.Status"/> of a file item onti a <see cref="FundsTransferEventType"/>.
		/// </summary>
		/// <param name="fileItem">The funds transfer file item.</param>
		/// <returns>Returns the corresponding <see cref="FundsTransferEventType"/>.</returns>
		/// <exception cref="LogicException">
		/// Thrown when the conversion is not possible.
		/// </exception>
		protected static FundsTransferEventType GetEventTypeFromResponseFileItem(FundsResponseFileItem fileItem)
		{
			if (fileItem == null) throw new ArgumentNullException(nameof(fileItem));

			FundsTransferEventType eventType;

			switch (fileItem.Status)
			{
				case FundsResponseStatus.Failed:
					eventType = FundsTransferEventType.Failed;
					break;

				case FundsResponseStatus.Accepted:
					eventType = FundsTransferEventType.Accepted;
					break;

				case FundsResponseStatus.Succeeded:
					eventType = FundsTransferEventType.Succeeded;
					break;

				default:
					throw new LogicException($"Unexpected item status '{fileItem.Status}' for request with ID {fileItem.RequestID}.");
			}

			return eventType;
		}

		#endregion

		#region Private methods

		private async Task<FundsResponseResult> AcceptResponseItemAsync(
			FundsResponseFile file,
			FundsResponseFileItem item,
			FundsTransferRequest request,
			FundsTransferBatchMessage responseBatchMessage)
		{
			if (file == null) throw new ArgumentNullException(nameof(file));
			if (item == null) throw new ArgumentNullException(nameof(item));
			if (request == null) throw new ArgumentNullException(nameof(request));
			if (responseBatchMessage == null) throw new ArgumentNullException(nameof(responseBatchMessage));

			using (var transaction = this.DomainContainer.BeginTransaction())
			{
				FundsTransferEventType eventType = GetEventTypeFromResponseFileItem(item);

				var actionResult = await this.AccountingSession.AddFundsTransferEventAsync(
					request,
					DateTime.UtcNow,
					eventType,
					batchMessageID: responseBatchMessage.ID,
					responseCode: item.ResponseCode,
					comments: item.Comments,
					traceCode: item.TraceCode);

				var transferEvent = actionResult.FundsTransferEvent;

				transferEvent.BatchMessage = responseBatchMessage;

				await transaction.CommitAsync();

				return new FundsResponseResult
				{
					Event = transferEvent,
					FileItem = item
				};
			}
		}

		#endregion
	}
}
