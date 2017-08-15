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
	/// <typeparam name="B">The type of billing item.</typeparam>
	/// <remarks>
	/// This action expects a billing item of type <typeparamref name="B"/> in arguments
	/// key <see cref="StandardArgumentKeys.BillingItem"/> and an
	/// optional <see cref="DateTime"/> in key <see cref="StandardArgumentKeys.Date"/>.
	/// If the latter is missing, the current UTC time is used.
	/// Warning: The <see cref="ExecuteAsync(S, D, SO, ST, IDictionary{string, object})"/> method implementation
	/// elevates the rights of any existing outer transaction.
	/// </remarks>
	public abstract class AccountingAction<U, BST, P, R, J, D, S, ST, SO, B>
		: WorkflowAction<U, D, S, ST, SO>
		where U : User
		where BST : StateTransition<U>
		where P : Posting<U>
		where R : Remittance<U>
		where J : Journal<U, BST, P, R>
		where D : IDomosDomainContainer<U, BST, P, R, J>
		where S : LogicSession<U, D>
		where ST : BST
		where SO : IStateful<U, ST>
	{
		#region Public methods

		/// <summary>
		/// Consumes the billing item of type <typeparamref name="B"/> in arguments
		/// key <see cref="StandardArgumentKeys.BillingItem"/> and the <see cref="DateTime"/>
		/// in key <see cref="StandardArgumentKeys.Date"/> and performs the accounting
		/// using method <see cref="ExecuteAccountingAsync(D, U, SO, DateTime, B)"/>.
		/// </summary>
		public override async Task ExecuteAsync(
			S session,
			D domainContainer,
			SO stateful,
			ST stateTransition,
			IDictionary<string, object> actionArguments)
		{
			if (session == null) throw new ArgumentNullException(nameof(session));
			if (domainContainer == null) throw new ArgumentNullException(nameof(domainContainer));
			if (stateful == null) throw new ArgumentNullException(nameof(stateful));
			if (stateTransition == null) throw new ArgumentNullException(nameof(stateTransition));
			if (actionArguments == null) throw new ArgumentNullException(nameof(actionArguments));

			DateTime date = GetDate(actionArguments);

			var billingItem = GetBillingItem(actionArguments);

			using (var transaction = domainContainer.BeginTransaction())
			{
				ElevateTransactionAccessRights(session, transaction); // Needed because accounting touches private data.

				var result = await ExecuteAccountingAsync(
					domainContainer,
					session.User,
					stateful,
					date,
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

		/// <summary>
		/// Indicate that this action expects a required 
		/// parameter of type <typeparamref name="B"/> under
		/// key <see cref="StandardArgumentKeys.BillingItem"/>
		/// and an optional one of type <see cref="DateTime"/> under 
		/// key <see cref="StandardArgumentKeys.Date"/>.
		/// </summary>
		public override IEnumerable<ParameterSpecification> GetParameterSpecifications()
		{
			yield return new ParameterSpecification(
				StandardArgumentKeys.BillingItem,
				true,
				AccountingActionResources.BILLING_ITEM_CAPTION,
				AccountingActionResources.BILLING_ITEM_DESCRIPTION,
				typeof(B));

			yield return new ParameterSpecification(
				StandardArgumentKeys.Date,
				false,
				AccountingActionResources.DATE_CAPTION,
				AccountingActionResources.DATE_DESCRIPTION,
				typeof(DateTime),
				new ValidationAttribute[] { new DataTypeAttribute(DataType.DateTime) });
		}

		#endregion

		#region Protected methods

		/// <summary>
		/// Override to consume the billing item and return an accounting result. 
		/// This method receives
		/// a <paramref name="domainContainer"/> with elevated access rights,
		/// suitable for accounting actions which typically touch private data.
		/// </summary>
		/// <param name="domainContainer">The domain container.</param>
		/// <param name="user">The acting user.</param>
		/// <param name="stateful">The stateful object.</param>
		/// <param name="utcDate">The date, in UTC.</param>
		/// <param name="billingItem">The billing item.</param>
		/// <returns>
		/// Returns the result of the accounting action.
		/// </returns>
		protected abstract Task<AccountingSession<U, BST, P, R, J, D>.ActionResult> ExecuteAccountingAsync(
			D domainContainer,
			U user,
			SO stateful,
			DateTime utcDate,
			B billingItem);

		/// <summary>
		/// Get the billing item from the action arguments.
		/// </summary>
		/// <exception cref="ArgumentException">
		/// Thrown when there is no entry for the billing item
		/// in the <paramref name="actionArguments"/>.
		/// </exception>
		protected B GetBillingItem(IDictionary<string, object> actionArguments)
			=> GetParameterValue<B>(actionArguments, StandardArgumentKeys.BillingItem, true);

		/// <summary>
		/// Get the batch date from action arguments, if it exists, else return the
		/// current date in UTC.
		/// </summary>
		protected DateTime GetDate(IDictionary<string, object> actionArguments)
		{
			if (actionArguments == null) throw new ArgumentNullException(nameof(actionArguments));

			if (actionArguments.ContainsKey(StandardArgumentKeys.Date))
			{
				DateTime date = GetParameterValue<DateTime>(actionArguments, StandardArgumentKeys.Date, true);

				if (date.Kind != DateTimeKind.Utc)
					return DateTime.SpecifyKind(date, DateTimeKind.Utc);
				else
					return date;
			}
			else
			{
				return DateTime.UtcNow;
			}
		}

		#endregion
	}
}
