using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Grammophone.Domos.Logic.Models.FundsTransfer
{
	/// <summary>
	/// Collection of <see cref="FundsResponseFileItem"/>s.
	/// </summary>
	[Serializable]
	public class FundsResponseFileItems : List<FundsResponseFileItem>
	{
		/// <summary>
		/// Create.
		/// </summary>
		public FundsResponseFileItems()
		{
		}

		/// <summary>
		/// Create with initial reserved capacity.
		/// </summary>
		/// <param name="capacity">The initial capacity of items to reserve.</param>
		public FundsResponseFileItems(int capacity)
			: base(capacity)
		{
		}

		/// <summary>
		/// Create with initial items.
		/// </summary>
		/// <param name="source">The items.</param>
		public FundsResponseFileItems(IEnumerable<FundsResponseFileItem> source)
			: base(source)
		{
		}
	}
}
