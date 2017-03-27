using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Grammophone.Domos.DataAccess;
using Grammophone.Domos.Domain;
using Grammophone.Domos.Domain.Workflow;

namespace Grammophone.Domos.Logic.Configuration
{
	/// <summary>
	/// Specifies the pre-actions and the post-actions of a <see cref="StatePath"/>.
	/// </summary>
	/// <typeparam name="U">The type of the user, derived from <see cref="User"/>.</typeparam>
	/// <typeparam name="D">The type of domain container, derived from <see cref="IUsersDomainContainer{U}"/>.</typeparam>
	/// <typeparam name="S">The type of session, derived from <see cref="LogicSession{U, D}"/>.</typeparam>
	/// <typeparam name="ST">The type of state transition, derived from <see cref="StateTransition{U}"/>.</typeparam>
	/// <typeparam name="SO">The type of stateful object, derived from <see cref="IStateful{U, ST}"/>.</typeparam>
	/// <remarks>
	/// Can be subclassed just to specify the type arguments, in order to make
	/// the configuration in Unity simpler.
	/// </remarks>
	[Serializable]
	public class StatePathConfiguration<U, D, S, ST, SO>
		where U : User
		where D : IUsersDomainContainer<U>
		where S : LogicSession<U, D>
		where ST : StateTransition<U>
		where SO : IStateful<U, ST>
	{
		#region Private fields

		private ICollection<IWorkflowAction<U, D, S, ST, SO>> preActions;

		private ICollection<IWorkflowAction<U, D, S, ST, SO>> postActions;

		#endregion

		#region Public properties

		/// <summary>
		/// The actions to execute before state transition.
		/// </summary>
		public ICollection<IWorkflowAction<U, D, S, ST, SO>> PreActions
		{
			get
			{
				return preActions ?? (preActions = new HashSet<IWorkflowAction<U, D, S, ST, SO>>());
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
		public ICollection<IWorkflowAction<U, D, S, ST, SO>> PostActions
		{
			get
			{
				return postActions ?? (postActions = new HashSet<IWorkflowAction<U, D, S, ST, SO>>());
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
