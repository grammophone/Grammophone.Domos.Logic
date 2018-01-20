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
		public FundsResponseLine(FundsResponseFile file, FundsResponseFileItem fileItem)
		{
			if (file == null) throw new ArgumentNullException(nameof(file));
			if (fileItem == null) throw new ArgumentNullException(nameof(fileItem));

			this.Time = file.Time;
			this.BatchMessageID = file.BatchMessageID;
			this.RequestID = fileItem.RequestID;
			this.Status = fileItem.Status;

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
		/// The date and time, in UTC.
		/// </summary>
		[Display(
			ResourceType = typeof(FundsResponseLineResources),
			Name = nameof(FundsResponseLineResources.Time_Name))]
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
		/// Optional ID of the funds transfer batch message associated with the line.
		/// </summary>
		[Display(
			ResourceType = typeof(FundsResponseLineResources),
			Name = nameof(FundsResponseLineResources.BatchMessageID_Name))]
		public Guid? BatchMessageID { get; set; }

		/// <summary>
		/// The ID of the external system transaction.
		/// </summary>
		[Required]
		[Display(
			ResourceType = typeof(FundsResponseLineResources),
			Name = nameof(FundsResponseLineResources.RequestID_Name))]
		public long RequestID { get; set; }

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
			Name = nameof(FundsResponseLineResources.TraceCode_Name))]
		public string TraceCode { get; set; }

		/// <summary>
		/// Optional comments.
		/// </summary>
		[MaxLength(FundsTransferEvent.CommentsLength)]
		[DataType(DataType.MultilineText)]
		[Display(
			ResourceType = typeof(FundsResponseLineResources),
			Name = nameof(FundsResponseLineResources.Comments_Name))]
		public string Comments { get; set; }

		#endregion
	}
}
