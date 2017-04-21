using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Grammophone.DataAccess;
using Grammophone.Domos.AccessChecking;
using Grammophone.Domos.DataAccess;
using Grammophone.Domos.Domain;
using Grammophone.Setup;

namespace Grammophone.Domos.Logic
{
	/// <summary>
	/// Base class for all managers handed 
	/// by <see cref="LogicSession{U, D}"/> descendants.
	/// </summary>
	/// <typeparam name="U">The type of the user in the domain container, derived from <see cref="User"/>.</typeparam>
	/// <typeparam name="D">The type of the domain container, derived from <see cref="IUsersDomainContainer{U}"/>.</typeparam>
	/// <typeparam name="S">The type of the session, derived from <see cref="LogicSession{U, D}"/>.</typeparam>
	public abstract class Manager<U, D, S> : Loggable
		where U : User
		where D : IUsersDomainContainer<U>
		where S : LogicSession<U, D>
	{
		#region Construction

		/// <summary>
		/// Create.
		/// </summary>
		/// <param name="session">The session handing off the manager.</param>
		protected Manager(S session)
		{
			if (session == null) throw new ArgumentNullException(nameof(session));

			this.Session = session;
		}

		#endregion

		#region Public properties

		/// <summary>
		/// The session which owns this manager.
		/// </summary>
		public S Session { get; private set; }

		#endregion

		#region Protected properties

		/// <summary>
		/// The access information associated with the <see cref="Session"/>.
		/// </summary>
		protected AccessResolver<U> AccessResolver
		{
			get
			{
				return this.Session.AccessResolver;
			}
		}

		/// <summary>
		/// References the domain model. Do not dispose, because the owner
		/// is the <see cref="Session"/> object.
		/// </summary>
		protected D DomainContainer
		{
			get
			{
				return this.Session.DomainContainer;
			}
		}

		/// <summary>
		/// The Unity IoC container set up for the <see cref="Session"/>.
		/// </summary>
		protected Settings SessionSettings
		{
			get
			{
				return this.Session.Settings;
			}
		}

		#endregion

		#region Protected methods

		/// <summary>
		/// Elevate access to all entities for the duration of a <paramref name="transaction"/>,
		/// taking care of any nesting. Use with care.
		/// </summary>
		/// <param name="transaction">The transaction.</param>
		/// <remarks>
		/// This is suitable for domain containers having <see cref="TransactionMode.Deferred"/>,
		/// where the saving takes place at the topmost transaction.
		/// The <see cref="GetElevatedAccessScope"/> method for elevating access rights might 
		/// restore them too soon.
		/// </remarks>
		protected void ElevateTransactionAccessRights(ITransaction transaction)
			=> this.Session.ElevateTransactionAccessRights(transaction);

		/// <summary>
		/// Get a scope of elevated access, taking care of nesting.
		/// Please ensure that <see cref="ElevatedAccessScope.Dispose"/> is called in all cases.
		/// </summary>
		/// <remarks>
		/// Until all nested of elevated access scopes 
		/// are disposed,
		/// no security checks are performed by the session.
		/// </remarks>
		protected ElevatedAccessScope GetElevatedAccessScope() => this.Session.GetElevatedAccessScope();

		/// <summary>
		/// Update an object graph asynchronously.
		/// </summary>
		/// <typeparam name="T">The type of the root of the graph.</typeparam>
		/// <param name="objectGraphRoot">The root of the graph.</param>
		/// <param name="attachAsModified">
		/// If this is true and the graph is disconnected, 
		/// it is attached with a 'modified' state,
		/// else this parameter has no effect.
		/// </param>
		/// <returns>Returns a task completing the action.</returns>
		protected async Task UpdateObjectGraphAsync<T>(T objectGraphRoot, bool attachAsModified = false)
			where T : class
		{
			if (objectGraphRoot == null) throw new ArgumentNullException(nameof(objectGraphRoot));

			using (var transaction = DomainContainer.BeginTransaction())
			{
				if (attachAsModified) this.DomainContainer.AttachGraphAsModified(objectGraphRoot);

				await transaction.CommitAsync();
			}
		}

		#endregion
	}
}
