using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
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
		#region Construction

		/// <summary>
		/// Create.
		/// </summary>
		/// <param name="entityName">The name of the entity under violation.</param>
		/// <param name="message">The exception message.</param>
		public EntityAccessDeniedException(string entityName, string message)
			: base(message)
		{
			if (entityName == null) throw new ArgumentNullException(nameof(entityName));

			this.EntityName = entityName;
		}

		/// <summary>
		/// Used for serialization.
		/// </summary>
		protected EntityAccessDeniedException(SerializationInfo info, StreamingContext context)
			: base(info, context)
		{
			this.EntityName = info.GetString(nameof(EntityName));
		}

		#endregion

		#region Public properties

		/// <summary>
		/// The name of the entity under violation.
		/// </summary>
		public string EntityName { get; private set; }

		#endregion

		#region Protected methods

		/// <summary>
		/// Serialize the exception.
		/// </summary>
		public override void GetObjectData(SerializationInfo info, StreamingContext context)
		{
			base.GetObjectData(info, context);

			info.AddValue(nameof(EntityName), this.EntityName);
		}

		#endregion
	}
}
