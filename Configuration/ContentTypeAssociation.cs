using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Grammophone.Domos.Logic.Configuration
{
	/// <summary>
	/// Associates a file extension with a content type.
	/// </summary>
	[Serializable]
	public class ContentTypeAssociation
	{
		/// <summary>
		/// The file extension, including the dot.
		/// </summary>
		public string FileExtension { get; set; }

		/// <summary>
		/// The MIME type code.
		/// </summary>
		public string MIMEType { get; set; }
	}
}
