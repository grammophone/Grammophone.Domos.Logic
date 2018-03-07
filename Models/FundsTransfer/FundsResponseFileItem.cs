using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace Grammophone.Domos.Logic.Models.FundsTransfer
{
	/// <summary>
	/// A line in a <see cref="FundsResponseFile"/>.
	/// </summary>
	[Serializable]
	public class FundsResponseFileItem
	{
		/// <summary>
		/// The ID of the line in the batch.
		/// </summary>
		[XmlAttribute]
		[Display(
			Name = nameof(FundsResponseFileItemResources.LineID_Name),
			ResourceType = typeof(FundsResponseFileItemResources))]
		public long LineID { get; set; }

		/// <summary>
		/// The response code as returned by the Electronic Funds
		/// Transfer (EFT/ACH) system.
		/// </summary>
		[MaxLength(Domain.Accounting.FundsTransferEvent.ResponseCodeLength)]
		[Display(
			Name = nameof(FundsResponseFileItemResources.ResponseCode_Name),
			ResourceType = typeof(FundsResponseFileItemResources))]
		public string ResponseCode { get; set; }

		/// <summary>
		/// Unique code for event tracing.
		/// </summary>
		[MaxLength(Domain.Accounting.FundsTransferEvent.TraceCodeLength)]
		[Display(
			Name = nameof(FundsResponseFileItemResources.TraceCode_Name),
			ResourceType = typeof(FundsResponseFileItemResources))]
		public string TraceCode { get; set; }

		/// <summary>
		/// The type of this item.
		/// </summary>
		[Display(
			Name = nameof(FundsResponseFileItemResources.Status_Name),
			ResourceType = typeof(FundsResponseFileItemResources))]
		public FundsResponseStatus Status { get; set; }

		/// <summary>
		/// Optional comments.
		/// </summary>
		[DataType(DataType.MultilineText)]
		[Display(
			Name = nameof(FundsResponseFileItemResources.Comments_Name),
			ResourceType = typeof(FundsResponseFileItemResources))]
		[MaxLength(Domain.Accounting.FundsTransferEvent.CommentsLength)]
		public string Comments { get; set; }
	}
}
