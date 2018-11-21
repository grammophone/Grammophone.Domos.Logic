using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Grammophone.Domos.Logic.Models.FundsTransfer;

namespace Grammophone.Domos.Logic
{
	/// <summary>
	/// Contract to convert from <see cref="FundsRequestFile"/> or convert to <see cref="FundsResponseFile"/>.
	/// </summary>
	public interface IFundsTransferFileConverter
	{
		/// <summary>
		/// The name of the converter.
		/// </summary>
		string Name { get; }

		/// <summary>
		/// The description of the converter.
		/// </summary>
		string Description { get; }

		/// <summary>
		/// The MIME type of the converted request file
		/// used in <see cref="Export(FundsRequestFile, Stream)"/>.
		/// </summary>
		string RequestContentType { get; }

		/// <summary>
		/// The MIME type of the converted response file
		/// used in <see cref="Import(Stream, String, DateTime?)"/>.
		/// </summary>
		string ResponseContentType { get; }

		/// <summary>
		/// Convert a native request file to a platform-specific funds transfer request file.
		/// </summary>
		/// <param name="requestFile">The native request file.</param>
		/// <param name="outputStream">The stream to write the converted platform-specific file.</param>
		/// <returns>Returns a proposed filename, not specifying any path.</returns>
		string Export(FundsRequestFile requestFile, Stream outputStream);

		/// <summary>
		/// Convert a platform-specific funds response file to a native response file.
		/// </summary>
		/// <param name="inputStream">The stream containing the contents of the platform-specific response file.</param>
		/// <param name="fileName">The input file name.</param>
		/// <param name="responseTimeOverride">The optional date time in UTC to override the response time implied by the imported file.</param>
		/// <returns>Returns the native file.</returns>
		IEnumerable<FundsResponseFile> Import(Stream inputStream, string fileName, DateTime? responseTimeOverride = null);
	}
}
