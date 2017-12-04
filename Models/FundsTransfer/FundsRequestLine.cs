using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Grammophone.Domos.Accounting.Models;

namespace Grammophone.Domos.Logic.Models.FundsTransfer
{
	/// <summary>
	/// A flattened funds transfer request line.
	/// </summary>
	[Serializable]
	public class FundsRequestLine
	{
		#region Private fields

		/// <summary>
		/// Backing field for the <see cref="BankAccountInfo"/> property.
		/// </summary>
		private BankAccountInfo bankAccountInfo;

		#endregion

		#region Public properties

		/// <summary>
		/// Optional ID of the batch where the funds transfer request belongs.
		/// </summary>
		public Guid? BatchID { get; set; }

		/// <summary>
		/// Optional ID of the collation where the funds request queueing event belongs.
		/// </summary>
		public Guid? CollationID { get; set; }

		/// <summary>
		/// The ID of the external system transaction.
		/// </summary>
		[Required]
		[MaxLength(225)]
		public string TransactionID { get; set; }

		/// <summary>
		/// If positive, The amount is deposited to the bank account specified
		/// by <see cref="BankAccountInfo"/>, else it is withdrawed.
		/// </summary>
		[DataType(DataType.Currency)]
		public decimal Amount { get; set; }

		/// <summary>
		/// The bank account info.
		/// </summary>
		[Required]
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
