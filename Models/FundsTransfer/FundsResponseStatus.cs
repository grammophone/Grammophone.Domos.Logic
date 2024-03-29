﻿using System;
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
			Name = nameof(FundsResponseStatusResources.Rejected_Name))]
		Rejected,

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
		Succeeded,

		/// <summary>
		/// The transfer request has been anulled by the account owner and the funds have been reversed.
		/// </summary>
		[Display(
			ResourceType = typeof(FundsResponseStatusResources),
			Name = nameof(FundsResponseStatusResources.Returned_Name))]
		Returned,

		/// <summary>
		/// The transfer request has a notice of change.
		/// </summary>
		[Display(
			ResourceType = typeof(FundsResponseStatusResources),
			Name = nameof(FundsResponseStatusResources.NoticeOfChange_Name))]
		NoticeOfChange

	}
}
