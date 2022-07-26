using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
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
using Grammophone.GenericContentModel;

namespace Grammophone.Domos.Logic
{
	/// <summary>
	/// Base manager for exporting fund transfer requests and importing
	/// fund transfer responses optionally bound to a workflow.
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
	/// <typeparam name="SH">
	/// The type of entity holding the workflow state. This is the same as <typeparamref name="SO"/> if the
	/// the <see cref="IStateful{U, ST}"/> interface is implemented by the entity itself and not a surrogate/adapter.
	/// </typeparam>
	/// <typeparam name="AS">
	/// The type of accounting session, derived from <see cref="AccountingSession{U, BST, P, R, J, D}"/>.
	/// </typeparam>
	public abstract class WorkflowFundsTransferManager<U, BST, P, R, J, D, S, ST, SO, SH, AS>
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
		where SH : class
		where AS : AccountingSession<U, BST, P, R, J, D>
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
			[Required]
			public FundsTransferEvent Event { get; set; }

			/// <summary>
			/// Optional stateful object of type <typeparamref name="SO"/> associated with the <see cref="Event"/>.
			/// </summary>
			public SH StateHolder { get; set; }

			/// <summary>
			/// If <see cref="StateHolder"/> is not null, the current state of the <see cref="StateHolder"/>.
			/// </summary>
			public State CurrentState { get; set; }

			/// <summary>
			/// If <see cref="StateHolder"/> is not null, the state transition of the <see cref="StateHolder"/> associated with the <see cref="Event"/>.
			/// </summary>
			public ST StateTransition { get; set; }
		}

		#endregion

		#region Private fields

		private readonly AsyncSequentialMRUCache<string, StatePath> statePathsByCodeNameCache;

		#endregion

		#region Construction

		/// <summary>
		/// Create.
		/// </summary>
		/// <param name="session">The logic session.</param>
		/// <param name="accountingSessionFactory">A factory for creating an accounting session.</param>
		protected WorkflowFundsTransferManager(
			S session,
			Func<D, U, AS> accountingSessionFactory)
			: base(session, accountingSessionFactory)
		{
			statePathsByCodeNameCache = new AsyncSequentialMRUCache<string, StatePath>(LoadStatePathAsync);
		}

		#endregion

		#region Public properties

		/// <summary>
		/// The set of associations of managed funds transfer events with stateful objects
		/// of type <typeparamref name="SO"/>.
		/// </summary>
		public abstract IQueryable<FundsTransferEventAssociation> FundsTransferEventAssociations { get; }

		#endregion

		#region Protected methods

		/// <summary>
		/// Override to execute a state path on a stateful object.
		/// </summary>
		/// <param name="statefulObject">The stateful object.</param>
		/// <param name="statePath">The state path to execute.</param>
		/// <param name="actionArguments">The arguments to the actions of the state path.</param>
		/// <returns>Returns the state transition produced.</returns>
		protected abstract Task<ST> ExecuteStatePathAsync(SO statefulObject, StatePath statePath, Dictionary<string, object> actionArguments);

		/// <summary>
		/// Convert a state holder to a stateful object.
		/// </summary>
		/// <param name="stateHolder">The stateful object.</param>
		/// <returns>Returns the <see cref="IStateful{U, ST}"/> implementation.</returns>
		protected abstract SO GetStatefulObject(SH stateHolder);

		/// <summary>
		/// Decides the state path to execute on a stateful object
		/// when a <see cref="FundsResponseLine"/> arrives for it.
		/// Returns null to indicate that no path should be executed and the that the line should
		/// be consumed directly. Throws an exception to abort normal digestion of the line
		/// and to record a transfer event with <see cref="FundsTransferEvent.ExceptionData"/> set instead.
		/// </summary>
		/// <param name="statefulObject">The stateful object for which to decide the state path.</param>
		/// <param name="stateAfterFundsTransferRequest">The state of the <paramref name="statefulObject"/> right after the funds transfer request.</param>
		/// <param name="fundsResponseLine">The batch line arriving for the stateful object.</param>
		/// <param name="fundsTransferRequest">The funds transfer request being handled.</param>
		/// <returns>Returns the code name of the path to execute or null to execute none.</returns>
		/// <exception cref="Exception">
		/// Thrown to record a funds transfer event with its <see cref="FundsTransferEvent.ExceptionData"/>
		/// containing the thrown exception.
		/// </exception>
		protected abstract string TrySpecifyNextStatePath(
			SO statefulObject,
			State stateAfterFundsTransferRequest,
			FundsResponseLine fundsResponseLine,
			FundsTransferRequest fundsTransferRequest);

		/// <summary>
		/// Eager fetch any objects related to the state holder objects in order to avoid lazy loading during workflow execution.
		/// </summary>
		/// <param name="stateHolders">The set of entities which will undergo workflow execution.</param>
		protected virtual Task EagerFetchStateHoldersAsync(IQueryable<SH> stateHolders) => Task.CompletedTask;

		/// <summary>
		/// Digest a funds transfer response file from a credit system
		/// and execute the appropriate state paths
		/// as specified by the <see cref="TrySpecifyNextStatePath(SO, State, FundsResponseLine, FundsTransferRequest)"/> method
		/// on the corresponding stateful objects.
		/// The existence of the credit system and
		/// the collation specified in <paramref name="file"/> is assumed.
		/// </summary>
		/// <param name="file">The file to digest.</param>
		/// <param name="responseBatchMessage">The batch message where the generated funds transfer events will be assigned.</param>
		/// <returns>
		/// Returns a collection of results describing the execution outcome of the
		/// contents of the <paramref name="file"/> or an empty collection if the file is not relevant to this manager.
		/// </returns>
		/// <remarks>
		/// Each action in paths being executed will receive an arguments dictionary
		/// containing at least the key <see cref="StandardArgumentKeys.BillingItem"/> set with value
		/// of type <see cref="FundsResponseLine"/>.
		/// </remarks>
		protected internal override async Task<IReadOnlyCollection<FundsResponseResult>> DigestResponseFileAsync(
			FundsResponseFile file,
			FundsTransferBatchMessage responseBatchMessage)
		{
			if (file == null) throw new ArgumentNullException(nameof(file));

			long[] lineIDs = file.Items.Select(i => i.LineID).ToArray();

			var fundsTransferEventAssociations = this.FundsTransferEventAssociations;

			var stateHolders = from a in fundsTransferEventAssociations
												 where a != null
												 select a.StateHolder;

			await EagerFetchStateHoldersAsync(stateHolders);

			var associationsQuery = from a in fundsTransferEventAssociations
															where a.Event.Type == FundsTransferEventType.Pending
															let ftr = a.Event.Request
															where lineIDs.Contains(ftr.GroupID) && ftr.BatchID == file.BatchID
															select new
															{
																Request = ftr,
																ftr.Events,
																ftr.MainAccount,
																ftr.TransferAccount,
																a.StateHolder,
																a.CurrentState,
																StateAfterRequest = a.StateTransition != null ? a.StateTransition.Path.NextState : null
															};

			var associations = await associationsQuery.ToArrayAsync();

			if (associations.Length == 0) return emptyFundsResponseResults;

			var responseResults = new List<FundsResponseResult>(associations.Length);

			var itemsByLineID = 
				file.Items
				.OrderBy(i => i.Time)
				.ToSequentialReadOnlyMultiDictionary(i => i.LineID);

			foreach (var association in associations)
			{
				foreach (var item in itemsByLineID[association.Request.GroupID])
				{
					var statefulObject = GetStatefulObject(association.StateHolder);

					var fundsResponseResult =
						await DigestResponseItemAsync(
							file,
							item,
							association.Request,
							responseBatchMessage,
							statefulObject,
							association.StateAfterRequest);

					responseResults.Add(fundsResponseResult);
				}
			}

			return responseResults;
		}

		/// <summary>
		/// Digestion of a manual line in a batch.
		/// </summary>
		/// <param name="line">The line to accept.</param>
		/// <returns>
		/// Returns the collection of the results which correspond to the 
		/// funds transfer requests grouped in the line or an empty collection if the file is not relevant to this manager.
		/// </returns>
		protected internal override async Task<IReadOnlyCollection<FundsResponseResult>> DigestResponseLineAsync(FundsResponseLine line)
		{
			if (line == null) throw new ArgumentNullException(nameof(line));

			var associationsQuery = from a in this.FundsTransferEventAssociations
															where a.Event.Type == FundsTransferEventType.Pending
															let ftr = a.Event.Request
															where ftr.GroupID == line.LineID && ftr.BatchID == line.BatchID
															select new
															{
																Request = ftr,
																ftr.Events,
																a.StateHolder,
																a.CurrentState,
																StateAfterRequest = a.StateTransition != null ? a.StateTransition.Path.NextState : null
															};

			var associations = await associationsQuery.ToArrayAsync();

			if (associations.Length == 0) return emptyFundsResponseResults;

			var responseResults = new List<FundsResponseResult>(associations.Length);

			foreach (var association in associations)
			{
				var statefulObject = GetStatefulObject(association.StateHolder);

				var fundsResponseResult =
					await DigestResponseLineAsync(
						association.Request,
						line,
						statefulObject,
						association.StateAfterRequest);

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

		private async Task<FundsResponseResult> DigestResponseItemAsync(
			FundsResponseFile file,
			FundsResponseFileItem item,
			FundsTransferRequest fundsTransferRequest,
			FundsTransferBatchMessage responseBatchMessage,
			SO statefulObject,
			State stateAfterRequest)
		{
			if (file == null) throw new ArgumentNullException(nameof(file));
			if (item == null) throw new ArgumentNullException(nameof(item));
			if (fundsTransferRequest == null) throw new ArgumentNullException(nameof(fundsTransferRequest));

			var line = new FundsResponseLine(file, item, responseBatchMessage.ID);

			return await DigestResponseLineAsync(fundsTransferRequest, line, statefulObject, stateAfterRequest);
		}

		private async Task<FundsResponseResult> DigestResponseLineAsync(
			FundsTransferRequest fundsTransferRequest,
			FundsResponseLine line,
			SO statefulObject,
			State stateAfterRequest)
		{
			if (fundsTransferRequest == null) throw new ArgumentNullException(nameof(fundsTransferRequest));
			if (line == null) throw new ArgumentNullException(nameof(line));

			if (statefulObject == null || stateAfterRequest == null)
			{
				return await DigestResponseLineAsync(fundsTransferRequest, line);
			}

			var eventType = GetEventTypeFromResponseLine(line);

			var previousEvent = TryGetExistingDigestedFundsTransferEvent(fundsTransferRequest, eventType, line.ResponseCode);

			if (previousEvent != null)
			{
				return new FundsResponseResult
				{
					Event = previousEvent,
					Line = line,
					IsAlreadyDigested = true
				};
			}

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

				// Attempt to get the next path to be executed. Any exception will be recorded in a funds transfer event with ExceptionData.

				string statePathCodeName = TrySpecifyNextStatePath(statefulObject, stateAfterRequest, line, fundsTransferRequest);

				if (statePathCodeName != null) // Should a path be executed?
				{
					var statePath = await statePathsByCodeNameCache.Get(statePathCodeName);

					using (var transaction = this.DomainContainer.BeginTransaction())
					{
						var transition = await ExecuteStatePathAsync(
							statefulObject,
							statePath,
							actionArguments);

						fundsResponseResult.Event = transition.FundsTransferEvent;

						R remittance = null;

						if (transition.FundsTransferEventID.HasValue)
						{
							remittance = await this.DomainContainer.Remittances.SingleOrDefaultAsync(r => r.FundsTransferEventID == transition.FundsTransferEventID);
						}

						if (transition.FundsTransferEvent != null)
							await OnResponseLineDigestionSuccessAsync(line, transition.FundsTransferEvent, remittance);

						await transaction.CommitAsync();
					}
				}
				else // If no path is specified, record the event directly.
				{
					using (var accountingSession = CreateAccountingSession())
					using (GetElevatedAccessScope())
					using (var transaction = this.DomainContainer.BeginTransaction())
					{
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

						var remittance = TryGetTransferRemittance(directActionResult);

						await OnResponseLineDigestionSuccessAsync(line, directActionResult.FundsTransferEvent, remittance);

						await transaction.CommitAsync();
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
