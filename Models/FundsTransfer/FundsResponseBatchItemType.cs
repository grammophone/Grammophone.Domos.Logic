using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Grammophone.Domos.Logic.Models.FundsTransfer
{
	/// <summary>
	/// Type of a <see cref="FundsResponseBatchItem"/>.
	/// </summary>
	public enum FundsResponseBatchItemType
	{
		/// <summary>
		/// The item in the batch marks a successful transfer.
		/// </summary>
		Succeeded,

		/// <summary>
		/// The item in the batch marks a failed transfer.
		/// </summary>
		Failed
	}
}
