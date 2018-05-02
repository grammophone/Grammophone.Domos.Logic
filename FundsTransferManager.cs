using System;
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
		#region Private fields

		private static Lazy<XmlSchemaSet> lazyResponseSchemaSet;

		private static Lazy<XmlSchemaSet> lazyRequestSchemaSet;

		private static Lazy<XmlSerializer> lazyResponseFileSerializer;

		private static Lazy<XmlSerializer> lazyRequestFileSerializer;

		private Func<D, U, AS> accountingSessionFactory;

		#endregion

		#region Construction

		/// <summary>
		/// Create.
		/// </summary>
		/// <param name="session">The logic session.</param>
		/// <param name="accountingSessionFactory">A factory for creating an accounting session.</param>
		protected FundsTransferManager(S session, Func<D, U, AS> accountingSessionFactory)
			: base(session)
		{
			if (accountingSessionFactory == null) throw new ArgumentNullException(nameof(accountingSessionFactory));

			this.accountingSessionFactory = accountingSessionFactory;
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

		/// <summary>
		/// The set of remittances associated with the <see cref="FundsTransferEvents"/>
		/// handled by this manager.
		/// </summary>
		public virtual IQueryable<R> Remittances => from r in this.DomainContainer.Remittances
																								where this.FundsTransferEvents.Any(e => e.ID == r.FundsTransferEventID)
																								select r;

		/// <summary>
		/// The set of journals associated with the <see cref="FundsTransferEvents"/>
		/// handled by this manager.
		/// </summary>
		public virtual IQueryable<J> Journals => from j in this.DomainContainer.Journals
																						 where this.FundsTransferEvents.Any(e => e.ID == j.FundsTransferEventID)
																						 select j;

		#endregion

		#region Public methods

		#region Funds transfer file conversions

		/// <summary>
		/// Get the funds transfer file converter registered under a name.
		/// </summary>
		/// <param name="converterName">The name under which the converter is registered.</param>
		public IFundsTransferFileConverter GetFundsTransferFileConverter(string converterName)
		{
			var converter = this.SessionSettings.Resolve<IFundsTransferFileConverter>(converterName);

			if (converter == null)
				throw new ArgumentException($"The converter name '{converterName}' does not correspond to a register funds transfer file converter.");

			return converter;
		}

		/// <summary>
		/// Get the funds transfer file converter associated with a credit system or null if no converter
		/// is associated.
		/// </summary>
		/// <param name="creditSystem">The credit system.</param>
		/// <returns>Returns the converter associated with the credit system or null.</returns>
		public IFundsTransferFileConverter GetFundsTransferFileConverter(CreditSystem creditSystem)
		{
			if (creditSystem == null) throw new ArgumentNullException(nameof(creditSystem));

			return creditSystem.FundsTransferFileConverterName != null ? 
				GetFundsTransferFileConverter(creditSystem.FundsTransferFileConverterName) : 
				null;
		}

		/// <summary>
		/// Get all the funds transfer file converters registered in the system.
		/// </summary>
		public IEnumerable<IFundsTransferFileConverter> GetFundsTransferFileConverters()
			=> this.SessionSettings.ResolveAll<IFundsTransferFileConverter>();

		#endregion

		/// <summary>
		/// Get the total statistics of funds transfers.
		/// </summary>
		public async Task<FundsTransferStatistic> GetTotalStatisticAsync()
		{
			var query = from b in this.FundsTransferBatches
									let lm = b.Messages.OrderByDescending(m => m.Time).FirstOrDefault() // The last message of the batch
									group lm by 1 into g
									select new FundsTransferStatistic
									{
										PendingBatchesCount = g.Count(m => m.Type == FundsTransferBatchMessageType.Pending),
										SubmittedBatchesCount = g.Count(m => m.Type == FundsTransferBatchMessageType.Submitted),
										RejectedBatchesCount = g.Count(m => m.Type == FundsTransferBatchMessageType.Rejected),
										AcceptedBatchesCount = g.Count(m => m.Type == FundsTransferBatchMessageType.Accepted),
										RespondedBatchesCount = g.Count(m => m.Type == FundsTransferBatchMessageType.Responded),
										UnbatchedRequestsCount = this.UnbatchedFundsTransferRequests.Count()
									};

			var statistic = await query.FirstOrDefaultAsync();

			if (statistic == null) // Statistic will be null when there are no batches due to batch grouping.
			{
				// OK, all batch counts are zero except possibly the unbatched requests count.
				statistic = new FundsTransferStatistic
				{
					UnbatchedRequestsCount = this.UnbatchedFundsTransferRequests.Count()
				};
			}

			return statistic;
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
			if (includeSubmitted)
			{
				return FilterRequestsByLatestEvent(
					e => e.Type == FundsTransferEventType.Pending || e.Type == FundsTransferEventType.Submitted);
			}
			else
			{
				return FilterRequestsByLatestEvent(
					e => e.Type == FundsTransferEventType.Pending);
			}
		}

		/// <summary>
		/// From the set of <see cref="FundsTransferRequests"/>, filter those whose
		/// last event matches a predicate.
		/// </summary>
		/// <param name="latestEventPredicate">The predicate to apply to the last event of each request.</param>
		/// <returns>Returns the set of filtered requests.</returns>
		public IQueryable<FundsTransferRequest> FilterRequestsByLatestEvent(Expression<Func<FundsTransferEvent, bool>> latestEventPredicate)
		{
			if (latestEventPredicate == null) throw new ArgumentNullException(nameof(latestEventPredicate));

			return this.FundsTransferRequests
				.Select(r => r.Events.OrderByDescending(e => e.Time).FirstOrDefault())
				.Where(latestEventPredicate)
				.Select(e => e.Request);
		}

		/// <summary>
		/// From the set of <see cref="FundsTransferBatches"/>, filter those whose
		/// last message matches a predicate.
		/// </summary>
		/// <param name="latestMessagePredicate">The predicate to apply to the last message of each batch.</param>
		/// <returns>Returns the set of filtered batches.</returns>
		public IQueryable<FundsTransferBatch> FilterBatchesByLatestMesage(Expression<Func<FundsTransferBatchMessage, bool>> latestMessagePredicate)
		{
			if (latestMessagePredicate == null) throw new ArgumentNullException(nameof(latestMessagePredicate));

			return this.FundsTransferBatches
				.Select(b => b.Messages.OrderByDescending(m => m.Time).FirstOrDefault())
				.Where(latestMessagePredicate)
				.Select(m => m.Batch);
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

			if (file.Items.Count == 0) return new FundsResponseResult[0];

			var firstItem = file.Items.First();

			var batchQuery = from b in this.FundsTransferBatches
											 where b.ID == file.BatchID
											 select b;

			var batch = await batchQuery.Include(b => b.Messages).SingleAsync();

			using (var accountingSession = CreateAccountingSession())
			using (GetElevatedAccessScope())
			{
				var responseBatchMessage = await accountingSession.AddFundsTransferBatchMessageAsync(
					batch,
					FundsTransferBatchMessageType.Responded,
					file.Time);

				return await DigestResponseFileAsync(file, responseBatchMessage);
			}
		}

		/// <summary>
		/// manual acceptance of a line in a batch.
		/// </summary>
		/// <param name="line">The line to accept.</param>
		/// <returns>
		/// Returns the collection of the results which correspond to the 
		/// funds transfer requests grouped in the line.
		/// </returns>
		public virtual async Task<IReadOnlyCollection<FundsResponseResult>> AcceptResponseLineAsync(FundsResponseLine line)
		{
			if (line == null) throw new ArgumentNullException(nameof(line));

			var requestsQuery = from r in this.FundsTransferRequests
													where r.BatchID == line.BatchID && r.GroupID == line.LineID
													select r;

			var requests = await requestsQuery.Include(r => r.Batch).ToArrayAsync();

			if (requests.Length == 0)
				throw new UserException(FundsTransferManagerMessages.FILE_NOT_APPLICABLE);

			var results = new List<FundsResponseResult>(requests.Length);

			foreach (var fundsTransferRequest in requests)
			{
				FundsResponseResult result =
					await AcceptResponseItemAsync(fundsTransferRequest, line);

				results.Add(result);
			}

			return results;
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
		/// <exception cref="FundsFileSchemaException">
		/// Thrown when the XML contents are not according the the schema
		/// for a <see cref="FundsResponseFile"/>.
		/// </exception>
		public async Task<IReadOnlyCollection<FundsResponseResult>> AcceptResponseFileAsync(System.IO.Stream stream)
			=> await AcceptResponseFileAsync(ReadResponseFile(stream));

		/// <summary>
		/// Read a <see cref="FundsResponseFile"/> from a stream containing its XML representation.
		/// </summary>
		/// <param name="stream">The input stream.</param>
		/// <returns>Returns the response file.</returns>
		/// <exception cref="FundsFileSchemaException">
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

				try
				{
					return (FundsResponseFile)serializer.Deserialize(xmlReader);
				}
				catch (InvalidOperationException ex)
				{
					throw TranslateToFundsFileSchemaException(ex);
				}
			}
		}

		/// <summary>
		/// Read a <see cref="FundsRequestFile"/> from a stream containing its XML representation.
		/// </summary>
		/// <param name="stream">The input stream.</param>
		/// <returns>Returns the request file.</returns>
		/// <exception cref="FundsFileSchemaException">
		/// Thrown when the XML contents are not according the the schema
		/// for a <see cref="FundsRequestFile"/>.
		/// </exception>
		public FundsRequestFile ReadRequestFile(System.IO.Stream stream)
		{
			if (stream == null) throw new ArgumentNullException(nameof(stream));

			var requestSchemaSet = GetRequestSchemaSet();

			var xmlReaderSettings = new XmlReaderSettings
			{
				Schemas = requestSchemaSet,
				ValidationType = ValidationType.Schema,
			};

			using (var xmlReader = XmlReader.Create(stream, xmlReaderSettings))
			{
				var serializer = GetRequestFileSerializer();

				try
				{
					return (FundsRequestFile)serializer.Deserialize(xmlReader);
				}
				catch (InvalidOperationException ex)
				{
					throw TranslateToFundsFileSchemaException(ex);
				}
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

			using (var accountingSession = CreateAccountingSession())
			using (GetElevatedAccessScope())
			{
				return await accountingSession.EnrollRequestsIntoBatchAsync(creditSystem, requests);
			}
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

			using (var accountingSession = CreateAccountingSession())
			using (GetElevatedAccessScope())
			{
				return await accountingSession.EnrollRequestsIntoBatchAsync(creditSystem, requests);
			}
		}

		/// <summary>
		/// Create a funds request file for a batch.
		/// </summary>
		/// <param name="pendingBatchMessage">The 'pending' message for the batch.</param>
		/// <returns>Returns the funds request file.</returns>
		/// <exception cref="LogicException">
		/// Thrown when the specified message has <see cref="FundsTransferBatchMessage.Type"/> other
		/// than <see cref="FundsTransferBatchMessageType.Pending"/> or when
		/// it has the <see cref="FundsTransferBatchMessage.Batch"/> not properly set up.
		/// </exception>
		/// <remarks>
		/// For best performance, eager fetch the Batch.CreditSystem and Events.Request.Group
		/// relationships of the <paramref name="pendingBatchMessage"/>.
		/// </remarks>
		public FundsRequestFile ExportRequestFile(FundsTransferBatchMessage pendingBatchMessage)
		{
			if (pendingBatchMessage == null) throw new ArgumentNullException(nameof(pendingBatchMessage));

			if (pendingBatchMessage.Type != FundsTransferBatchMessageType.Pending)
				throw new LogicException(
					$"The given batch message has type '{pendingBatchMessage.Type}' instead of '{FundsTransferBatchMessageType.Pending}'.");

			string creditSystemCodeName = pendingBatchMessage?.Batch?.CreditSystem?.CodeName;

			if (creditSystemCodeName == null)
				throw new LogicException("The Batch of the message is not properly set up.");

			var items = from e in pendingBatchMessage.Events
									let r = e.Request
									group r by r.Group into g
									select new FundsRequestFileItem()
									{
										Amount = g.Sum(r => r.Amount),
										LineID = g.Key.ID,
										BankAccountInfo = g.Key.EncryptedBankAccountInfo.Decrypt(),
										AccountHolderName = g.Key.AccountHolderName
									};

			return new FundsRequestFile
			{
				CreditSystemCodeName = creditSystemCodeName,
				Time = pendingBatchMessage.Time,
				BatchID = pendingBatchMessage.BatchID,
				BatchMessageID = pendingBatchMessage.ID,
				Items = new FundsRequestFileItems(items)
			};
		}

		/// <summary>
		/// Create a funds request file for a batch.
		/// </summary>
		/// <param name="batchID">The ID of the batch.</param>
		/// <returns>Returns the funds request file.</returns>
		public async Task<FundsRequestFile> ExportRequestFileAsync(long batchID)
		{
			var pendingBatchMessage = await this.FundsTransferBatchMessages
				.Include(m => m.Batch.CreditSystem)
				.Include(m => m.Events.Select(e => e.Request.Group))
				.SingleOrDefaultAsync(e => e.BatchID == batchID && e.Type == FundsTransferBatchMessageType.Pending);

			if (pendingBatchMessage == null)
				throw new LogicException($"A message with batch ID '{batchID}' was not found in the specified messages set.");

			return ExportRequestFile(pendingBatchMessage);
		}

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
		/// Get the XML document representing a <see cref="FundsResponseFile"/>.
		/// </summary>
		/// <param name="responseFile">The response file object.</param>
		/// <returns>Returns the XML document.</returns>
		public XmlDocument GetResponseFileXML(FundsResponseFile responseFile)
		{
			if (responseFile == null) throw new ArgumentNullException(nameof(responseFile));

			XmlDocument document = new XmlDocument();

			using (var xmlWriter = document.CreateNavigator().AppendChild())
			{
				WriteResponseFile(xmlWriter, responseFile);
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
		/// Write a <see cref="FundsResponseFile"/> into an XML writer.
		/// </summary>
		/// <param name="xmlWriter">Thee XML writer.</param>
		/// <param name="reqsponseFile">The response file to write.</param>
		public void WriteResponseFile(XmlWriter xmlWriter, FundsResponseFile reqsponseFile)
		{
			if (xmlWriter == null) throw new ArgumentNullException(nameof(xmlWriter));
			if (reqsponseFile == null) throw new ArgumentNullException(nameof(reqsponseFile));

			var serializer = GetResponseFileSerializer();

			serializer.Serialize(xmlWriter, reqsponseFile);
		}

		/// <summary>
		/// Add a rejection message to a batch.
		/// </summary>
		/// <param name="batchID">The ID of the batch.</param>
		/// <param name="comments">Optional comments to record in the message. Maximum length is <see cref="FundsTransferBatchMessage.CommentsLength"/>.</param>
		/// <param name="messageCode">Optional code to record inthe message. Maximum length is <see cref="FundsTransferBatchMessage.MessageCodeLength"/>.</param>
		/// <param name="utcTime">Optional time in UTC, else the current time is implied.</param>
		/// <returns>returns the created message.</returns>
		/// <exception cref="AccountingException">
		/// Thrown when a more recent message than <paramref name="utcTime"/> exists.
		/// </exception>
		public async Task<FundsTransferBatchMessage> AddBatchRejectedMessageAsync(
			long batchID,
			String comments = null,
			String messageCode = null,
			DateTime? utcTime = null)
			=> await AddBatchMessageAsync(batchID, FundsTransferBatchMessageType.Rejected, comments, messageCode, utcTime);

		/// <summary>
		/// Attempt to set the batch as submitted, if is not already set, else do nothing.
		/// </summary>
		/// <param name="batchID">The ID of the batch.</param>
		/// <param name="utcTime">Optional time in UTC, else the current time is implied.</param>
		/// <returns>If the batch was not already submitted, returns the submission event, else null.</returns>
		/// <exception cref="AccountingException">
		/// Thrown when the batch is not already set as submitted
		/// and a more recent message than <paramref name="utcTime"/> exists.
		/// </exception>
		public async Task<FundsTransferBatchMessage> TrySetBatchAsSubmittedAsync(long batchID, DateTime? utcTime = null)
			=> await TryAddBatchMessageAsync(batchID, FundsTransferBatchMessageType.Submitted, utcTime);

		/// <summary>
		/// Attempt to set the batch as accepted, if is not already set, else do nothing.
		/// </summary>
		/// <param name="batchID">The ID of the batch.</param>
		/// <param name="utcTime">Optional time in UTC, else the current time is implied.</param>
		/// <returns>If the batch was not already submitted, returns the submission event, else null.</returns>
		/// <exception cref="AccountingException">
		/// Thrown when the batch is not already set as accepted
		/// and a more recent message than <paramref name="utcTime"/> exists.
		/// </exception>
		public async Task<FundsTransferBatchMessage> TrySetBatchAsAcceptedAsync(long batchID, DateTime? utcTime = null)
			=> await TryAddBatchMessageAsync(batchID, FundsTransferBatchMessageType.Accepted, utcTime);

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
		/// Create an accounting session. The caller is responsible for disposing it.
		/// </summary>
		protected AS CreateAccountingSession()
			=> accountingSessionFactory(this.DomainContainer, this.Session.User);

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

			long[] lineIDs = file.Items.Select(i => i.LineID).ToArray();

			var results = new List<FundsResponseResult>(file.Items.Count);

			var fundsTransferRequestsQuery = from r in this.FundsTransferRequests.Include(r => r.Batch.CreditSystem).Include(r => r.Group)
																			 where r.BatchID == file.BatchID && lineIDs.Contains(r.GroupID)
																			 select r;

			var fundsTransferRequests = await fundsTransferRequestsQuery.Include(r => r.Events).ToArrayAsync();

			if (fundsTransferRequests.Length == 0)
				throw new UserException(FundsTransferManagerMessages.FILE_NOT_APPLICABLE);

			var itemsByLineID = file.Items.ToDictionary(i => i.LineID);

			foreach (var fundsTransferRequest in fundsTransferRequests)
			{
				var item = itemsByLineID[fundsTransferRequest.GroupID];

				FundsResponseResult result =
					await AcceptResponseItemAsync(file, item, fundsTransferRequest, responseBatchMessage);

				results.Add(result);
			}

			return results;
		}

		/// <summary>
		/// Translate the <see cref="FundsResponseLine.Status"/> of a batch line into a <see cref="FundsTransferEventType"/>.
		/// </summary>
		/// <param name="line">The funds transfer batch line.</param>
		/// <returns>Returns the corresponding <see cref="FundsTransferEventType"/>.</returns>
		/// <exception cref="LogicException">
		/// Thrown when the conversion is not possible.
		/// </exception>
		protected virtual FundsTransferEventType GetEventTypeFromResponseLine(FundsResponseLine line)
		{
			if (line == null) throw new ArgumentNullException(nameof(line));

			FundsTransferEventType eventType;

			switch (line.Status)
			{
				case FundsResponseStatus.Reject:
					eventType = FundsTransferEventType.Rejected;
					break;

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
					throw new LogicException($"Unexpected item status '{line.Status}' for request with ID {line.LineID}.");
			}

			return eventType;
		}

		/// <summary>
		/// Called when the funds transfer event is created for successfully digesting a funds response line.
		/// </summary>
		/// <param name="fundsResponseLine">The funds transfer line being digested.</param>
		/// <param name="fundsTransferEvent">The event that is created for the line.</param>
		/// <param name="remittance">The remittance serving the transfer event, if any.</param>
		/// <remarks>
		/// The default implementation does nothing.
		/// </remarks>
		protected virtual Task OnResponseLineDigestionSuccessAsync(
			FundsResponseLine fundsResponseLine,
			FundsTransferEvent fundsTransferEvent,
			R remittance = null)
			=> Task.CompletedTask;

		/// <summary>
		/// Called when the funds transfer event is created for marking an error digesting a funds response line.
		/// </summary>
		/// <param name="fundsResponseLine">The funds transfer line being digested.</param>
		/// <param name="fundsTransferEvent">The event that is created for the line.</param>
		/// <param name="exception">The exception occured during digestion.</param>
		/// <remarks>
		/// The default implementation does nothing.
		/// </remarks>
		protected virtual Task OnResponseLineDigestionFailureAsync(
			FundsResponseLine fundsResponseLine,
			FundsTransferEvent fundsTransferEvent,
			Exception exception)
			=> Task.CompletedTask;

		/// <summary>
		/// Override to append the journal during processing of a batch line. The default implementation deoes nothing.
		/// </summary>
		/// <param name="journal">The journal to append.</param>
		/// <param name="request">The funds transfer request being processed.</param>
		/// <param name="line">The line of the batch file being processed.</param>
		/// <param name="eventType">The type of funds transfer event which will be recorded.</param>
		/// <param name="exception">If not null, the exception produced during the processing of the line.</param>
		protected virtual Task AppendResponseJournalAsync(
			J journal,
			FundsTransferRequest request,
			FundsResponseLine line,
			FundsTransferEventType eventType,
			Exception exception = null)
			=> Task.CompletedTask;

		/// <summary>
		/// Create a failure funds transfer event for an exception as a result of a response line processing.
		/// </summary>
		/// <param name="fundsTransferRequest">The funds transfer request for which to record the event.</param>
		/// <param name="line">The funds response line.</param>
		/// <param name="exception">The exception to save.</param>
		/// <param name="eventType">The type of the event which.</param>
		/// <returns>Returns the line processing result for the error.</returns>
		/// <remarks>
		/// If there is a double exception when trying to record the exception,
		/// the double exception is logged and a response result is returned with the original exception in it
		/// and without an event.
		/// </remarks>
		protected virtual async Task<FundsResponseResult> RecordDigestionExceptionEventAsync(
			FundsTransferRequest fundsTransferRequest,
			FundsResponseLine line,
			Exception exception,
			FundsTransferEventType eventType)
		{
			if (fundsTransferRequest == null) throw new ArgumentNullException(nameof(fundsTransferRequest));
			if (line == null) throw new ArgumentNullException(nameof(line));
			if (exception == null) throw new ArgumentNullException(nameof(exception));

			try
			{
				using (var accountingSession = CreateAccountingSession())
				using (GetElevatedAccessScope())
				using (var transaction = this.DomainContainer.BeginTransaction())
				{
					var errorActionResult = await accountingSession.AddFundsTransferEventAsync(
						fundsTransferRequest,
						line.Time,
						eventType,
						j => AppendResponseJournalAsync(j, fundsTransferRequest, line, eventType, exception),
						line.BatchMessageID,
						line.ResponseCode,
						line.TraceCode,
						line.Comments,
						exception: exception);

					await OnResponseLineDigestionFailureAsync(line, errorActionResult.FundsTransferEvent, exception);

					await transaction.CommitAsync();

					return new FundsResponseResult
					{
						Event = errorActionResult.FundsTransferEvent,
						Exception = exception,
						Line = line
					};
				}
			}
			catch (Exception doubleException)
			{
				this.Logger.Log(NLog.LogLevel.Error, doubleException);

				this.DomainContainer.ChangeTracker.UndoChanges();

				return new FundsResponseResult
				{
					Exception = exception,
					Line = line
				};
			}
		}

		#endregion

		#region Private methods

		/// <summary>
		/// Add a message to a batch.
		/// </summary>
		/// <param name="batchID">The ID of the batch.</param>
		/// <param name="messageType">The type of the message.</param>
		/// <param name="comments">Optional comments to record in the message. Maximum length is <see cref="FundsTransferBatchMessage.CommentsLength"/>.</param>
		/// <param name="messageCode">Optional code to record inthe message. Maximum length is <see cref="FundsTransferBatchMessage.MessageCodeLength"/>.</param>
		/// <param name="utcTime">Optional time in UTC, else the current time is implied.</param>
		/// <returns>returns the created message.</returns>
		/// <exception cref="AccountingException">
		/// Thrown when <paramref name="messageType"/> is <see cref="FundsTransferBatchMessageType.Pending"/>
		/// or <see cref="FundsTransferBatchMessageType.Responded"/> and
		/// there already exists a message with the same type,
		/// or when a more recent message than <paramref name="utcTime"/> exists.
		/// </exception>
		private async Task<FundsTransferBatchMessage> AddBatchMessageAsync(
			long batchID,
			FundsTransferBatchMessageType messageType,
			String comments = null,
			String messageCode = null,
			DateTime? utcTime = null)
		{
			using (var transaction = this.DomainContainer.BeginTransaction())
			using (var accountingSession = CreateAccountingSession())
			using (GetElevatedAccessScope())
			{
				var batch = await this.FundsTransferBatches.Include(b => b.Messages).SingleAsync(b => b.ID == batchID);

				utcTime = utcTime ?? DateTime.UtcNow;

				var message = await accountingSession.AddFundsTransferBatchMessageAsync(
					batch,
					messageType,
					utcTime.Value,
					comments,
					messageCode);

				await transaction.CommitAsync();

				return message;
			}
		}

		/// <summary>
		/// Attempt to add a message to a batch, if its type is not already existing, else do nothing and return null.
		/// </summary>
		/// <param name="batchID">The ID of the batch.</param>
		/// <param name="messageType">The type of the message.</param>
		/// <param name="utcTime">Optional time in UTC, else the current time is implied.</param>
		/// <returns>If a message of the specified type did not preexist, returns the added message, else null.</returns>
		/// <exception cref="AccountingException">
		/// Thrown when there des not exist a message with the same type
		/// and a more recent message than <paramref name="utcTime"/> exists.
		/// </exception>
		private async Task<FundsTransferBatchMessage> TryAddBatchMessageAsync(
			long batchID,
			FundsTransferBatchMessageType messageType,
			DateTime? utcTime = null)
		{
			using (var transaction = this.DomainContainer.BeginTransaction())
			using (var accountingSession = CreateAccountingSession())
			using (GetElevatedAccessScope())
			{
				var batch = await this.FundsTransferBatches.Include(b => b.Messages).SingleAsync(b => b.ID == batchID);

				if (batch.Messages.Any(m => m.Type == messageType)) return null;

				utcTime = utcTime ?? DateTime.UtcNow;

				var message = await accountingSession.AddFundsTransferBatchMessageAsync(
					batch,
					messageType,
					utcTime.Value);

				await transaction.CommitAsync();

				return message;
			}
		}

		private static XmlSerializer GetResponseFileSerializer()
			=> lazyResponseFileSerializer.Value;

		private static XmlSerializer GetRequestFileSerializer()
			=> lazyRequestFileSerializer.Value;

		private XmlSchemaSet GetResponseSchemaSet()
			=> lazyResponseSchemaSet.Value;

		private XmlSchemaSet GetRequestSchemaSet()
			=> lazyRequestSchemaSet.Value;

		private static XmlSchemaSet CreateResponseSchemaSet()
			=> CreateSchemaSet("Grammophone.Domos.Logic.Models.FundsTransfer.FundsResponseFile.xsd");

		private static XmlSchemaSet CreateRequestSchemaSet()
			=> CreateSchemaSet("Grammophone.Domos.Logic.Models.FundsTransfer.FundsRequestFile.xsd");

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

			var line = new FundsResponseLine(file, item, responseBatchMessage.ID);

			return await AcceptResponseItemAsync(request, line);
		}

		private async Task<FundsResponseResult> AcceptResponseItemAsync(FundsTransferRequest request, FundsResponseLine line)
		{
			FundsTransferEventType eventType = GetEventTypeFromResponseLine(line);

			// Is there already a successfully digested event for the line?

			var previousEvent = request.Events.FirstOrDefault(e => e.Type == eventType && e.ExceptionData == null);

			if (previousEvent != null)
			{
				return new FundsResponseResult
				{
					Event = previousEvent,
					Line = line,
					IsAlreadyDigested = true
				};
			}

			try
			{
				using (var transaction = this.DomainContainer.BeginTransaction())
				using (var accountingSession = CreateAccountingSession())
				using (GetElevatedAccessScope())
				{
					var actionResult = await accountingSession.AddFundsTransferEventAsync(
						request,
						line.Time,
						eventType,
						asyncJournalAppendAction: j => AppendResponseJournalAsync(j, request, line, eventType, null),
						batchMessageID: line.BatchMessageID,
						responseCode: line.ResponseCode,
						comments: line.Comments,
						traceCode: line.TraceCode);

					var transferEvent = actionResult.FundsTransferEvent;

					var transferRemittance = TryGetTransferRemittance(actionResult);

					await OnResponseLineDigestionSuccessAsync(line, transferEvent, transferRemittance);

					await transaction.CommitAsync();

					return new FundsResponseResult
					{
						Event = transferEvent,
						Line = line
					};
				}
			}
			catch (Exception exception)
			{
				this.DomainContainer.ChangeTracker.UndoChanges(); // Undo attempted entities.

				return await RecordDigestionExceptionEventAsync(request, line, exception, eventType);
			}
		}

		private FundsFileSchemaException TranslateToFundsFileSchemaException(InvalidOperationException invalidOperationException)
		{
			string message;

			if (invalidOperationException.InnerException != null)
			{
				message = $"{invalidOperationException.Message} - {invalidOperationException.InnerException.Message}";
			}
			else
			{
				message = invalidOperationException.Message;
			}

			return new FundsFileSchemaException(message, invalidOperationException);
		}

		private R TryGetTransferRemittance(AccountingSession<U, BST, P, R, J, D>.ActionResult actionResult)
			=> actionResult.Journal.Remittances.FirstOrDefault(r => r.FundsTransferEvent == actionResult.FundsTransferEvent);

		#endregion
	}
}
