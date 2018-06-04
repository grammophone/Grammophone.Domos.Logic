using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Grammophone.Domos.Logic.Models.FundsTransfer
{
	/// <summary>
	/// The type of a <see cref="FundsResponseFile"/>.
	/// </summary>
	public enum FundsResponseFileType
	{
		/// <summary>
		/// The batch was found invalid and was rejected by the credit system.
		/// </summary>
		[Display(
			ResourceType = typeof(FundsResponseFileTypeResources),
			Name = nameof(FundsResponseFileTypeResources.Rejected_Name))]
		Rejected,

		/// <summary>
		/// The batch has been accepted by the credit system.
		/// </summary>
		[Display(
			ResourceType = typeof(FundsResponseFileTypeResources),
			Name = nameof(FundsResponseFileTypeResources.Accepted_Name))]
		Accepted,

		/// <summary>
		/// A response for the batch has been received from the credit system.
		/// </summary>
		[Display(
			ResourceType = typeof(FundsResponseFileTypeResources),
			Name = nameof(FundsResponseFileTypeResources.Responded_Name))]
		Responded
	}
}
