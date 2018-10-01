using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace Grammophone.Domos.Logic
{
	/// <summary>
	/// Thrown when acces to a manager is denied.
	/// </summary>
	[Serializable]
	public class ManagerAccessDeniedException : AccessDeniedException
	{
		#region Construction

		/// <summary>
		/// Create with default message.
		/// </summary>
		/// <param name="managerType">The type of the manager.</param>
		public ManagerAccessDeniedException(Type managerType)
			: this(managerType, $"Access to manager {managerType.FullName} is denied.")
		{
		}

		/// <summary>
		/// Create.
		/// </summary>
		/// <param name="managerType">The type of the manager.</param>
		/// <param name="message">The exception message.</param>
		public ManagerAccessDeniedException(Type managerType, string message)
			: base(message)
		{
			this.ManagerName = managerType.FullName;
		}

		/// <summary>
		/// Used in serialization.
		/// </summary>
		protected ManagerAccessDeniedException(SerializationInfo info, StreamingContext context)
			: base(info, context)
		{
			this.ManagerName = info.GetString(nameof(ManagerName));
		}

		#endregion

		#region Public properties

		/// <summary>
		/// The name of the manager.
		/// </summary>
		public string ManagerName { get; }

		#endregion

		#region Protected methods

		/// <summary>
		/// Serialize the exception.
		/// </summary>
		public override void GetObjectData(SerializationInfo info, StreamingContext context)
		{
			base.GetObjectData(info, context);

			info.AddValue(nameof(ManagerName), this.ManagerName);
		}

		#endregion
	}
}
