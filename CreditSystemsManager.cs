using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Grammophone.Domos.DataAccess;
using Grammophone.Domos.Domain;
using Grammophone.Domos.Domain.Accounting;
using Grammophone.Domos.Domain.Workflow;

namespace Grammophone.Domos.Logic
{
	/// <summary>
	/// Manager for <see cref="CreditSystem"/>s.
	/// </summary>
	public class CreditSystemsManager<U, BST, P, R, J, D, S> : Manager<U, D, S>
		where U : User
		where BST : StateTransition<U>
		where P : Posting<U>
		where R : Remittance<U>
		where J : Journal<U, BST, P, R>
		where D : IDomosDomainContainer<U, BST, P, R, J>
		where S : LogicSession<U, D>
	{
		#region Construction

		/// <summary>
		/// Create.
		/// </summary>
		/// <param name="session">The logic session.</param>
		protected internal CreditSystemsManager(S session) : base(session)
		{
		}

		#endregion

		#region Public properties

		/// <summary>
		/// The available credit systems.
		/// </summary>
		public IQueryable<CreditSystem> CreditSystems => this.DomainContainer.CreditSystems;

		#endregion

		#region Public methods

		/// <summary>
		/// Delete a <see cref="CreditSystem"/>.
		/// </summary>
		/// <param name="creditSystemID">The ID of the credit system.</param>
		/// <returns>
		/// Returns true when the credit system was found and deleted, else false if not found.
		/// </returns>
		public async Task<bool> DeleteCreditSystemAsync(long creditSystemID)
		{
			var creditSystem = await this.DomainContainer.CreditSystems.SingleOrDefaultAsync(cs => cs.ID == creditSystemID);

			if (creditSystem == null) return false;

			using (GetElevatedAccessScope())
			{
				this.DomainContainer.CreditSystems.Remove(creditSystem);

				await this.DomainContainer.SaveChangesAsync();
			}

			return true;
		}

		/// <summary>
		/// Add a <see cref="CreditSystem"/>.
		/// </summary>
		/// <param name="creditSystem">The credit system to add.</param>
		/// <exception cref="ArgumentException">
		/// Thrown when the <see cref="CreditSystem.FundsTransferFileConverterName"/>
		/// if the <paramref name="creditSystem"/>
		/// does not point to a registered <see cref="IFundsTransferFileConverter"/>.
		/// </exception>
		public async Task AddCreditSystemAsync(CreditSystem creditSystem)
		{
			if (creditSystem == null) throw new ArgumentNullException(nameof(creditSystem));

			EnsureCreditSystemIsValid(creditSystem);

			using (GetElevatedAccessScope())
			{
				this.DomainContainer.CreditSystems.Add(creditSystem);

				await this.DomainContainer.SaveChangesAsync();
			}
		}

		/// <summary>
		/// Edit a <see cref="CreditSystem"/>.
		/// </summary>
		/// <param name="creditSystem">The credit system to edit.</param>
		/// <param name="attachAsModified">
		/// If this is true and the credit system is disconnected, 
		/// it is attached with a 'modified' state,
		/// else this parameter has no effect.
		/// </param>
		/// <exception cref="ArgumentException">
		/// Thrown when the <see cref="CreditSystem.FundsTransferFileConverterName"/>
		/// if the <paramref name="creditSystem"/>
		/// does not point to a registered <see cref="IFundsTransferFileConverter"/>.
		/// </exception>
		public async Task EditCreditSystemAsync(CreditSystem creditSystem, bool attachAsModified = false)
		{
			if (creditSystem == null) throw new ArgumentNullException(nameof(creditSystem));

			EnsureCreditSystemIsValid(creditSystem);

			using (GetElevatedAccessScope())
			{
				await UpdateObjectGraphAsync(creditSystem, attachAsModified);
			}
		}

		/// <summary>
		/// Get the registration names of the <see cref="IFundsTransferFileConverter"/> implementations
		/// available in the system. These names are suitable to be set in the <see cref="CreditSystem.FundsTransferFileConverterName"/>
		/// property of <see cref="CreditSystem"/>.
		/// </summary>
		/// <returns>Returns a collection of the names. The default name is not returned, if such a registration exists.</returns>
		public IEnumerable<string> GetFundsTransferFileConvertersNames()
			=> this.SessionSettings.GetRegistrationNames<IFundsTransferFileConverter>().Where(n => n != null);

		/// <summary>
		/// Get all the registered implementations of the <see cref="IFundsTransferFileConverter"/> interface
		/// in a dictionary whose keys are their registration names. These registration names are
		/// suitable to be set in the <see cref="CreditSystem.FundsTransferFileConverterName"/>
		/// property of <see cref="CreditSystem"/>.
		/// </summary>
		/// <returns>
		/// Returns a dictionary of the <see cref="IFundsTransferFileConverter"/> implemtnations keyes by their registration name.
		/// </returns>
		public IReadOnlyDictionary<string, IFundsTransferFileConverter> GetFundsTransferFileConvertersByName()
			=> this.SessionSettings.ResolveAllToDictionary<IFundsTransferFileConverter>();

		#endregion

		#region Protected methods

		/// <summary>
		/// Validate the credit system and ensure that its <see cref="CreditSystem.FundsTransferFileConverterName"/>
		/// points to a registered <see cref="IFundsTransferFileConverter"/> type.
		/// </summary>
		/// <param name="creditSystem">The credit system.</param>
		/// <exception cref="ArgumentException">
		/// Thrown when the <see cref="CreditSystem.FundsTransferFileConverterName"/>
		/// if the <paramref name="creditSystem"/>
		/// does not point to a registered <see cref="IFundsTransferFileConverter"/>.
		/// </exception>
		protected virtual void EnsureCreditSystemIsValid(CreditSystem creditSystem)
		{
			if (creditSystem == null) throw new ArgumentNullException(nameof(creditSystem));

			if (creditSystem.FundsTransferFileConverterName != null)
			{
				if (!this.SessionSettings.IsRegistered<IFundsTransferFileConverter>(creditSystem.FundsTransferFileConverterName))
				{
					throw new ArgumentException(
						$"The name '{creditSystem.FundsTransferFileConverterName}' does not correspond to a registered funds transfer file converter.",
						nameof(creditSystem));
				}
			}
		}

		#endregion
	}
}
