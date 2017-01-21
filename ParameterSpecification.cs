using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Grammophone.Domos.Logic
{
	/// <summary>
	/// Specification of a parameter in the parameters dictionary used 
	/// in <see cref="IWorkflowAction{U, D, S, ST, SO}.ExecuteAsync(S, SO, ST, IDictionary{string, object})"/>
	/// method. The collection of the parameter specifications for the dictionary is provided 
	/// by <see cref="IWorkflowAction{U, D, S, ST, SO}.GetParameterSpecifications"/> method.
	/// </summary>
	[Serializable]
	public class ParameterSpecification
	{
		#region Construction

		/// <summary>
		/// Create.
		/// </summary>
		/// <param name="key">
		/// The key used in the parameters dictionary.
		/// </param>
		/// <param name="isRequired">
		/// If true, the parameter must exist in the dictionary.
		/// </param>
		/// <param name="caption">
		/// The caption of the parameter.
		/// </param>
		/// <param name="description">
		/// The description of the parameter.
		/// </param>
		/// <param name="type">
		/// The type of the parameter.
		/// </param>
		public ParameterSpecification(string key, bool isRequired, string caption, string description, Type type)
		{
			if (key == null) throw new ArgumentNullException(nameof(key));
			if (caption == null) throw new ArgumentNullException(nameof(caption));
			if (description == null) throw new ArgumentNullException(nameof(description));
			if (type == null) throw new ArgumentNullException(nameof(type));

			this.Key = key;
			this.IsRequired = IsRequired;
			this.Caption = caption;
			this.Description = description;
			this.Type = type;
		}

		#endregion

		#region Public properties

		/// <summary>
		/// The key used in the parameters dictionary.
		/// </summary>
		public string Key { get; private set; }

		/// <summary>
		/// If true, the parameter must exist in the dictionary.
		/// </summary>
		public bool IsRequired { get; private set; }

		/// <summary>
		/// The caption of the parameter.
		/// </summary>
		public string Caption { get; private set; }

		/// <summary>
		/// The description of the parameter.
		/// </summary>
		public string Description { get; private set; }

		/// <summary>
		/// The type of the parameter.
		/// </summary>
		public Type Type { get; private set; }

		#endregion
	}
}
