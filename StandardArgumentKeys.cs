using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Grammophone.Domos.Domain.Accounting;

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
		/// Specifies a <see cref="BillingItem"/>.
		/// </summary>
		public const string BatchItem = nameof(BatchItem);

		/// <summary>
		/// Specifies a <see cref="DateTime"/> of type <see cref="DateTimeKind.Utc"/>.
		/// </summary>
		public const string Date = nameof(Date);

		/// <summary>
		/// Parameter key for 
		/// a <see cref="Models.FundsTransfer.FundsResponseBatchItem"/>.
		/// </summary>
		public const string BillingItem = nameof(BillingItem);
	}
}
