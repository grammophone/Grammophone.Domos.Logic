using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Grammophone.Domos.DataAccess;
using Grammophone.Domos.Domain;
using Grammophone.Domos.Domain.Workflow;

namespace Grammophone.Domos.Logic
{
	/// <summary>
	/// Specifies the pre-actions and the post-actions of a <see cref="StatePath"/>.
	/// </summary>
	/// <typeparam name="U">The type of the user, derived from <see cref="User"/>.</typeparam>
	/// <typeparam name="ST">The type of the state transition, derived fom <see cref="StateTransition{U}"/>.</typeparam>
	/// <typeparam name="D">The type of domain container, derived from <see cref="IWorkflowUsersDomainContainer{U, ST}"/>.</typeparam>
	/// <typeparam name="S">The type of session, derived from <see cref="Session{U, D}"/>.</typeparam>
	/// <remarks>
	/// Can be subclassed just to specify the type arguments, in order to make
	/// the configuration in Unity simpler.
	/// </remarks>
	[Serializable]
	public class StatePathConfiguration<U, ST, D, S>
		where U : User
		where ST : StateTransition<U>
		where D : IWorkflowUsersDomainContainer<U, ST>
		where S : Session<U, D>
	{
		#region Private fields

		private ICollection<IWorkflowAction<U, ST, D, S>> preActions;

		private ICollection<IWorkflowAction<U, ST, D, S>> postActions;

		#endregion

		#region Public properties

		/// <summary>
		/// The actions to execute before state transition.
		/// </summary>
		public ICollection<IWorkflowAction<U, ST, D, S>> PreActions
		{
			get
			{
				return preActions ?? (preActions = new HashSet<IWorkflowAction<U, ST, D, S>>());
			}
			set
			{
				if (value == null) throw new ArgumentNullException(nameof(value));

				preActions = value;
			}
		}

		/// <summary>
		/// The actions to execute after state transition.
		/// </summary>
		public ICollection<IWorkflowAction<U, ST, D, S>> PostActions
		{
			get
			{
				return postActions ?? (postActions = new HashSet<IWorkflowAction<U, ST, D, S>>());
			}
			set
			{
				if (value == null) throw new ArgumentNullException(nameof(value));

				postActions = value;
			}
		}

		#endregion
	}
}
