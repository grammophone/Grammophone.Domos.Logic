using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Grammophone.Domos.Accounting;
using Grammophone.Domos.Accounting.Models;
using Grammophone.Domos.Domain.Accounting;

namespace Grammophone.Domos.Logic.Models.FundsTransfer
{
	/// <summary>
	/// A line in a <see cref="FundsRequestFile"/>.
	/// </summary>
	[Serializable]
	public class FundsRequestFileItem
	{
		#region Private fields

		/// <summary>
		/// Backing field for the <see cref="BankAccountInfo"/> property.
		/// </summary>
		private BankAccountInfo bankAccountInfo;

		#endregion

		#region Construction

		/// <summary>
		/// Create.
		/// </summary>
		public FundsRequestFileItem()
		{
		}

		/// <summary>
		/// Create from a <see cref="FundsTransferEvent"/>, which must have <see cref="FundsTransferEvent.BatchMessage"/> set
		/// and <see cref="FundsTransferEvent.Type"/> equal to <see cref="FundsTransferEventType.Pending"/>.
		/// </summary>
		/// <param name="transferEvent">The funds transfer event.</param>
		/// <exception cref="ArgumentException">
		/// The event is not assigned in a batch message.
		/// </exception>
		/// <exception cref="LogicException">
		/// The event has <see cref="FundsTransferEvent.Type"/> other
		/// than <see cref="FundsTransferEventType.Pending"/>.
		/// </exception>
		public FundsRequestFileItem(FundsTransferEvent transferEvent)
		{
			if (transferEvent == null) throw new ArgumentNullException(nameof(transferEvent));

			if (transferEvent.BatchMessage == null)
				throw new ArgumentException($"The event is not assigned in a batch message.", nameof(transferEvent));

			if (transferEvent.Type != FundsTransferEventType.Pending)
				throw new LogicException($"The event has type '{transferEvent.Type}' instead of '{FundsTransferEventType.Pending}'.");

			this.RequestID = transferEvent.RequestID;
			this.Amount = transferEvent.Request.Amount;
			this.BankAccountInfo = transferEvent.Request.EncryptedBankAccountInfo.Decrypt();
		}

		#endregion

		#region Public properties

		/// <summary>
		/// The ID of the external system transaction.
		/// </summary>
		[XmlAttribute]
		[Display(
			Name = nameof(FundsRequestFileItemResources.RequestID_Name),
			ResourceType = typeof(FundsRequestFileItemResources))]
		public long RequestID { get; set; }

		/// <summary>
		/// If positive, The amount is deposited to the bank account specified
		/// by <see cref="BankAccountInfo"/>, else it is withdrawed.
		/// </summary>
		[XmlAttribute]
		[Display(
			Name = nameof(FundsRequestFileItemResources.Amount_Name),
			ResourceType = typeof(FundsRequestFileItemResources))]
		[DataType(DataType.Currency)]
		public decimal Amount { get; set; }

		/// <summary>
		/// The bank account info.
		/// </summary>
		[Required]
		[Display(
			Name = nameof(FundsRequestFileItemResources.BankAccountInfo_Name),
			ResourceType = typeof(FundsRequestFileItemResources))]
		public BankAccountInfo BankAccountInfo
		{
			get
			{
				return bankAccountInfo ?? (bankAccountInfo = new BankAccountInfo());
			}
			set
			{
				bankAccountInfo = value;
			}
		}

		#endregion
	}
}
