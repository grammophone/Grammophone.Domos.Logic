using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Grammophone.DataAccess;
using Grammophone.Domos.Domain.Accounting;

namespace Grammophone.Domos.Logic.Models.FundsTransfer
{
	/// <summary>
	/// A line in a <see cref="FundsResponseBatch"/>.
	/// </summary>
	[Serializable]
	public class FundsResponseBatchItem
	{
		/// <summary>
		/// The ID of the external system transaction.
		/// </summary>
		[Required]
		[MaxLength(225)]
		[XmlAttribute]
		[Display(
			Name = nameof(FundsResponseBatchItemResources.TransactionID_Name),
			ResourceType = typeof(FundsResponseBatchItemResources))]
		public virtual string TransactionID { get; set; }

		/// <summary>
		/// The response code as returned by the Electronic Funds
		/// Transfer (EFT/ACH) system.
		/// </summary>
		[MaxLength(3)]
		[Display(
			Name = nameof(FundsResponseBatchItemResources.ResponseCode_Name),
			ResourceType = typeof(FundsResponseBatchItemResources))]
		public virtual string ResponseCode { get; set; }

		/// <summary>
		/// Unique code for event tracing.
		/// </summary>
		[MaxLength(36)]
		[Display(
			Name = nameof(FundsResponseBatchItemResources.TraceCode_Name),
			ResourceType = typeof(FundsResponseBatchItemResources))]
		public virtual string TraceCode { get; set; }

		/// <summary>
		/// The type of this item.
		/// </summary>
		[Display(
			Name = nameof(FundsResponseBatchItemResources.Type_Name),
			ResourceType = typeof(FundsResponseBatchItemResources))]
		public virtual FundsResponseBatchItemType Type { get; set; }

		/// <summary>
		/// Optional comments.
		/// </summary>
		[Display(
			Name = nameof(FundsResponseBatchItemResources.Comments_Name),
			ResourceType = typeof(FundsResponseBatchItemResources))]
		[MaxLength(256)]
		public virtual string Comments { get; set; }
	}
}
