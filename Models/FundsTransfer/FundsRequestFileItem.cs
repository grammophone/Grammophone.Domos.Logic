using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Grammophone.Domos.Accounting.Models;

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

		#region Public properties

		/// <summary>
		/// The ID of the external system transaction.
		/// </summary>
		[Required]
		[MaxLength(225)]
		[XmlAttribute]
		[Display(
			Name = nameof(FundsRequestFileItemResources.TransactionID_Name),
			ResourceType = typeof(FundsRequestFileItemResources))]
		public string TransactionID { get; set; }

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
