using System;
using System.Collections.Generic;
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
	/// Base for actions initiating a funds transfer.
	/// </summary>
	/// <typeparam name="U">The type of user.</typeparam>
	/// <typeparam name="BST">The base type of state transitions, derived from <see cref="StateTransition{U}"/>.</typeparam>
	/// <typeparam name="A">The base type of account, derived from <see cref="Account{U}"/>.</typeparam>
	/// <typeparam name="P">The type of posting, derived from <see cref="Posting{U, A}"/>.</typeparam>
	/// <typeparam name="R">The type of remittance, derived from <see cref="Remittance{U, A}"/>.</typeparam>
	/// <typeparam name="J">The type of journal, derived from <see cref="Journal{U, BST, A, P, R}"/>.</typeparam>
	/// <typeparam name="D">The type of domain container, derived from <see cref="IDomosDomainContainer{U, BST, A, P, R, J}"/>.</typeparam>
	/// <typeparam name="S">The type of session, derived from <see cref="Session{U, D}"/>.</typeparam>
	/// <typeparam name="ST">The type of state transition, derived from <typeparamref name="BST"/></typeparam>
	/// <typeparam name="SO">The type of stateful object, derived from <see cref="IStateful{U, ST}"/>.</typeparam>
	public abstract class FundsTransferInitiationAction<U, BST, A, P, R, J, D, S, ST, SO>
		: WorkflowAction<U, D, S, ST, SO>
		where U : User
		where BST : StateTransition<U>
		where A : Account<U>
		where P : Posting<U, A>
		where R : Remittance<U, A>
		where J : Journal<U, BST, A, P, R>
		where D : IDomosDomainContainer<U, BST, A, P, R, J>
		where S : Session<U, D>
		where ST : BST
		where SO : IStateful<U, ST>
	{
		#region Public methods

		/// <summary>
		/// Create a funds transfer request using 
		/// the <see cref="CreateFundsTransferRequestAsync(D, U, SO, DateTime, string, string)"/>.
		/// method.
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

			string transactionID = Guid.NewGuid().ToString("D");

			string batchID = GetBatchID(actionArguments);

			using (var transaction = domainContainer.BeginTransaction())
			{
				ElevateTransactionAccessRights(session, transaction); // Needed because accounting touches private data.

				var queueingEvent = await CreateFundsTransferRequestAsync(
					domainContainer,
					session.User,
					stateful,
					DateTime.UtcNow,
					transactionID,
					batchID);

				if (queueingEvent != null)
				{
					stateTransition.FundsTransferEvent = queueingEvent;
				}

				await transaction.CommitAsync();
			}
		}

		/// <summary>
		/// Returns the specification list containing a single parameter for
		/// an optional batch ID.
		/// </summary>
		public override IEnumerable<ParameterSpecification> GetParameterSpecifications()
		{
			yield return new ParameterSpecification(
				StandardArgumentKeys.BatchID,
				false,
				FundsTransferInitiationActionResources.BATCH_ID_CAPTION,
				FundsTransferInitiationActionResources.BATCH_ID_DESCRIPTION,
				typeof(string));
		}

		#endregion

		#region Protected methods

		/// <summary>
		/// Override to create the funds transfer request for the action.
		/// </summary>
		/// <param name="domainContainer">The domain container.</param>
		/// <param name="user">The acting user.</param>
		/// <param name="stateful">The stateful object for which to create the funds transfer request.</param>
		/// <param name="utcDate">The date, in UTC.</param>
		/// <param name="transactionID">The ID of the transaction.</param>
		/// <param name="batchID">The optional batch ID.</param>
		/// <returns>
		/// Returns the queuing event of the created transfer request.
		/// </returns>
		protected abstract Task<FundsTransferEvent> CreateFundsTransferRequestAsync(
			D domainContainer,
			U user,
			SO stateful,
			DateTime utcDate,
			string transactionID,
			string batchID = null);

		/// <summary>
		/// Get the batch ID from the action arguments or null if not specified.
		/// </summary>
		/// <param name="actionArguments">The action arguments.</param>
		/// <returns>Returns the batch ID ot null if not given.</returns>
		protected string GetBatchID(IDictionary<string, object> actionArguments)
			=> GetParameterValue<string>(actionArguments, StandardArgumentKeys.BatchID, false);

		#endregion
	}
}
