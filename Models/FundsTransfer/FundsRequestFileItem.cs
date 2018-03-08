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

		#endregion

		#region Public properties

		/// <summary>
		/// The ID of the external system transaction.
		/// </summary>
		[XmlAttribute]
		[Display(
			Name = nameof(FundsRequestFileItemResources.LineID_Name),
			ResourceType = typeof(FundsRequestFileItemResources))]
		public long LineID { get; set; }

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

		/// <summary>
		/// The name of the account holder.
		/// </summary>
		[Required]
		[Display(
			Name = nameof(FundsRequestFileItemResources.AccountHolderName_Name),
			ResourceType = typeof(FundsRequestFileItemResources))]
		public string AccountHolderName { get; set; }

		#endregion
	}
}
