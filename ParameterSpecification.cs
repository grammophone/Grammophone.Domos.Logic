using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Grammophone.Domos.Logic
{
	/// <summary>
	/// Specification of a parameter in the parameters dictionary used 
	/// in <see cref="IWorkflowAction{U, D, S, ST, SO}.ExecuteAsync(S, D, SO, ST, IDictionary{string, object})"/>
	/// method. The collection of the parameter specifications for the dictionary is provided 
	/// by <see cref="IWorkflowAction{U, D, S, ST, SO}.GetParameterSpecifications"/> method.
	/// </summary>
	public class ParameterSpecification
	{
		#region Private fields

		private Func<object> defaultValueFunction;

		#endregion

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
		/// <param name="validationAttributes">
		/// Optional validation attributes for the parameter.
		/// </param>
		/// <param name="defaultValueFunction">
		/// Optional function to provide the default value of the parameter.
		/// </param>
		public ParameterSpecification(
			string key, 
			bool isRequired, 
			string caption, 
			string description, 
			Type type, 
			IEnumerable<ValidationAttribute> validationAttributes = null,
			Func<object> defaultValueFunction = null)
		{
			if (key == null) throw new ArgumentNullException(nameof(key));
			if (caption == null) throw new ArgumentNullException(nameof(caption));
			if (description == null) throw new ArgumentNullException(nameof(description));
			if (type == null) throw new ArgumentNullException(nameof(type));

			this.Key = key;
			this.IsRequired = isRequired;
			this.Caption = caption;
			this.Description = description;
			this.Type = type;
			this.ValidationAttributes = validationAttributes ?? Enumerable.Empty<ValidationAttribute>();
			this.defaultValueFunction = defaultValueFunction;
		}

		#endregion

		#region Public properties

		/// <summary>
		/// The key used in the parameters dictionary.
		/// </summary>
		public string Key { get; }

		/// <summary>
		/// If true, the parameter must exist in the dictionary.
		/// </summary>
		public bool IsRequired { get; }

		/// <summary>
		/// The caption of the parameter.
		/// </summary>
		public string Caption { get; }

		/// <summary>
		/// The description of the parameter.
		/// </summary>
		public string Description { get; }

		/// <summary>
		/// The type of the parameter.
		/// </summary>
		public Type Type { get; }

		/// <summary>
		/// Any validation attributes for the parameter.
		/// </summary>
		public IEnumerable<ValidationAttribute> ValidationAttributes { get; }

		#endregion

		#region Public methods

		/// <summary>
		/// Get the default value of the parameter, if there is one, else null.
		/// </summary>
		/// <returns>
		/// If specified in the constructor, it uses the given default value function,
		/// else creates the appropriate default value for the defined <see cref="Type"/>.
		/// </returns>
		public object GetDefaultValue()
		{
			if (defaultValueFunction != null) 
			{
				object defaultValue = defaultValueFunction();

				if (defaultValue != null)
				{
					if (!this.Type.IsAssignableFrom(defaultValue.GetType()))
					{
						throw new LogicException("The default value is incompatible to the parameter's type.");
					}
				}
				else
				{
					if (this.IsRequired)
					{
						throw new LogicException("The default value should not be null when the parameter is required.");
					}
				}

				return defaultValue;
			}

			if (this.Type.IsValueType || IsComplexType(this.Type))
			{
				return Activator.CreateInstance(this.Type);
			}

			return null;
		}

		#endregion

		#region Private methods

		private bool IsComplexType(Type type)
		{
			if (type == typeof(string)
				|| type == typeof(bool)
				|| type == typeof(int)
				|| type == typeof(short)
				|| type == typeof(long)
				|| type == typeof(Guid)
				|| type == typeof(DateTime)
				|| type == typeof(decimal)
				|| type == typeof(float)
				|| type == typeof(double)
				|| type == typeof(byte)
				|| type == typeof(uint)
				|| type == typeof(ushort)
				|| type == typeof(ulong)
				|| type == typeof(char))
			{
				return false;
			}
			else
			{
				return true;
			}
		}

		#endregion
	}
}
