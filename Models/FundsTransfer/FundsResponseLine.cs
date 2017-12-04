using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Grammophone.Domos.Logic.Models.FundsTransfer
{
	/// <summary>
	/// A flattened funds transfer response line.
	/// </summary>
	[Serializable]
	public class FundsResponseLine
	{
		/// <summary>
		/// The ID of the external system transaction.
		/// </summary>
		[Required]
		[MaxLength(225)]
		public string TransactionID { get; set; }

		/// <summary>
		/// If true, the line hasb been successfully processed.
		/// </summary>
		public bool HasSucceeded { get; set; }

		/// <summary>
		/// The response code as returned by the Electronic Funds
		/// Transfer (EFT/ACH) system.
		/// </summary>
		[MaxLength(3)]
		public string ResponseCode { get; set; }

		/// <summary>
		/// Unique code for event tracing.
		/// </summary>
		[MaxLength(36)]
		public string TraceCode { get; set; }

		/// <summary>
		/// Optional comments.
		/// </summary>
		[MaxLength(256)]
		public string Comments { get; set; }
	}
}
