using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Grammophone.Domos.DataAccess;
using Grammophone.Domos.Domain;

namespace Grammophone.Domos.Logic
{
	/// <summary>
	/// A scope for impersonating a user. Calling <see cref="Dispose"/> restores the overridden user.
	/// </summary>
	/// <typeparam name="U">The type of the user, derived from <see cref="User"/>.</typeparam>
	public class ImpersonationScope<U> : IDisposable
		where U : User
	{
		#region Private fields

		private readonly Action<ImpersonationScope<U>> userRestorationAction;

		#endregion

		#region Construction

		internal ImpersonationScope(U impersonatedUser, U overriddenUser, Action<ImpersonationScope<U>> userRestorationAction)
		{
			if (impersonatedUser == null) throw new ArgumentNullException(nameof(impersonatedUser));
			if (overriddenUser == null) throw new ArgumentNullException(nameof(overriddenUser));
			if (userRestorationAction == null) throw new ArgumentNullException(nameof(userRestorationAction));

			this.ImpersonatedUser = impersonatedUser;
			this.OverriddenUser = overriddenUser;
			this.userRestorationAction = userRestorationAction;
		}

		#endregion

		#region Public properties

		/// <summary>
		/// The overriden user which was active before the impoersonation and who will be restored when calling <see cref="Dispose"/>.
		/// </summary>
		public U OverriddenUser { get; }

		/// <summary>
		/// The impersonated user.
		/// </summary>
		public U ImpersonatedUser { get; }

		#endregion

		#region IDisposable implementation

		/// <summary>
		/// Restores the <see cref="OverriddenUser"/>.
		/// </summary>
		public void Dispose()
		{
			userRestorationAction(this);
		}

		#endregion
	}
}
