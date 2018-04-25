using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Grammophone.Domos.Logic.Models.FundsTransfer
{
	/// <summary>
	/// Status of a <see cref="FundsResponseLine"/> or a <see cref="FundsResponseFileItem"/>.
	/// </summary>
	public enum FundsResponseStatus
	{
		/// <summary>
		/// The transfer has been rejected because it was malformed.
		/// </summary>
		[Display(
			ResourceType = typeof(FundsResponseStatusResources),
			Name = nameof(FundsResponseStatusResources.Accepted_Name))]
		Reject,

		/// <summary>
		/// The request has been validated and accepted, but the response is not ready yet.
		/// </summary>
		[Display(
			ResourceType = typeof(FundsResponseStatusResources),
			Name = nameof(FundsResponseStatusResources.Accepted_Name))]
		Accepted,

		/// <summary>
		/// The transfer request has failed.
		/// </summary>
		[Display(
			ResourceType = typeof(FundsResponseStatusResources),
			Name = nameof(FundsResponseStatusResources.Failed_Name))]
		Failed,

		/// <summary>
		/// The transfer request has succeeded.
		/// </summary>
		[Display(
			ResourceType = typeof(FundsResponseStatusResources),
			Name = nameof(FundsResponseStatusResources.Succeeded_Name))]
		Succeeded
	}
}
