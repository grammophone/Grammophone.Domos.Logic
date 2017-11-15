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
	/// <typeparam name="P">The type of posting, derived from <see cref="Posting{U}"/>.</typeparam>
	/// <typeparam name="R">The type of remittance, derived from <see cref="Remittance{U}"/>.</typeparam>
	/// <typeparam name="J">The type of journal, derived from <see cref="Journal{U, BST, P, R}"/>.</typeparam>
	/// <typeparam name="D">The type of domain container, derived from <see cref="IDomosDomainContainer{U, BST, P, R, J}"/>.</typeparam>
	/// <typeparam name="S">The type of session, derived from <see cref="LogicSession{U, D}"/>.</typeparam>
	/// <typeparam name="ST">The type of state transition, derived from <typeparamref name="BST"/></typeparam>
	/// <typeparam name="SO">The type of stateful object, derived from <see cref="IStateful{U, ST}"/>.</typeparam>
	public abstract class FundsTransferInitiationAction<U, BST, P, R, J, D, S, ST, SO>
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
		/// Create a funds transfer request using 
		/// the <see cref="CreateFundsTransferRequestAsync(D, U, SO, DateTime, string, Guid?, Guid?)"/>.
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

			Guid? batchID = GetBatchID(actionArguments);

			Guid? collationID = GetCollationID(actionArguments);

			using (var transaction = domainContainer.BeginTransaction())
			{
				ElevateTransactionAccessRights(session, transaction); // Needed because accounting touches private data.

				var queueingEvent = await CreateFundsTransferRequestAsync(
					domainContainer,
					session.User,
					stateful,
					DateTime.UtcNow,
					transactionID,
					batchID,
					collationID);

				if (queueingEvent != null)
				{
					stateTransition.FundsTransferEvent = queueingEvent;
				}

				await transaction.CommitAsync();
			}
		}

		/// <summary>
		/// Returns the specification list containing parameters for
		/// an optional batch ID and an optional collation ID for the funds transfer queueing event.
		/// </summary>
		public override IEnumerable<ParameterSpecification> GetParameterSpecifications()
		{
			yield return new ParameterSpecification(
				StandardArgumentKeys.BatchID,
				false,
				FundsTransferInitiationActionResources.BATCH_ID_CAPTION,
				FundsTransferInitiationActionResources.BATCH_ID_DESCRIPTION,
				typeof(Guid));

			yield return new ParameterSpecification(
				StandardArgumentKeys.CollationID,
				false,
				FundsTransferInitiationActionResources.COLLATION_ID_CAPTION,
				FundsTransferInitiationActionResources.COLLATION_ID_DESCRIPTION,
				typeof(Guid));
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
		/// <param name="queueEventCollationID">The optional ID of the collation of the funds transfer queuing event.</param>
		/// <returns>
		/// Returns the queuing event of the created transfer request.
		/// </returns>
		protected abstract Task<FundsTransferEvent> CreateFundsTransferRequestAsync(
			D domainContainer,
			U user,
			SO stateful,
			DateTime utcDate,
			string transactionID,
			Guid? batchID,
			Guid? queueEventCollationID);

		/// <summary>
		/// Get the batch ID from the action arguments or null if not specified.
		/// </summary>
		/// <param name="actionArguments">The action arguments.</param>
		/// <returns>Returns the batch ID ot null if not given.</returns>
		protected Guid? GetBatchID(IDictionary<string, object> actionArguments)
			=> GetOptionalParameterValue<Guid>(actionArguments, StandardArgumentKeys.BatchID);

		/// <summary>
		/// Get the collation ID from the action arguments or null if not specified.
		/// </summary>
		/// <param name="actionArguments">The action arguments.</param>
		/// <returns>Returns the collation ID ot null if not given.</returns>
		protected Guid? GetCollationID(IDictionary<string, object> actionArguments)
			=> GetOptionalParameterValue<Guid>(actionArguments, StandardArgumentKeys.CollationID);

		#endregion
	}
}
