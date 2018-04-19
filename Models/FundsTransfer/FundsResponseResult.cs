using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Grammophone.Domos.Domain.Accounting;

namespace Grammophone.Domos.Logic.Models.FundsTransfer
{
	/// <summary>
	/// Result of the state path executed due to accepting
	/// a <see cref="FundsResponseLine"/> for a funds transfer request.
	/// </summary>
	[Serializable]
	public class FundsResponseResult
	{
		/// <summary>
		/// The resulting funds transfer event, if generated sauccessfully, else null.
		/// </summary>
		public FundsTransferEvent Event { get; set; }

		/// <summary>
		/// The batch file line item being accepted.
		/// </summary>
		public FundsResponseLine Line { get; set; }

		/// <summary>
		/// If not null, the exception thrown during processing of the <see cref="Line"/>.
		/// </summary>
		public Exception Exception { get; set; }

		/// <summary>
		/// If true, the funds transfer line has already been digested
		/// for the request, and the <see cref="Event"/> property points to the previous event which digested it.
		/// </summary>
		public bool IsAlreadyDigested { get; set; }
	}
}
