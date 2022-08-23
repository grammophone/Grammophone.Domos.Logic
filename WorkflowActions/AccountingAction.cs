using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Grammophone.Domos.Accounting;
using Grammophone.Domos.DataAccess;
using Grammophone.Domos.Domain;
using Grammophone.Domos.Domain.Accounting;
using Grammophone.Domos.Domain.Workflow;

namespace Grammophone.Domos.Logic.WorkflowActions
{
	/// <summary>
	/// Base for actions consuming a billing item
	/// into the accounting records.
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
	/// <typeparam name="B">The type of billing item.</typeparam>
	/// <remarks>
	/// This action expects a billing item of type <typeparamref name="B"/> in arguments
	/// key <see cref="StandardArgumentKeys.BillingItem"/>.
	/// If the latter is missing, the current UTC time is used.
	/// Warning: The <see cref="ExecuteAsync(S, D, SO, ST, IDictionary{string, object}, IDictionary{string, object})"/> method implementation
	/// elevates the rights of any existing outer transaction.
	/// </remarks>
	public abstract class AccountingAction<U, BST, P, R, J, D, S, ST, SO, AS, B>
		: WorkflowAction<U, D, S, ST, SO>
		where U : User
		where BST : StateTransition<U>
		where P : Posting<U>
		where R : Remittance<U>
		where J : Journal<U, BST, P, R>
		where D : IDomosDomainContainer<U, BST, P, R, J>
		where S : LogicSession<U, D>
		where ST : BST
		where AS : AccountingSession<U, BST, P, R, J, D>
		where SO : IStateful<U, ST>
	{
		#region Public methods

		/// <summary>
		/// Consumes the billing item of type <typeparamref name="B"/> in arguments
		/// key <see cref="StandardArgumentKeys.BillingItem"/> and performs the accounting
		/// using method <see cref="ExecuteAccountingAsync(AS, SO, ST, B)"/>.
		/// </summary>
		public override async Task ExecuteAsync(
			S session,
			D domainContainer,
			SO stateful,
			ST stateTransition,
			IDictionary<string, object> actionArguments, IDictionary<string, object> context)
		{
			if (session == null) throw new ArgumentNullException(nameof(session));
			if (domainContainer == null) throw new ArgumentNullException(nameof(domainContainer));
			if (stateful == null) throw new ArgumentNullException(nameof(stateful));
			if (stateTransition == null) throw new ArgumentNullException(nameof(stateTransition));
			if (actionArguments == null) throw new ArgumentNullException(nameof(actionArguments));

			B billingItem = GetBillingItem(actionArguments);

			using (var transaction = domainContainer.BeginTransaction())
			{
				ElevateTransactionAccessRights(session, transaction); // Needed because accounting touches private data.

				using (var accountingSession = CreateAccountingSession(domainContainer, session.User))
				{
					var result = await ExecuteAccountingAsync(
						accountingSession,
						stateful,
						stateTransition,
						billingItem);

					if (result.Journal != null)
					{
						result.Journal.StateTransition = stateTransition;
					}

					if (result.FundsTransferEvent != null)
					{
						stateTransition.FundsTransferEvent = result.FundsTransferEvent;
					}

					await transaction.CommitAsync();
				}
			}
		}

		/// <summary>
		/// Indicate that this action expects a required 
		/// parameter of type <typeparamref name="B"/> under
		/// key <see cref="StandardArgumentKeys.BillingItem"/>.
		/// </summary>
		public override IEnumerable<ParameterSpecification> GetParameterSpecifications()
		{
			yield return new ParameterSpecification(
				StandardArgumentKeys.BillingItem,
				true,
				AccountingActionResources.BILLING_ITEM_CAPTION,
				AccountingActionResources.BILLING_ITEM_DESCRIPTION,
				typeof(B));
		}

		#endregion

		#region Protected methods

		/// <summary>
		/// Override to consume the billing item and return an accounting result. 
		/// </summary>
		/// <param name="accountingSession">The accounting session in use.</param>
		/// <param name="stateful">The stateful object for which the workflow action runs.</param>
		/// <param name="stateTransition">The state transition being produced.</param>
		/// <param name="billingItem">The billing item.</param>
		/// <returns>
		/// Returns the result of the accounting action.
		/// </returns>
		protected abstract Task<AccountingSession<U, BST, P, R, J, D>.ActionResult> ExecuteAccountingAsync(
			AS accountingSession,
			SO stateful,
			ST stateTransition,
			B billingItem);

		/// <summary>
		/// Override to provide the construction of the accounting session.
		/// </summary>
		/// <param name="domainContainer">The domain container in use.</param>
		/// <param name="agent">The user running the action, which will be the agent of the accounting.</param>
		/// <returns>Returns the approptiate accounting session implementation.</returns>
		protected abstract AS CreateAccountingSession(D domainContainer, U agent);

		/// <summary>
		/// Get the billing item from the action arguments.
		/// </summary>
		/// <exception cref="ArgumentException">
		/// Thrown when there is no entry for the billing item
		/// in the <paramref name="actionArguments"/>.
		/// </exception>
		protected B GetBillingItem(IDictionary<string, object> actionArguments)
			=> GetParameterValue<B>(actionArguments, StandardArgumentKeys.BillingItem);

		#endregion
	}
}
