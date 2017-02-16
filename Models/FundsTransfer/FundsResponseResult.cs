using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Grammophone.Domos.Logic.Models.Workflow;

namespace Grammophone.Domos.Logic.Models.FundsTransfer
{
	/// <summary>
	/// Result of the state path executed due to accepting
	/// a <see cref="FundsResponseBatchItem"/>.
	/// </summary>
	/// <typeparam name="SO">The type of the stateful object.</typeparam>
	/// <typeparam name="ST">The type of state transition.</typeparam>
	[Serializable]
	public class FundsResponseResult<SO, ST>
		where ST : class
	{
		/// <summary>
		/// The result of the execution of status path accepting the batch item.
		/// </summary>
		public ExecutionResult<SO, ST> ExecutionResult { get; set; }

		/// <summary>
		/// The batch item being accepted.
		/// </summary>
		public FundsResponseBatchItem BatchItem { get; set; }
	}
}
