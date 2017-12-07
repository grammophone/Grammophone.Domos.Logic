﻿using System;
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
		#region Private fields

		/// <summary>
		/// Backing field for the <see cref="Date"/> property.
		/// </summary>
		private DateTime date;

		#endregion

		#region Construction

		/// <summary>
		/// Create.
		/// </summary>
		public FundsResponseLine()
		{
		}

		/// <summary>
		/// Create by flattening a funds response file item.
		/// </summary>
		/// <param name="file">The funds transfer response file.</param>
		/// <param name="fileItem">The line inside the funds transfer <paramref name="file"/>.</param>
		public FundsResponseLine(FundsResponseFile file, FundsResponseFileItem fileItem)
		{
			if (file == null) throw new ArgumentNullException(nameof(file));
			if (fileItem == null) throw new ArgumentNullException(nameof(fileItem));

			this.CreditSystemCodeName = file.CreditSystemCodeName;
			this.Date = file.Date;
			this.BatchID = file.BatchID;
			this.CollationID = file.CollationID;
			this.TransactionID = fileItem.TransactionID;
			this.Status = fileItem.Status;
			this.ResponseCode = fileItem.ResponseCode;
			this.TraceCode = fileItem.TraceCode;
			this.Comments = fileItem.Comments;
		}

		#endregion

		#region Public properties

		/// <summary>
		/// The code name of the credit system involved in the transfer.
		/// </summary>
		[Required]
		[Display(
			ResourceType = typeof(FundsResponseLineResources),
			Name = nameof(FundsResponseLineResources.CreditSystemCodeName_Name))]
		public string CreditSystemCodeName { get; set; }

		/// <summary>
		/// The date and time, in UTC.
		/// </summary>
		[Display(
			ResourceType = typeof(FundsResponseLineResources),
			Name = nameof(FundsResponseLineResources.Date_Name))]
		public DateTime Date
		{
			get
			{
				return date;
			}
			set
			{
				if (value.Kind == DateTimeKind.Local)
					throw new ArgumentException("The value must not be lcoal.");

				date = value;
			}
		}

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
		[MaxLength(Domain.Accounting.FundsTransferRequest.TransactionIdLength)]
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
		[MaxLength(Domain.Accounting.FundsTransferEvent.ResponseCodeLength)]
		[Display(
			ResourceType = typeof(FundsResponseLineResources),
			Name = nameof(FundsResponseLineResources.ResponseCode_Name))]
		public string ResponseCode { get; set; }
		
		/// <summary>
		/// Unique code for event tracing.
		/// </summary>
		[MaxLength(Domain.Accounting.FundsTransferEvent.TraceCodeLength)]
		[Display(
			ResourceType = typeof(FundsResponseLineResources),
			Name = nameof(FundsResponseLineResources.TraceCode_Name))]
		public string TraceCode { get; set; }

		/// <summary>
		/// Optional comments.
		/// </summary>
		[MaxLength(Domain.Accounting.FundsTransferEvent.CommentsLength)]
		[Display(
			ResourceType = typeof(FundsResponseLineResources),
			Name = nameof(FundsResponseLineResources.Comments_Name))]
		public string Comments { get; set; }

		#endregion
	}
}
