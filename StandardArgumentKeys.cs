using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Grammophone.Domos.Domain.Accounting;
using Grammophone.Domos.Logic.Models.FundsTransfer;

namespace Grammophone.Domos.Logic
{
	/// <summary>
	/// Standard keys of arguments used in <see cref="IWorkflowAction{U, D, S, ST, SO}"/>
	/// implementations.
	/// </summary>
	public static class StandardArgumentKeys
	{
		/// <summary>
		/// Specifies the <see cref="FundsTransferRequest.BatchID"/>
		/// of <see cref="FundsTransferRequest"/>s.
		/// </summary>
		public const string BatchID = nameof(BatchID);

		/// <summary>
		/// Specifies the <see cref="FundsTransferRequest.TransactionID"/>
		/// of a <see cref="FundsTransferRequest"/>.
		/// </summary>
		public const string TransactionID = nameof(TransactionID);

		/// <summary>
		/// Specifies a <see cref="FundsResponseBatchItem"/>.
		/// </summary>
		public const string BatchItem = nameof(BatchItem);
	}
}
