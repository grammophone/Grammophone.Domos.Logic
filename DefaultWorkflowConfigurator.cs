﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Grammophone.Domos.DataAccess;
using Grammophone.Domos.Domain;
using Grammophone.Domos.Domain.Workflow;
using Grammophone.Domos.Logic.Configuration;
using Grammophone.Setup;
using Unity;

namespace Grammophone.Domos.Logic
{
	/// <summary>
	/// Configurator facilitating workflow managers using the contents of a Unity configuration section.
	/// </summary>
	public abstract class DefaultWorkflowConfigurator : DefaultConfigurator
	{
		/// <summary>
		/// Register a state path configuration
		/// under the code name of a state path.
		/// </summary>
		/// <param name="unityContainer">The unity container to register to.</param>
		/// <param name="statePathCodeName">The state path code name.</param>
		/// <param name="statePathConfiguration">The configuration to register.</param>
		protected void RegisterStatePathConfiguration<U, D, S, ST, SO>(
			IUnityContainer unityContainer,
			string statePathCodeName,
			StatePathConfiguration<U, D, S, ST, SO> statePathConfiguration)
			where U : User
			where D : IUsersDomainContainer<U>
			where S : LogicSession<U, D>
			where ST : StateTransition<U>
			where SO : IStateful<U, ST>
		{
			if (unityContainer == null) throw new ArgumentNullException(nameof(unityContainer));
			if (statePathCodeName == null) throw new ArgumentNullException(nameof(statePathCodeName));
			if (statePathConfiguration == null) throw new ArgumentNullException(nameof(statePathConfiguration));

			unityContainer.RegisterInstance(statePathCodeName, statePathConfiguration);
		}
	}
}
