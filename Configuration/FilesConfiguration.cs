using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Grammophone.Domos.Logic.Configuration
{
	/// <summary>
	/// Holds settings for <see cref="FilesManager{U, D, S}"/>.
	/// </summary>
	[Serializable]
	public class FilesConfiguration
	{
		/// <summary>
		/// The path of a XAML file holding the <see cref="ContentTypeAssociations"/>.
		/// </summary>
		[Required]
		public string ContentTypeAssociationsXamlPath { get; set; }
	}
}
