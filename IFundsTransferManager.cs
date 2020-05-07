using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using System.Xml;
using Grammophone.Domos.Accounting;
using Grammophone.Domos.Domain;
using Grammophone.Domos.Domain.Accounting;
using Grammophone.Domos.Domain.Workflow;
using Grammophone.Domos.Logic.Models.FundsTransfer;

namespace Grammophone.Domos.Logic
{
	/// <summary>
	/// Interface for a funds transfer manager.
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
	public interface IFundsTransferManager<U, BST, P, R, J>
		where U : User
		where BST : StateTransition<U>
		where P : Posting<U>
		where R : Remittance<U>
		where J : Journal<U, BST, P, R>
	{
		/// <summary>
		/// The set of credit systems handled by the manager.
		/// </summary>
		IQueryable<CreditSystem> CreditSystems { get; }

		/// <summary>
		/// The funds transfer batches handled by this manager.
		/// </summary>
		IQueryable<FundsTransferBatch> FundsTransferBatches { get; }

		/// <summary>
		/// The funds transfer batch messages in the system.
		/// </summary>
		IQueryable<FundsTransferBatchMessage> FundsTransferBatchMessages { get; }

		/// <summary>
		/// The funds transfer events handled by this manager.
		/// </summary>
		IQueryable<FundsTransferEvent> FundsTransferEvents { get; }

		/// <summary>
		/// The funds transfer requests handled by this manager which are not enrolled in a batch.
		/// </summary>
		IQueryable<FundsTransferRequest> FundsTransferRequests { get; }

		/// <summary>
		/// The set of journals associated with the <see cref="FundsTransferEvents"/>
		/// handled by this manager.
		/// </summary>
		IQueryable<J> Journals { get; }

		/// <summary>
		/// The set of remittances associated with the <see cref="FundsTransferEvents"/>
		/// handled by this manager.
		/// </summary>
		IQueryable<R> Remittances { get; }

		/// <summary>
		/// The funds transfer requests handled by this manager which are not enrolled in a batch.
		/// </summary>
		IQueryable<FundsTransferRequest> UnbatchedFundsTransferRequests { get; }

		/// <summary>
		/// Accept a funds response file
		/// and execute per line any accounting or workflow associated with this manager.
		/// </summary>
		/// <param name="file">The file to digest.</param>
		/// <returns>
		/// Returns a collection of results describing the execution outcome of the
		/// contents of the <paramref name="file"/> or an empty collection if the file is not relevant to this manager.
		/// </returns>
		Task<IReadOnlyCollection<FundsResponseResult>> AcceptResponseFileAsync(FundsResponseFile file);

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
		Task<IReadOnlyCollection<FundsResponseResult>> AcceptResponseFileAsync(Stream stream);

		/// <summary>
		/// manual acceptance of a line in a batch.
		/// </summary>
		/// <param name="line">The line to accept.</param>
		/// <returns>
		/// Returns the collection of the results which correspond to the 
		/// funds transfer requests grouped in the line.
		/// </returns>
		/// <exception cref="UserException">
		/// Thrown if the <paramref name="line"/> is not relevant to this manager.
		/// </exception>
		Task<IReadOnlyCollection<FundsResponseResult>> AcceptResponseLineAsync(FundsResponseLine line);

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
		Task<FundsTransferBatchMessage> EnrollRequestsIntoBatchAsync(long creditSystemID, IQueryable<FundsTransferRequest> requests);

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
		Task<FundsTransferBatchMessage> EnrollRequestsIntoBatchAsync(string creditSystemCodeName, IQueryable<FundsTransferRequest> requests);

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
		FundsRequestFile ExportRequestFile(FundsTransferBatchMessage pendingBatchMessage);

		/// <summary>
		/// Create a funds request file for a batch.
		/// </summary>
		/// <param name="batchID">The ID of the batch.</param>
		/// <returns>Returns the funds request file.</returns>
		Task<FundsRequestFile> ExportRequestFileAsync(long batchID);

		/// <summary>
		/// From the set of <see cref="FundsTransferBatches"/>, filter those whose
		/// last message matches a predicate.
		/// </summary>
		/// <param name="latestMessagePredicate">The predicate to apply to the last message of each batch.</param>
		/// <returns>Returns the set of filtered batches.</returns>
		IQueryable<FundsTransferBatch> FilterBatchesByLatestMesage(Expression<Func<FundsTransferBatchMessage, bool>> latestMessagePredicate);

		/// <summary>
		/// From the set of <see cref="FundsTransferRequests"/>, filter those whose
		/// last event matches a predicate.
		/// </summary>
		/// <param name="latestEventPredicate">The predicate to apply to the last event of each request.</param>
		/// <returns>Returns the set of filtered requests.</returns>
		IQueryable<FundsTransferRequest> FilterRequestsByLatestEvent(Expression<Func<FundsTransferEvent, bool>> latestEventPredicate);

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
		Task<CreditSystem> GetCreditSystemAsync(long creditSystemID);

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
		Task<CreditSystem> GetCreditSystemAsync(string creditSystemCodeName);

		/// <summary>
		/// Get the funds transfer file converter associated with a credit system or null if no converter
		/// is associated.
		/// </summary>
		/// <param name="creditSystem">The credit system.</param>
		/// <returns>Returns the converter associated with the credit system or null.</returns>
		IFundsTransferFileConverter TryGetFundsTransferFileConverter(CreditSystem creditSystem);

		/// <summary>
		/// Get the funds transfer file converter registered under a name.
		/// </summary>
		/// <param name="converterName">The name under which the converter is registered.</param>
		IFundsTransferFileConverter GetFundsTransferFileConverter(string converterName);

		/// <summary>
		/// Get all the <see cref="IFundsTransferFileConverter"/> implementations registered in the system.
		/// </summary>
		IEnumerable<IFundsTransferFileConverter> GetFundsTransferFileConverters();

		/// <summary>
		/// Get all the registered implementations of the <see cref="IFundsTransferFileConverter"/> interface
		/// in a dictionary whose keys are their registration names.
		/// </summary>
		/// <returns>
		/// Returns a dictionary of the <see cref="IFundsTransferFileConverter"/> implemtnations keyes by their registration name.
		/// </returns>
		IReadOnlyDictionary<string, IFundsTransferFileConverter> GetFundsTransferFileConvertersByName();

		/// <summary>
		/// Get all the names under which the available <see cref="IFundsTransferFileConverter"/>
		/// implementations are registered in the system.
		/// </summary>
		IEnumerable<string> GetFundsTransferFileConvertersNames();

		/// <summary>
		/// From a set of <see cref="FundsTransferRequests"/>, filter those which
		/// have no response yet.
		/// </summary>
		/// <param name="includeSubmitted">
		/// If true, include the already submitted requests in the results,
		/// else exclude the submitted requests.
		/// </param>
		IQueryable<FundsTransferRequest> GetPendingRequests(bool includeSubmitted = false);

		/// <summary>
		/// Get the total statistics of funds transfers.
		/// </summary>
		Task<FundsTransferStatistic> GetTotalStatisticAsync();

		/// <summary>
		/// Get the XML document representing a <see cref="FundsRequestFile"/>.
		/// </summary>
		/// <param name="requestFile">The request file object.</param>
		/// <returns>Returns the XML document.</returns>
		XmlDocument GetRequestFileXML(FundsRequestFile requestFile);

		/// <summary>
		/// Get the XML document representing a <see cref="FundsResponseFile"/>.
		/// </summary>
		/// <param name="responseFile">The response file object.</param>
		/// <returns>Returns the XML document.</returns>
		XmlDocument GetResponseFileXML(FundsResponseFile responseFile);

		/// <summary>
		/// Read a <see cref="FundsRequestFile"/> from a stream containing its XML representation.
		/// </summary>
		/// <param name="stream">The input stream.</param>
		/// <returns>Returns the request file.</returns>
		/// <exception cref="FundsFileSchemaException">
		/// Thrown when the XML contents are not according the the schema
		/// for a <see cref="FundsRequestFile"/>.
		/// </exception>
		FundsRequestFile ReadRequestFile(Stream stream);

		/// <summary>
		/// Read a <see cref="FundsResponseFile"/> from a stream containing its XML representation.
		/// </summary>
		/// <param name="stream">The input stream.</param>
		/// <returns>Returns the response file.</returns>
		/// <exception cref="FundsFileSchemaException">
		/// Thrown when the XML contents are not according the the schema
		/// for a <see cref="FundsResponseFile"/>.
		/// </exception>
		FundsResponseFile ReadResponseFile(Stream stream);

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
		Task<FundsTransferBatchMessage> TrySetBatchAsAcceptedAsync(long batchID, DateTime? utcTime = null);

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
		Task<FundsTransferBatchMessage> TrySetBatchAsSubmittedAsync(long batchID, DateTime? utcTime = null);

		/// <summary>
		/// Writes a <see cref="FundsRequestFile"/> into a stream as XML.
		/// </summary>
		/// <param name="stream">The stream to write to.</param>
		/// <param name="requestFile">The request file to write.</param>
		void WriteRequestFile(Stream stream, FundsRequestFile requestFile);

		/// <summary>
		/// Write a <see cref="FundsRequestFile"/> into an XML writer.
		/// </summary>
		/// <param name="xmlWriter">Thee XML writer.</param>
		/// <param name="requestFile">The request file to write.</param>
		void WriteRequestFile(XmlWriter xmlWriter, FundsRequestFile requestFile);

		/// <summary>
		/// Writes a <see cref="FundsResponseFile"/> into a stream as XML.
		/// </summary>
		/// <param name="stream">The stream to write to.</param>
		/// <param name="responseFile">The response file to write.</param>
		void WriteResponseFile(Stream stream, FundsResponseFile responseFile);

		/// <summary>
		/// Write a <see cref="FundsResponseFile"/> into an XML writer.
		/// </summary>
		/// <param name="xmlWriter">Thee XML writer.</param>
		/// <param name="reqsponseFile">The response file to write.</param>
		void WriteResponseFile(XmlWriter xmlWriter, FundsResponseFile reqsponseFile);
	}
}
