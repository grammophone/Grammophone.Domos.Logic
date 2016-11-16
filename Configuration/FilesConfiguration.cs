using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Grammophone.Domos.Domain.Files;

namespace Grammophone.Domos.Logic.Configuration
{
	/// <summary>
	/// Holds settings for <see cref="FilesManager{U, D, S}"/>.
	/// </summary>
	[Serializable]
	public class FilesConfiguration
	{
		#region Construction

		/// <summary>
		/// Create.
		/// </summary>
		/// <param name="contentTypeAssociationsPath">
		/// The path of a XAML file holding the <see cref="ContentTypeAssociations"/>.
		/// </param>
		public FilesConfiguration(string contentTypeAssociationsPath)
		{
			if (contentTypeAssociationsPath == null) throw new ArgumentNullException(nameof(contentTypeAssociationsPath));

			this.ContentTypeAssociationsXamlPath = contentTypeAssociationsPath;
		}

		#endregion

		#region Public properties

		/// <summary>
		/// The path of a XAML file holding the <see cref="ContentTypeAssociations"/>.
		/// </summary>
		public string ContentTypeAssociationsXamlPath { get; private set; }

		#endregion
	}
}
