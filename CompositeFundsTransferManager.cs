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

namespace Grammophone.Domos.Logic
{
	/// <summary>
	/// Funds transfer manager which aggrregates funds transfer managers.
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
	/// The type of domain container, derived from <see cref="IDomosDomainContainer{U, BST, P, R, J}"/>.
	/// </typeparam>
	/// <typeparam name="S">
	/// The type of session, derived from <see cref="LogicSession{U, D}"/>.
	/// </typeparam>
	/// <typeparam name="AS">
	/// The type of accounting session, derived from <see cref="AccountingSession{U, BST, P, R, J, D}"/>.
	/// </typeparam>
	public abstract class CompositeFundsTransferManager<U, BST, P, R, J, D, S, AS> : FundsTransferManager<U, BST, P, R, J, D, S, AS>
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
		/// <param name="accountingSessionFactory">A factory for creating an accounting session.</param>
		/// <param name="fundsTransferManagers">The funds transfer managers to compose.</param>
		protected CompositeFundsTransferManager(
			S session,
			Func<D, U, AS> accountingSessionFactory,
			IEnumerable<FundsTransferManager<U, BST, P, R, J, D, S, AS>> fundsTransferManagers)
			: base(session, accountingSessionFactory)
		{
			if (fundsTransferManagers == null) throw new ArgumentNullException(nameof(fundsTransferManagers));

			this.FundsTransferManagers = fundsTransferManagers;
		}

		#endregion

		#region Public properties

		/// <summary>
		/// The funds transfer managers being composed.
		/// </summary>
		public IEnumerable<FundsTransferManager<U, BST, P, R, J, D, S, AS>> FundsTransferManagers { get; }

		/// <summary>
		/// A union of funds transfer requests
		/// </summary>
		public override IQueryable<FundsTransferRequest> FundsTransferRequests
		{
			get
			{
				// If there are no members in FundsTransferManagers property, return the empty set.

				if (!this.FundsTransferManagers.Any()) return this.DomainContainer.FundsTransferRequests.Where(ftr => ftr.ID < 0L);

				// Union the funds transfer requests from the composed managers.

				IQueryable<FundsTransferRequest> fundsTransferRequests = null;

				foreach (var manager in this.FundsTransferManagers)
				{
					if (fundsTransferRequests == null)
						fundsTransferRequests = manager.FundsTransferRequests;
					else
						fundsTransferRequests = fundsTransferRequests.Union(manager.FundsTransferRequests);
				}

				return fundsTransferRequests;
			}
		}

		#endregion

		#region Protected methods

		/// <summary>
		/// Digest a funds response file by feeding it to all <see cref="FundsTransferManagers"/>
		/// for digestion and combining results.
		/// The existence of the credit system and
		/// the collation specified in <paramref name="file"/> is assumed.
		/// </summary>
		/// <param name="file">The file to digest.</param>
		/// <param name="responseBatchMessage">The batch message where the generated funds transfer events will be assigned.</param>
		/// <returns>
		/// Returns a collection of results describing the execution outcome of the
		/// contents of the <paramref name="file"/> or an empty collection if the file is not relevant to this manager.
		/// </returns>
		protected internal override async Task<IReadOnlyCollection<FundsResponseResult>> DigestResponseFileAsync(
			FundsResponseFile file,
			FundsTransferBatchMessage responseBatchMessage)
		{
			if (file == null) throw new ArgumentNullException(nameof(file));
			if (responseBatchMessage == null) throw new ArgumentNullException(nameof(responseBatchMessage));

			var fundsResponseResults = Enumerable.Empty<FundsResponseResult>();

			foreach (var manager in this.FundsTransferManagers)
			{
				var managerResults = await manager.DigestResponseFileAsync(file, responseBatchMessage);

				await manager.PostProcessLinesAsync(file.BatchID, managerResults, responseBatchMessage.ID);

				fundsResponseResults.Concat(managerResults);
			}

			return fundsResponseResults.ToArray();
		}

		/// <summary>
		/// Digestion of a manual line in a batch by feeding it to all <see cref="FundsTransferManagers"/>
		/// for digestion and combining results.
		/// </summary>
		/// <param name="line">The line to accept.</param>
		/// <returns>
		/// Returns the collection of the results which correspond to the 
		/// funds transfer requests grouped in the line.
		/// </returns>
		protected internal override async Task<IReadOnlyCollection<FundsResponseResult>> DigestResponseLineAsync(FundsResponseLine line)
		{
			if (line == null) throw new ArgumentNullException(nameof(line));

			var fundsResponseResults = Enumerable.Empty<FundsResponseResult>();

			foreach (var manager in this.FundsTransferManagers)
			{
				var managerResults = await manager.DigestResponseLineAsync(line);

				await manager.PostProcessLinesAsync(line.BatchID, managerResults, line.BatchMessageID);

				fundsResponseResults.Concat(managerResults);
			}

			return fundsResponseResults.ToArray();
		}

		#endregion
	}
}
