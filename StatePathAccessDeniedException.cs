using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using Grammophone.Domos.Domain;
using Grammophone.Domos.Domain.Workflow;

namespace Grammophone.Domos.Logic
{
	/// <summary>
	/// Exception thrown when the current user has been denied access to execute a state path.
	/// </summary>
	[Serializable]
	public class StatePathAccessDeniedException : AccessDeniedException
	{
		#region Construction

		/// <summary>
		/// Create.
		/// </summary>
		/// <param name="statePath">The state path whose execution is denied.</param>
		/// <param name="statefulObject">The stateful object against which the state path execution was denied.</param>
		public StatePathAccessDeniedException(StatePath statePath, IEntityWithID<long> statefulObject)
			: this(statePath, statefulObject, $"Access to state path '{statePath.Name}' is denied.")
		{ }

		/// <summary>
		/// Create.
		/// </summary>
		/// <param name="statePath">The state path whose execution is denied.</param>
		/// <param name="statefulObject">The stateful object against which the state path execution was denied.</param>
		/// <param name="message">The message of the exception.</param>
		public StatePathAccessDeniedException(StatePath statePath, IEntityWithID<long> statefulObject, string message)
			: base(message)
		{
			if (statePath == null) throw new ArgumentNullException(nameof(statePath));
			if (statefulObject == null) throw new ArgumentNullException(nameof(statefulObject));

			this.StatePathCodeName = statePath.CodeName;
			this.StatefulObjectID = statefulObject.ID;
		}

		/// <summary>
		/// Constructor used in serialization.
		/// </summary>
		protected StatePathAccessDeniedException(SerializationInfo info, StreamingContext context)
			: base(info, context)
		{
			this.StatefulObjectID = info.GetInt64(nameof(StatefulObjectID));
			this.StatePathCodeName = info.GetString(nameof(StatePathCodeName));
		}

		#endregion

		#region Public properties

		/// <summary>
		/// The <see cref="StatePath.CodeName"/> of the <see cref="StatePath"/> on which access was denied.
		/// </summary>
		public string StatePathCodeName { get; }

		/// <summary>
		/// The ID of the stateful object against which the state path execution was denied.
		/// </summary>
		public long StatefulObjectID { get; }

		#endregion

		#region Public methods

		/// <summary>
		/// Serialize the exception.
		/// </summary>
		public override void GetObjectData(SerializationInfo info, StreamingContext context)
		{
			base.GetObjectData(info, context);

			info.AddValue(nameof(StatefulObjectID), this.StatefulObjectID);
			info.AddValue(nameof(StatePathCodeName), this.StatePathCodeName);
		}

		#endregion
	}
}
