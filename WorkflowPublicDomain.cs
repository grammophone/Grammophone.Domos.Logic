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
	/// Public domain view of a <see cref="IWorkflowUsersDomainContainer{U, ST}"/>.
	/// </summary>
	/// <typeparam name="U">
	/// The type of users, derived from <see cref="User"/>.
	/// </typeparam>
	/// <typeparam name="ST">
	/// The type of state transitions, derived from <see cref="StateTransition{U}"/>.
	/// </typeparam>
	/// <typeparam name="D">
	/// The type of domain container, derived from <see cref="IWorkflowUsersDomainContainer{U, ST}"/>.
	/// </typeparam>
	public abstract class WorkflowUsersPublicDomain<U, ST, D> : UsersPublicDomain<U, D>
		where U : User
		where ST : StateTransition<U>
		where D : IWorkflowUsersDomainContainer<U, ST>
	{
		#region Construction

		/// <summary>
		/// Create.
		/// </summary>
		/// <param name="domainContainer">
		/// The domain container to wrap, preferrably with enabled entity access security.
		/// See	<see cref="Session{U, D}.CreateSecuredDomainContainer"/>.
		/// </param>
		/// <param name="ownsDomainContainer">
		/// If true, the public domain instance owns the <paramref name="domainContainer"/>
		/// and the <see cref="PublicDomain{D}.Dispose"/> method will dispose the 
		/// underlying <paramref name="domainContainer"/>.
		/// </param>
		protected WorkflowUsersPublicDomain(D domainContainer, bool ownsDomainContainer)
			: base(domainContainer, ownsDomainContainer)
		{
		}

		#endregion

		#region Public properties

		/// <summary>
		/// Entity set of workflow graphs in the system.
		/// </summary>
		public IQueryable<WorkflowGraph> WorkflowGraphs => domainContainer.WorkflowGraphs;

		/// <summary>
		/// Entity set of workflow state groups in the system.
		/// </summary>
		public IQueryable<StateGroup> StateGroups => domainContainer.StateGroups;

		/// <summary>
		/// Entity set of workflow states in the system.
		/// </summary>
		public IQueryable<State> States => domainContainer.States;

		/// <summary>
		/// Entity set of workflow state paths in the system.
		/// </summary>
		public IQueryable<StatePath> StatePaths => domainContainer.StatePaths;

		#endregion
	}
}
