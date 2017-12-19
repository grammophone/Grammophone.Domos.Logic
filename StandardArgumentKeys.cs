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
		/// Specifies a <see cref="DateTime"/> of type <see cref="DateTimeKind.Utc"/>.
		/// </summary>
		public const string Time = nameof(Time);

		/// <summary>
		/// Parameter key for a generic billing item,
		/// for example a <see cref="Models.FundsTransfer.FundsResponseLine"/>
		/// when we have workflow actions of
		/// type <see cref="WorkflowActions.FundsTransferResponseAction{U, BST, P, R, J, D, S, ST, SO, AS}"/>.
		/// </summary>
		public const string BillingItem = nameof(BillingItem);
	}
}
