using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Grammophone.Domos.Logic
{
	/// <summary>
	/// Defines a scope of elevated access, taking care of nesting.
	/// Please ensure that <see cref="Dispose"/> is called in all cases.
	/// </summary>
	/// <remarks>
	/// Until all nested of elevated access scopes are <see cref="Dispose"/>d,
	/// no security checks are performed by the session.
	/// </remarks>
	public sealed class ElevatedAccessScope : IDisposable
	{
		#region Private fields

		/// <summary>
		/// Delegate to decrement the nesting level.
		/// </summary>
		private Action decrementNextingLevelAction;

		#endregion

		#region Construction

		/// <summary>
		/// Create.
		/// </summary>
		/// <param name="decrementNextingLevelAction">Delegate to decrement the nesting level.</param>
		internal ElevatedAccessScope(Action decrementNextingLevelAction)
		{
			if (decrementNextingLevelAction == null)
				throw new ArgumentNullException(nameof(decrementNextingLevelAction));

			this.decrementNextingLevelAction = decrementNextingLevelAction;
		}

		#endregion

		#region IDisposable implementation

		/// <summary>
		/// Decrements the elevated access scope nesting level.
		/// </summary>
		public void Dispose()
		{
			if (decrementNextingLevelAction != null)
			{
				decrementNextingLevelAction();

				decrementNextingLevelAction = null;
			}
		}

		#endregion
	}
}
