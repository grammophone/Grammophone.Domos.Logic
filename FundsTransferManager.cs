﻿using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;
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

		/// <summary>
		/// Static initialization.
		/// </summary>
		static FundsTransferManager()
		{
			lazyResponseSchemaSet = new Lazy<XmlSchemaSet>(CreateResponseSchemaSet);

			lazyRequestSchemaSet = new Lazy<XmlSchemaSet>(CreateRequestSchemaSet);

			lazyResponseFileSerializer = new Lazy<XmlSerializer>(() => new XmlSerializer(typeof(FundsResponseFile)));

			lazyRequestFileSerializer = new Lazy<XmlSerializer>(() => new XmlSerializer(typeof(FundsRequestFile)));
		}

		#endregion

		#region Private fields

		private static Lazy<XmlSchemaSet> lazyResponseSchemaSet;

		private static Lazy<XmlSchemaSet> lazyRequestSchemaSet;

		private static Lazy<XmlSerializer> lazyResponseFileSerializer;

		private static Lazy<XmlSerializer> lazyRequestFileSerializer;

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
		/// Get the total statistics of funds transfers.
		/// </summary>
		public async Task<FundsTransferStatistic> GetTotalStatisticAsync()
		{
			var pendingBatches = FilterBatches(m => m.Type == FundsTransferBatchMessageType.Pending);
			var submittedBatches = FilterBatches(m => m.Type == FundsTransferBatchMessageType.Submitted);
			var rejectedBatches = FilterBatches(m => m.Type == FundsTransferBatchMessageType.Rejected);
			var acceptedBatches = FilterBatches(m => m.Type == FundsTransferBatchMessageType.Accepted);
			var respondedBatches = FilterBatches(m => m.Type == FundsTransferBatchMessageType.Responded);

			var query = from r in this.UnbatchedFundsTransferRequests
									group r by 1 into g
									select new FundsTransferStatistic
									{
										UnbatchedRequestsCount = g.Count(),
										PendingBatchesCount = pendingBatches.Count(),
										SubmittedBatchesCount = submittedBatches.Count(),
										RejectedBatchesCount = rejectedBatches.Count(),
										AcceptedBatchesCount = acceptedBatches.Count(),
										RespondedBatchesCount = respondedBatches.Count()
									};

			return await query.FirstOrDefaultAsync() ?? new FundsTransferStatistic();
		}

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
		/// Accept a <see cref="FundsResponseFile"/> in XML format
		/// and execute per line any accounting or workflow associated with this manager.
		/// </summary>
		/// <param name="stream">The stream containing the XML represenation of the file to digest.</param>
		/// <returns>
		/// Returns a collection of results describing the execution outcome of the
		/// contents of the file.
		/// </returns>
		public async Task<IReadOnlyCollection<FundsResponseResult>> AcceptResponseFileAsync(System.IO.Stream stream)
			=> await AcceptResponseFileAsync(ReadResponseFile(stream));

		/// <summary>
		/// Rread a <see cref="FundsResponseFile"/> from a stream containing its XML representation.
		/// </summary>
		/// <param name="stream">The input stream.</param>
		/// <returns>Returns the response file.</returns>
		/// <exception cref="XmlSchemaValidationException">
		/// Thrown when the XML contents are not according the the schema
		/// for a <see cref="FundsResponseFile"/>.
		/// </exception>
		public FundsResponseFile ReadResponseFile(System.IO.Stream stream)
		{
			if (stream == null) throw new ArgumentNullException(nameof(stream));

			var responseSchemaSet = GetResponseSchemaSet();

			var xmlReaderSettings = new XmlReaderSettings
			{
				Schemas = responseSchemaSet,
				ValidationType = ValidationType.Schema,
			};

			using (var xmlReader = XmlReader.Create(stream, xmlReaderSettings))
			{
				var serializer = GetResponseFileSerializer();

				return (FundsResponseFile)serializer.Deserialize(xmlReader);
			}
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
		/// Get the XML document representing a <see cref="FundsRequestFile"/>.
		/// </summary>
		/// <param name="requestFile">The request file object.</param>
		/// <returns>Returns the XML document.</returns>
		public XmlDocument GetRequestFileXML(FundsRequestFile requestFile)
		{
			if (requestFile == null) throw new ArgumentNullException(nameof(requestFile));

			XmlDocument document = new XmlDocument();

			using (var xmlWriter = document.CreateNavigator().AppendChild())
			{
				WriteRequestFile(xmlWriter, requestFile);
			}

			return document;
		}

		/// <summary>
		/// Writes a <see cref="FundsRequestFile"/> into a stream as XML.
		/// </summary>
		/// <param name="stream">The stream to write to.</param>
		/// <param name="requestFile">The request file to write.</param>
		public void WriteRequestFile(System.IO.Stream stream, FundsRequestFile requestFile)
		{
			if (stream == null) throw new ArgumentNullException(nameof(stream));
			if (requestFile == null) throw new ArgumentNullException(nameof(requestFile));

			using (var xmlWriter = XmlWriter.Create(stream))
			{
				WriteRequestFile(xmlWriter, requestFile);
			}
		}

		/// <summary>
		/// Write a <see cref="FundsRequestFile"/> into an XML writer.
		/// </summary>
		/// <param name="xmlWriter">Thee XML writer.</param>
		/// <param name="requestFile">The request file to write.</param>
		public void WriteRequestFile(XmlWriter xmlWriter, FundsRequestFile requestFile)
		{
			if (xmlWriter == null) throw new ArgumentNullException(nameof(xmlWriter));
			if (requestFile == null) throw new ArgumentNullException(nameof(requestFile));

			var serializer = GetRequestFileSerializer();

			serializer.Serialize(xmlWriter, requestFile);
		}

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

		private static XmlSerializer GetResponseFileSerializer()
			=> lazyResponseFileSerializer.Value;

		private static XmlSerializer GetRequestFileSerializer()
			=> lazyRequestFileSerializer.Value;

		private XmlSchemaSet GetResponseSchemaSet()
			=> lazyResponseSchemaSet.Value;

		private XmlSchemaSet GetRequestSchemaSet()
			=> lazyRequestSchemaSet.Value;

		private static XmlSchemaSet CreateResponseSchemaSet()
			=> CreateSchemaSet("Grammophone.Domos.Logic.Models.Fundsransfer.FundsRequestFile.xsd");

		private static XmlSchemaSet CreateRequestSchemaSet()
			=> CreateSchemaSet("Grammophone.Domos.Logic.Models.Fundsransfer.FundsResponseFile.xsd");

		private static XmlSchemaSet CreateSchemaSet(string xsdResourceName)
		{
			if (xsdResourceName == null) throw new ArgumentNullException(nameof(xsdResourceName));

			var schemaSet = new XmlSchemaSet();

			var currentAssembly = System.Reflection.Assembly.GetExecutingAssembly();

			using (var stream = currentAssembly.GetManifestResourceStream(xsdResourceName))
			{
				var schema = XmlSchema.Read(stream, (_, validationArgs) =>
				{
					if (validationArgs.Severity == XmlSeverityType.Error)
						throw new XmlSchemaException(validationArgs.Message);
				});

				schemaSet.Add(schema);
			}

			return schemaSet;
		}

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
