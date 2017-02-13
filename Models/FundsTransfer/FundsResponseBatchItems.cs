using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Grammophone.Domos.Logic.Models.FundsTransfer
{
	/// <summary>
	/// Collection of <see cref="FundsResponseBatchItem"/>s.
	/// </summary>
	[Serializable]
	public class FundsResponseBatchItems : List<FundsResponseBatchItem>
	{
		/// <summary>
		/// Create.
		/// </summary>
		public FundsResponseBatchItems()
		{
		}

		/// <summary>
		/// Create with initial reserved capacity.
		/// </summary>
		/// <param name="capacity">The initial capacity of items to reserve.</param>
		public FundsResponseBatchItems(int capacity)
		{
		}
	}
}
