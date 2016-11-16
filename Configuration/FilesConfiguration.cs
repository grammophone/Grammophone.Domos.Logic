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
		/// <param name="encryptionAlgorithm">
		/// Name of a symmetric encryption algorithm, 
		/// suitable for <see cref="System.Security.Cryptography.SymmetricAlgorithm.Create(string)"/>.
		/// </param>
		/// <param name="encryptionKey">
		/// The encryption key to use when the files have <see cref="File.IsEncrypted"/> set.
		/// </param>
		public FilesConfiguration(
			string contentTypeAssociationsPath,
			string encryptionAlgorithm,
			string encryptionKey)
		{
			if (contentTypeAssociationsPath == null) throw new ArgumentNullException(nameof(contentTypeAssociationsPath));
			if (encryptionAlgorithm == null) throw new ArgumentNullException(nameof(encryptionAlgorithm));
			if (encryptionKey == null) throw new ArgumentNullException(nameof(encryptionKey));

			this.ContentTypeAssociationsXamlPath = contentTypeAssociationsPath;
			this.EncryptionAlgorithmName = encryptionAlgorithm;
			this.EncryptionKey = encryptionKey;
		}

		#endregion

		#region Public properties

		/// <summary>
		/// The path of a XAML file holding the <see cref="ContentTypeAssociations"/>.
		/// </summary>
		public string ContentTypeAssociationsXamlPath { get; private set; }

		/// <summary>
		/// Name of a symmetric encryption algorithm, 
		/// suitable for <see cref="System.Security.Cryptography.SymmetricAlgorithm.Create(string)"/>.
		/// </summary>
		public string EncryptionAlgorithmName { get; private set; }

		/// <summary>
		/// The encryption key to use when the files have <see cref="File.IsEncrypted"/> set,
		/// encoded as base64.
		/// </summary>
		public string EncryptionKey { get; private set; }

		#endregion
	}
}
