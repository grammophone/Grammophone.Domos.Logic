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

namespace Grammophone.Domos.Logic.WorkflowActions
{
	/// <summary>
	/// Accounting action consuming a <see cref="FundsResponseLine"/> as a
	/// billing item.
	/// </summary>
	/// <typeparam name="U">The type of user.</typeparam>
	/// <typeparam name="BST">The base type of state transitions, derived from <see cref="StateTransition{U}"/>.</typeparam>
	/// <typeparam name="P">The type of posting, derived from <see cref="Posting{U}"/>.</typeparam>
	/// <typeparam name="R">The type of remittance, derived from <see cref="Remittance{U}"/>.</typeparam>
	/// <typeparam name="J">The type of journal, derived from <see cref="Journal{U, BST, P, R}"/>.</typeparam>
	/// <typeparam name="D">The type of domain container, derived from <see cref="IDomosDomainContainer{U, BST, P, R, J}"/>.</typeparam>
	/// <typeparam name="S">The type of session, derived from <see cref="LogicSession{U, D}"/>.</typeparam>
	/// <typeparam name="ST">The type of state transition, derived from <typeparamref name="BST"/></typeparam>
	/// <typeparam name="SO">The type of stateful object, derived from <see cref="IStateful{U, ST}"/>.</typeparam>
	/// <typeparam name="AS">
	/// The type of accounting session to be used, derived from <see cref="AccountingSession{U, BST, P, R, J, D}"/>.
	/// </typeparam>
	public abstract class FundsTransferResponseAction<U, BST, P, R, J, D, S, ST, SO, AS>
		: AccountingAction<U, BST, P, R, J, D, S, ST, SO, AS, FundsResponseLine>
		where U : User
		where BST : StateTransition<U>
		where P : Posting<U>
		where R : Remittance<U>
		where J : Journal<U, BST, P, R>
		where D : IDomosDomainContainer<U, BST, P, R, J>
		where S : LogicSession<U, D>
		where ST : BST
		where SO : IStateful<U, ST>
		where AS : AccountingSession<U, BST, P, R, J, D>
	{
		#region Protected methods

		/// <summary>
		/// Consumes a <see cref="FundsResponseLine"/> as a billing item by calling
		/// <see cref="AccountingSession{U, BST, P, R, J, D}.AddFundsTransferEventAsync(FundsTransferRequest, DateTime, FundsTransferEventType, Func{J, Task}, long?, string, string, string, Exception)"/>
		/// and, when the <see cref="FundsResponseLine.Status"/> is <see cref="FundsResponseStatus.Succeeded"/>,
		/// appending to the resulting journal by calling <see cref="AppendToJournalAsync(D, SO, J, FundsResponseLine, U)"/>.
		/// </summary>
		/// <param name="accountingSession">
		/// The accounting session, as created
		/// via <see cref="AccountingAction{U, BST, P, R, J, D, S, ST, SO, AS, B}.CreateAccountingSession(D, U)"/>.
		/// </param>
		/// <param name="stateful">The stateful object for which the workflow action runs.</param>
		/// <param name="stateTransition">The state transition being produced.</param>
		/// <param name="billingItem">The <see cref="FundsResponseLine"/>.</param>
		/// <returns></returns>
		protected override async Task<AccountingSession<U, BST, P, R, J, D>.ActionResult> ExecuteAccountingAsync(
			AS accountingSession,
			SO stateful,
			ST stateTransition,
			FundsResponseLine billingItem)
		{
			if (accountingSession == null) throw new ArgumentNullException(nameof(accountingSession));
			if (stateful == null) throw new ArgumentNullException(nameof(stateful));
			if (billingItem == null) throw new ArgumentNullException(nameof(billingItem));

			D domainContainer = accountingSession.DomainContainer;

			var fundsTransferRequest = await 
				domainContainer.FundsTransferRequests
				.Include(r => r.Batch.Messages)
				.SingleOrDefaultAsync(r => r.ID == billingItem.LineID);

			if (fundsTransferRequest == null)
				throw new UserException(FundsTransferResponseActionResources.INVALID_FUNDS_REQUEST);

			FundsTransferEventType eventType;

			switch (billingItem.Status)
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
					throw new LogicException($"Unexpected funds transfer line status: '{billingItem.Status}'.");
			}

			// Local function to enclose arguments and guard event type.
			async Task AppendToJournalFunctionAsync(J journal)
			{
				if (eventType != FundsTransferEventType.Succeeded) return;

				await AppendToJournalAsync(domainContainer, stateful, journal, billingItem, accountingSession.Agent);
			}

			return await accountingSession.AddFundsTransferEventAsync(
				fundsTransferRequest,
				billingItem.Time,
				eventType,
				AppendToJournalFunctionAsync,
				billingItem.BatchMessageID,
				billingItem.ResponseCode,
				billingItem.TraceCode,
				billingItem.Comments);
		}

		/// <summary>
		/// Override to enroll to the accounting actions any extra journal lines when
		/// the <see cref="FundsResponseLine.Status"/> of the <paramref name="fundsResponseLine"/>
		/// is <see cref="FundsResponseStatus.Succeeded"/>. Default implementation does nothing.
		/// </summary>
		/// <param name="domainContainer">The domain container in use.</param>
		/// <param name="stateful">The stateful object for which the workflow action runs.</param>
		/// <param name="journal">The journal to append to.</param>
		/// <param name="fundsResponseLine">The funds response line being consumed.</param>
		/// <param name="agent">The user agent running the action.</param>
		protected virtual Task AppendToJournalAsync(D domainContainer, SO stateful, J journal, FundsResponseLine fundsResponseLine, U agent)
			=> Task.FromResult(0);

		#endregion
	}
}
