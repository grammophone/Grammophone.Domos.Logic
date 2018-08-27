using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Grammophone.Domos.Domain.Accounting;

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
		/// Backing field for the <see cref="Time"/> property.
		/// </summary>
		private DateTime time;

		#endregion

		#region Construction

		/// <summary>
		/// Create.
		/// </summary>
		public FundsResponseLine()
		{
			time = DateTime.UtcNow;
		}

		/// <summary>
		/// Create by flattening a funds response file item.
		/// </summary>
		/// <param name="file">The funds transfer response file.</param>
		/// <param name="fileItem">The line inside the funds transfer <paramref name="file"/>.</param>
		/// <param name="batchMessageID">Optional ID of the batch event where the line belongs.</param>
		public FundsResponseLine(FundsResponseFile file, FundsResponseFileItem fileItem, long? batchMessageID = null)
		{
			if (file == null) throw new ArgumentNullException(nameof(file));
			if (fileItem == null) throw new ArgumentNullException(nameof(fileItem));

			this.Time = fileItem.Time;
			this.BatchID = file.BatchID;
			this.LineID = fileItem.LineID;
			this.Status = fileItem.Status;
			this.BatchMessageID = batchMessageID;

			if (fileItem.ResponseCode != null)
			{
				if (fileItem.ResponseCode.Length <= FundsTransferEvent.ResponseCodeLength)
				{
					this.ResponseCode = fileItem.ResponseCode;
				}
				else
				{
					this.ResponseCode = $"{fileItem.ResponseCode.Substring(0, FundsTransferEvent.ResponseCodeLength - 1)}…";
				}
			}

			if (fileItem.TraceCode != null)
			{
				if (fileItem.TraceCode.Length <= FundsTransferEvent.TraceCodeLength)
				{
					this.TraceCode = fileItem.TraceCode;
				}
				else
				{
					this.TraceCode = $"{fileItem.TraceCode.Substring(0, FundsTransferEvent.TraceCodeLength - 1)}…";
				}
			}

			if (fileItem.Comments != null)
			{
				if (fileItem.Comments.Length <= FundsTransferEvent.CommentsLength)
				{
					this.Comments = fileItem.Comments;
				}
				else
				{
					this.Comments = $"{fileItem.Comments.Substring(0, FundsTransferEvent.CommentsLength - 1)}…";
				}
			}
		}

		#endregion

		#region Public properties

		/// <summary>
		/// Optional ID of the batch message.
		/// </summary>
		[Display(
			ResourceType = typeof(FundsResponseLineResources),
			Name = nameof(FundsResponseLineResources.BatchMessageID_Name),
			Description = nameof(FundsResponseLineResources.BatchMessageID_Description))]
		public long? BatchMessageID { get; set; }

		/// <summary>
		/// The date and time, in UTC.
		/// </summary>
		[Display(
			ResourceType = typeof(FundsResponseLineResources),
			Name = nameof(FundsResponseLineResources.Time_Name),
			Description = nameof(FundsResponseLineResources.Time_Description))]
		public DateTime Time
		{
			get
			{
				return time;
			}
			set
			{
				if (value.Kind == DateTimeKind.Local)
					throw new ArgumentException("The value must not be lcoal.");

				time = value;
			}
		}

		/// <summary>
		/// The ID of the line within the batch. This matches the <see cref="FundsTransferRequest.GroupID"/> property
		/// of <see cref="FundsTransferRequest"/>.
		/// </summary>
		[Required]
		[Display(
			ResourceType = typeof(FundsResponseLineResources),
			Name = nameof(FundsResponseLineResources.LineID_Name),
			Description = nameof(FundsResponseLineResources.LineID_Description))]
		public long LineID { get; set; }

		/// <summary>
		/// The ID of the batch.
		/// </summary>
		[Required]
		[Display(
			ResourceType = typeof(FundsResponseLineResources),
			Name = nameof(FundsResponseLineResources.BatchID_Name),
			Description = nameof(FundsResponseLineResources.BatchID_Description))]
		public long BatchID { get; set; }

		/// <summary>
		/// The status of the response.
		/// </summary>
		[Display(
			ResourceType = typeof(FundsResponseLineResources),
			Name = nameof(FundsResponseLineResources.Status_Name),
			Description = nameof(FundsResponseLineResources.Status_Description))]
		public FundsResponseStatus Status { get; set; }

		/// <summary>
		/// The response code as returned by the Electronic Funds
		/// Transfer (EFT/ACH) system.
		/// </summary>
		[MaxLength(FundsTransferEvent.ResponseCodeLength)]
		[Display(
			ResourceType = typeof(FundsResponseLineResources),
			Name = nameof(FundsResponseLineResources.ResponseCode_Name))]
		public string ResponseCode { get; set; }
		
		/// <summary>
		/// Unique code for event tracing.
		/// </summary>
		[MaxLength(FundsTransferEvent.TraceCodeLength)]
		[Display(
			ResourceType = typeof(FundsResponseLineResources),
			Name = nameof(FundsResponseLineResources.TraceCode_Name),
			Description = nameof(FundsResponseLineResources.TraceCode_Description))]
		public string TraceCode { get; set; }

		/// <summary>
		/// Optional comments.
		/// </summary>
		[MaxLength(FundsTransferEvent.CommentsLength)]
		[DataType(DataType.MultilineText)]
		[Display(
			ResourceType = typeof(FundsResponseLineResources),
			Name = nameof(FundsResponseLineResources.Comments_Name),
			Description = nameof(FundsResponseLineResources.Comments_Description))]
		public string Comments { get; set; }

		#endregion
	}
}
