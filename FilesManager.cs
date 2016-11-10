using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Practices.Unity;
using Grammophone.Domos.DataAccess;
using Grammophone.Domos.Domain;
using Grammophone.Domos.Domain.Files;
using Grammophone.Storage;

namespace Grammophone.Domos.Logic
{
	/// <summary>
	/// Base manager for handling files.
	/// </summary>
	/// <typeparam name="U">The type of the user in the domain container, derived from <see cref="User"/>.</typeparam>
	/// <typeparam name="D">The type of the domain container, derived from <see cref="IUsersDomainContainer{U}"/>.</typeparam>
	/// <typeparam name="S">The type of the session, derived from <see cref="Session{U, D}"/>.</typeparam>
	public abstract class FilesManager<U, D, S> : Manager<U, D, S>
		where U : User
		where D : IUsersDomainContainer<U>
		where S : Session<U, D>
	{
		#region Constants

		#endregion

		#region Private 

		private static IDictionary<string, int> contentTypeIDsByMIME;

		#endregion

		#region Construction

		/// <summary>
		/// Create.
		/// </summary>
		/// <param name="session">The session handing off the manager.</param>
		protected FilesManager(S session) : base(session)
		{
		}

		#endregion

		#region Public properties

		/// <summary>
		/// Dictionary of content type IDs by MIME.
		/// </summary>
		public IDictionary<string, int> ContentTypeIDsByMIME
		{
			get
			{
				if (contentTypeIDsByMIME == null)
				{
					var query = from ct in this.DomainContainer.ContentTypes
											select new { ct.ID, ct.MIME };

					contentTypeIDsByMIME = query.ToDictionary(r => r.MIME, r => r.ID);
				}

				return contentTypeIDsByMIME;
			}
		}

		#endregion

		#region Protected methods

		/// <summary>
		/// Upload the contents of a file to the storage and update
		/// the <see cref="File"/> descendants 
		/// </summary>
		/// <param name="file">The file entity to be updated.</param>
		/// <param name="containerName">The name of the storage container.</param>
		/// <param name="name">The friendly name of the file.</param>
		/// <param name="fullName">The full name of the file relative to its container.</param>
		/// <param name="stream">The source of the file's contents.</param>
		/// <param name="contentType">The content type (MIME) of the file.</param>
		/// <returns></returns>
		protected async Task UploadFileAsync(
			File file, 
			string containerName,
			string name, 
			string fullName, 
			System.IO.Stream stream, 
			string contentType)
		{
			if (file == null) throw new ArgumentNullException(nameof(file));
			if (name == null) throw new ArgumentNullException(nameof(name));
			if (fullName == null) throw new ArgumentNullException(nameof(fullName));
			if (stream == null) throw new ArgumentNullException(nameof(stream));
			if (contentType == null) throw new ArgumentNullException(nameof(contentType));

			int contentTypeID;

			if (!this.ContentTypeIDsByMIME.TryGetValue(contentType, out contentTypeID))
			{
				throw new FileException(FilesManagerMessages.UNSUPPORTED_CONTENT_TYPE);
			}
			
			{
				file.ContentTypeID = contentTypeID;
				file.Name = name;
				file.FullName = fullName;

				var storageClient = GetStorageClient();

				var container = await storageClient.GetContainerAsync(containerName);

				await container.CreateFileAsync(fullName, contentType, stream);
			}

		}

		/// <summary>
		/// Get a storage client 
		/// </summary>
		/// <returns></returns>
		protected IStorageClient GetStorageClient()
		{
			return this.SessionDIContainer.Resolve<IStorageClient>();
		}

		#endregion
	}
}
