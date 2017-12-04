using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Grammophone.Domos.Logic.Models.FundsTransfer
{
	/// <summary>
	/// Type of a <see cref="FundsResponseFileItem"/>.
	/// </summary>
	public enum FundsResponseFileItemType
	{
		/// <summary>
		/// The item in the batch marks a successful transfer.
		/// </summary>
		[Display(
			Name = nameof(FundsResponseFileItemTypeResources.Succeeded_Name),
			ResourceType = typeof(FundsResponseFileItemTypeResources))]
		Succeeded,

		/// <summary>
		/// The item in the batch marks a failed transfer.
		/// </summary>
		[Display(
			Name = nameof(FundsResponseFileItemTypeResources.Failed_Name),
			ResourceType = typeof(FundsResponseFileItemTypeResources))]
		Failed
	}
}
