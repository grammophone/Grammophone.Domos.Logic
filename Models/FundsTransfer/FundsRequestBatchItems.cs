using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Grammophone.Domos.Logic.Models.FundsTransfer
{
	/// <summary>
	/// Collectino of <see cref="FundsRequestBatchItem"/>s.
	/// </summary>
	[Serializable]
	public class FundsRequestBatchItems : List<FundsRequestBatchItem>
	{
		/// <summary>
		/// Create.
		/// </summary>
		public FundsRequestBatchItems()
		{
		}

		/// <summary>
		/// Create with initial reserved capacity.
		/// </summary>
		/// <param name="capacity">The initial capacity of items to reserve.</param>
		public FundsRequestBatchItems(int capacity) : base(capacity)
		{
		}
	}
}
