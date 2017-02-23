using System;
using System.Collections.Generic;
using System.Linq;
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
		/// <summary>
		/// The type of the manager on which access is denied.
		/// </summary>
		public Type ManagerType { get; private set; }

		/// <summary>
		/// Create with default message.
		/// </summary>
		/// <param name="managerType">The type of the manager.</param>
		public ManagerAccessDeniedException(Type managerType)
			: this(managerType, $"Access to manager {GetManagerName(managerType)} is denied.")
		{
		}

		/// <summary>
		/// Create.
		/// </summary>
		/// <param name="managerType">The type of the manager.</param>
		/// <param name="message">The exception message.</param>
		public ManagerAccessDeniedException(Type managerType, string message)
			: base(message) { }

		/// <summary>
		/// Create.
		/// </summary>
		/// <param name="managerType">The type of the manager.</param>
		/// <param name="message">The exception message.</param>
		/// <param name="inner">The inner exception.</param>
		public ManagerAccessDeniedException(Type managerType, string message, Exception inner)
			: base(message, inner) { }

		/// <summary>
		/// Used in serialization.
		/// </summary>
		protected ManagerAccessDeniedException(
		System.Runtime.Serialization.SerializationInfo info,
		System.Runtime.Serialization.StreamingContext context) : base(info, context) { }

		private static string GetManagerName(Type managerType)
		{
			if (managerType == null) throw new ArgumentNullException(nameof(managerType));

			if (managerType.IsConstructedGenericType) managerType = managerType.GetGenericTypeDefinition();

			return managerType.FullName;
		}
	}
}
