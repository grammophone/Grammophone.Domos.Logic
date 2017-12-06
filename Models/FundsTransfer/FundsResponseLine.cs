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
		/// Optional ID of the batch where the funds transfer response belongs.
		/// </summary>
		[Display(
			ResourceType = typeof(FundsResponseLineResources),
			Name = nameof(FundsResponseLineResources.BatchID_Name))]
		public Guid? BatchID { get; set; }

		/// <summary>
		/// Optional ID of the collation where the funds response belongs.
		/// </summary>
		[Display(
			ResourceType = typeof(FundsResponseLineResources),
			Name = nameof(FundsResponseLineResources.CollationID_Name))]
		public Guid? CollationID { get; set; }

		/// <summary>
		/// The ID of the external system transaction.
		/// </summary>
		[Required]
		[MaxLength(225)]
		[Display(
			ResourceType = typeof(FundsResponseLineResources),
			Name = nameof(FundsResponseLineResources.TransactionID_Name))]
		public string TransactionID { get; set; }

		/// <summary>
		/// The status of the response.
		/// </summary>
		[Display(
			ResourceType = typeof(FundsResponseLineResources),
			Name = nameof(FundsResponseLineResources.Status_Name))]
		public FundsResponseStatus Status { get; set; }

		/// <summary>
		/// The response code as returned by the Electronic Funds
		/// Transfer (EFT/ACH) system.
		/// </summary>
		[MaxLength(3)]
		[Display(
			ResourceType = typeof(FundsResponseLineResources),
			Name = nameof(FundsResponseLineResources.ResponseCode_Name))]
		public string ResponseCode { get; set; }
		
		/// <summary>
		/// Unique code for event tracing.
		/// </summary>
		[MaxLength(36)]
		[Display(
			ResourceType = typeof(FundsResponseLineResources),
			Name = nameof(FundsResponseLineResources.TraceCode_Name))]
		public string TraceCode { get; set; }

		/// <summary>
		/// Optional comments.
		/// </summary>
		[MaxLength(256)]
		[Display(
			ResourceType = typeof(FundsResponseLineResources),
			Name = nameof(FundsResponseLineResources.Comments_Name))]
		public string Comments { get; set; }
	}
}
