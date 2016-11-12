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
using Grammophone.Configuration;
using Grammophone.Caching;

namespace Grammophone.Domos.Logic
{
	/// <summary>
	/// Base manager for handling files. It expects a dedicated configuration section where
	/// <see cref="Configuration.FilesConfiguration"/> is defined 
	/// and the <see cref="IStorageClient"/> implementations are registered.
	/// </summary>
	/// <typeparam name="U">The type of the user in the domain container, derived from <see cref="User"/>.</typeparam>
	/// <typeparam name="D">The type of the domain container, derived from <see cref="IUsersDomainContainer{U}"/>.</typeparam>
	/// <typeparam name="S">The type of the session, derived from <see cref="Session{U, D}"/>.</typeparam>
	public abstract class FilesManager<U, D, S> : Manager<U, D, S>
		where U : User
		where D : IUsersDomainContainer<U>
		where S : Session<U, D>
	{
		#region Construction

		/// <summary>
		/// Create.
		/// </summary>
		/// <param name="session">The session handing off the manager.</param>
		protected FilesManager(S session) : base(session)
		{
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
		/// <param name="encrypt">If true, the file contents are encrypted.</param>
		/// <param name="contentType">The content type (MIME) of the file.</param>
		/// <param name="providerName">The name of a storage provider or null for the default provider.</param>
		/// <returns>Returns a task completing the action.</returns>
		protected async Task UploadFileAsync(
			File file, 
			string containerName,
			string name, 
			string fullName, 
			System.IO.Stream stream, 
			bool encrypt,
			string contentType,
			string providerName = null)
		{
			if (file == null) throw new ArgumentNullException(nameof(file));
			if (containerName == null) throw new ArgumentNullException(nameof(containerName));
			if (name == null) throw new ArgumentNullException(nameof(name));
			if (fullName == null) throw new ArgumentNullException(nameof(fullName));
			if (stream == null) throw new ArgumentNullException(nameof(stream));
			if (contentType == null) throw new ArgumentNullException(nameof(contentType));

			int contentTypeID;

			var environment = this.Session.Environment;

			if (!environment.ContentTypeIDsByMIME.TryGetValue(contentType, out contentTypeID))
			{
				throw new FileException(FilesManagerMessages.UNSUPPORTED_CONTENT_TYPE);
			}
			
			using (var transaction = this.DomainContainer.BeginTransaction())
			{
				file.ContentTypeID = contentTypeID;
				file.ContainerName = containerName;
				file.ProviderName = providerName;
				file.Name = name;
				file.FullName = fullName;

				var storageProvider = environment.GetStorageProvider(providerName);

				var storageClient = storageProvider.GetClient();

				var container = await storageClient.GetContainerAsync(containerName);

				transaction.RollingBack += () => // Compensate for transaction failure and remove leftover files.
				{
					container.DeleteFileAsync(fullName).Wait();
				};

				await container.CreateFileAsync(fullName, contentType, stream);

				await transaction.CommitAsync();
			}
		}

		#endregion
	}
}
