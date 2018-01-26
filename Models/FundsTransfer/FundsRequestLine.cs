using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Grammophone.Domos.Logic.Models.FundsTransfer
{
	/// <summary>
	/// A flattened line to produce a funds transfer request.
	/// </summary>
	[Serializable]
	public class FundsRequestLine
	{
		/// <summary>
		/// Maximum length for the <see cref="Comments"/> property.
		/// </summary>
		public const int CommentsLength = 512;

		/// <summary>
		/// Optional ID of the batch to enroll the new funds transfer under.
		/// </summary>
		[Display(
			ResourceType = typeof(FundsRequestLineResources),
			Name = nameof(FundsRequestLineResources.BatchID_Name))]
		public Guid? BatchID { get; set; }

		/// <summary>
		/// Optional comments to be recorded.
		/// </summary>
		[MaxLength(CommentsLength)]
		[DataType(DataType.MultilineText)]
		[Display(
			ResourceType = typeof(FundsRequestLineResources),
			Name = nameof(FundsRequestLineResources.Comments_Name))]
		public string Comments { get; set; }
	}
}
