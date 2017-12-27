using Grammophone.Domos.Domain.Accounting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Grammophone.Domos.Logic.Models.FundsTransfer
{
	/// <summary>
	/// Statistic for funds transfers.
	/// </summary>
	[Serializable]
	public class FundsTransferStatistic
	{
		/// <summary>
		/// Count of funds transfer requests not under a batch.
		/// </summary>
		public int UnbatchedRequestsCount { get; set; }

		/// <summary>
		/// Count of batches pending to be submitted,
		/// ie whose last message is of type <see cref="FundsTransferBatchMessageType.Pending"/>.
		/// </summary>
		public int PendingBatchesCount { get; set; }

		/// <summary>
		/// Count of submitted batches pending a response,
		/// ie whose last message is of type <see cref="FundsTransferBatchMessageType.Submitted"/>.
		/// </summary>
		public int SubmittedBatchesCount { get; set; }

		/// <summary>
		/// Count of rejected batches,
		/// ie whose last message is of type <see cref="FundsTransferBatchMessageType.Rejected"/>.
		/// </summary>
		public int RejectedBatchesCount { get; set; }

		/// <summary>
		/// Count of accepted batches pending a response,
		/// ie whose last message is of type <see cref="FundsTransferBatchMessageType.Accepted"/>.
		/// </summary>
		public int AcceptedBatchesCount { get; set; }

		/// <summary>
		/// Count of batches having a response,
		/// ie whose last message is of type <see cref="FundsTransferBatchMessageType.Responded"/>.
		/// </summary>
		public int RespondedBatchesCount { get; set; }
	}
}
