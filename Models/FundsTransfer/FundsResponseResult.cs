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
	/// a <see cref="FundsResponseFileItem"/>.
	/// </summary>
	[Serializable]
	public class FundsResponseResult
	{
		/// <summary>
		/// The resulting funds transfer event, if generated sauccessfully, else null.
		/// </summary>
		public FundsTransferEvent Event { get; set; }

		/// <summary>
		/// The batch file item being accepted.
		/// </summary>
		public FundsResponseFileItem FileItem { get; set; }

		/// <summary>
		/// If not null, the exception thrown during processing of the <see cref="FileItem"/>.
		/// </summary>
		public Exception Exception { get; set; }
	}
}
