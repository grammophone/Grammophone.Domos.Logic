using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Grammophone.Domos.AccessChecking;
using Grammophone.Domos.Domain;

namespace Grammophone.Domos.Logic
{
	/// <summary>
	/// Exception for signaling a denial when accessing an entity.
	/// </summary>
	[Serializable]
	public class EntityAccessDeniedException : AccessDeniedException, IEntityAccessDeniedException
	{
		/// <summary>
		/// The name of the entity under violation.
		/// </summary>
		public string EntityName { get; private set; }

		/// <summary>
		/// Create.
		/// </summary>
		/// <param name="entityName">The name of the entity under violation.</param>
		/// <param name="message">The exception message.</param>
		public EntityAccessDeniedException(string entityName, string message)
			: base(message)
		{
			this.EntityName = entityName;
		}

		/// <summary>
		/// Used for serialization.
		/// </summary>
		protected EntityAccessDeniedException(
		System.Runtime.Serialization.SerializationInfo info,
		System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
	}
}
