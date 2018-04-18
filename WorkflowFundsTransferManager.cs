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
		where AS : AccountingSession<U, BST, P, R, J, D>
		where WM : IWorkflowManager<U, ST, SO>
	{
		#region Auxilliary classes

		/// <summary>
		/// Association of a funds transfer event to a stateful object.
		/// </summary>
		public class FundsTransferEventAssociation
		{
			/// <summary>
			/// The funds transfer event.
			/// </summary>
			public FundsTransferEvent Event { get; set; }

			/// <summary>
			/// The stateful object of type <typeparamref name="SO"/> associated with the <see cref="Event"/>.
			/// </summary>
			public SO StatefulObject { get; set; }

			/// <summary>
			/// The state of the <see cref="StatefulObject"/>.
			/// </summary>
			public State State { get; set; }
		}

		#endregion

		#region Private fields

		private AsyncSequentialMRUCache<string, StatePath> statePathsByCodeNameCache;

		#endregion

		#region Construction

		/// <summary>
		/// Create.
		/// </summary>
		/// <param name="session">The logic session.</param>
		/// <param name="accountingSessionFactory">A factory for creating an accounting session.</param>
		/// <param name="workflowManager">The associated workflow manager.</param>
		protected WorkflowFundsTransferManager(
			S session,
			Func<D, U, AS> accountingSessionFactory,
			WM workflowManager)
			: base(session, accountingSessionFactory)
		{
			if (workflowManager == null) throw new ArgumentNullException(nameof(workflowManager));

			this.WorkflowManager = workflowManager;

			this.statePathsByCodeNameCache =
				new AsyncSequentialMRUCache<string, StatePath>(LoadStatePathAsync);
		}

		#endregion

		#region Public properties

		/// <summary>
		/// The set of associations of managed funds transfer events with stateful objects
		/// of type <typeparamref name="SO"/>.
		/// </summary>
		public abstract IQueryable<FundsTransferEventAssociation> FundsTransferEventAssociations { get; }

		#endregion

		#region Protected properties

		/// <summary>
		/// The associated workflow manager.
		/// </summary>
		protected WM WorkflowManager { get; private set; }

		#endregion

		#region Public methods

		/// <summary>
		/// manual acceptance of a line in a batch.
		/// </summary>
		/// <param name="line">The line to accept.</param>
		/// <returns>
		/// Returns the collection of the results which correspond to the 
		/// funds transfer requests grouped in the line.
		/// </returns>
		public override async Task<IReadOnlyCollection<FundsResponseResult>> AcceptResponseLineAsync(FundsResponseLine line)
		{
			if (line == null) throw new ArgumentNullException(nameof(line));

			var associationsQuery = from a in this.FundsTransferEventAssociations
															let ftr = a.Event.Request
															where ftr.GroupID == line.LineID && ftr.BatchID == line.BatchID
															select new
															{
																a.Event.Request,
																a.StatefulObject,
																a.State
															};

			var associations = await associationsQuery.ToArrayAsync();

			if (associations.Length == 0)
				throw new UserException(FundsTransferManagerMessages.FILE_NOT_APPLICABLE);

			var responseResults = new List<FundsResponseResult>(associations.Length);

			foreach (var association in associations)
			{
				var fundsResponseResult =
					await AcceptResponseItemAsync(
						association.StatefulObject,
						association.Request,
						line);

				responseResults.Add(fundsResponseResult);
			}

			return responseResults;
		}

		#endregion

		#region Protected methods

		/// <summary>
		/// Decides the state path to execute on a stateful object
		/// when a <see cref="FundsResponseLine"/> arrives for it.
		/// Returns null to indicate that no path should be executed and the that the line should
		/// be consumed directly. Throws an exception to abort normal digestion of the line
		/// and to record a transfer event of type <see cref="FundsTransferEventType.Failed"/> instead.
		/// </summary>
		/// <param name="stateCodeName">The code name of the current state.</param>
		/// <param name="fundsResponseLine">The batch line arriving for the stateful object.</param>
		/// <returns>Returns the code name of the path to execute or null to execute none.</returns>
		/// <exception cref="Exception">
		/// Thrown to record a funds transfer event with type <see cref="FundsTransferEventType.Failed"/>
		/// containing the thrown exception.
		/// </exception>
		protected abstract string TryGetNextStatePathCodeName(
			string stateCodeName,
			FundsResponseLine fundsResponseLine);

		/// <summary>
		/// Digest a funds transfer response file from a credit system
		/// and execute the appropriate state paths
		/// as specified by the <see cref="TryGetNextStatePathCodeName(string, FundsResponseLine)"/> method
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

			long[] lineIDs = file.Items.Select(i => i.LineID).ToArray();

			var associationsQuery = from a in this.FundsTransferEventAssociations
															let ftr = a.Event.Request
															where lineIDs.Contains(ftr.GroupID) && ftr.BatchID == file.BatchID
															select new
															{
																a.Event.Request,
																a.StatefulObject,
																a.State
															};

			var associations = await associationsQuery.ToArrayAsync();

			if (associations.Length == 0)
				throw new UserException(FundsTransferManagerMessages.FILE_NOT_APPLICABLE);

			var responseResults = new List<FundsResponseResult>(associations.Length);

			var itemsByLineID = file.Items.ToDictionary(i => i.LineID);

			foreach (var association in associations)
			{
				var fundsResponseResult =
					await AcceptResponseItemAsync(
						file,
						itemsByLineID[association.Request.GroupID],
						association.StatefulObject,
						association.Request,
						responseBatchMessage);

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
			SO statefulObject,
			FundsTransferRequest fundsTransferRequest,
			FundsTransferBatchMessage responseBatchMessage)
		{
			if (file == null) throw new ArgumentNullException(nameof(file));
			if (item == null) throw new ArgumentNullException(nameof(item));
			if (fundsTransferRequest == null) throw new ArgumentNullException(nameof(fundsTransferRequest));

			var line = new FundsResponseLine(file, item, responseBatchMessage.ID);

			return await AcceptResponseItemAsync(statefulObject, fundsTransferRequest, line);
		}

		private async Task<FundsResponseResult> AcceptResponseItemAsync(SO statefulObject, FundsTransferRequest fundsTransferRequest, FundsResponseLine line)
		{
			if (fundsTransferRequest == null) throw new ArgumentNullException(nameof(fundsTransferRequest));
			if (line == null) throw new ArgumentNullException(nameof(line));

			var actionArguments = new Dictionary<string, object>
			{
				[StandardArgumentKeys.BillingItem] = line
			};

			try
			{
				var fundsResponseResult = new FundsResponseResult
				{
					Line = line
				};

				string currentStateCodeName = statefulObject.State.CodeName;

				// Attempt to get the next path to be executed. Any exception will be recorded in a failure funds transfer event.
				string nextStatePathCodeName = TryGetNextStatePathCodeName(currentStateCodeName, line);

				if (nextStatePathCodeName != null) // A path should be executed?
				{
					var statePath = await statePathsByCodeNameCache.Get(nextStatePathCodeName);

					var transition = await WorkflowManager.ExecuteStatePathAsync(
						statefulObject,
						statePath,
						actionArguments);

					fundsResponseResult.Event = transition.FundsTransferEvent;
				}
				else // If no path is specified, record the event directly.
				{
					using (var accountingSession = CreateAccountingSession())
					using (GetElevatedAccessScope())
					{
						var eventType = GetEventTypeFromResponseLine(line);

						var directActionResult = await accountingSession.AddFundsTransferEventAsync(
							fundsTransferRequest,
							line.Time,
							eventType,
							j => AppendResponseJournalAsync(j, fundsTransferRequest, line, eventType, null),
							line.BatchMessageID,
							line.ResponseCode,
							line.TraceCode,
							line.Comments);

						fundsResponseResult.Event = directActionResult.FundsTransferEvent;
					}
				}

				return fundsResponseResult;
			}
			catch (Exception exception)
			{
				this.DomainContainer.ChangeTracker.UndoChanges(); // Undo attempted entities.

				return await RecordDigestionExceptionEventAsync(fundsTransferRequest, line, exception, GetEventTypeFromResponseLine(line));
			}
		}

		#endregion
	}
}
