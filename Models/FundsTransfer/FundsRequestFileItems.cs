using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Grammophone.Domos.Logic.Models.FundsTransfer
{
	/// <summary>
	/// Collectino of <see cref="FundsRequestFileItem"/>s.
	/// </summary>
	[Serializable]
	public class FundsRequestFileItems : List<FundsRequestFileItem>
	{
		/// <summary>
		/// Create.
		/// </summary>
		public FundsRequestFileItems()
		{
		}

		/// <summary>
		/// Create with initial reserved capacity.
		/// </summary>
		/// <param name="capacity">The initial capacity of items to reserve.</param>
		public FundsRequestFileItems(int capacity) : base(capacity)
		{
		}
	}
}
