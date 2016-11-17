using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Grammophone.Domos.DataAccess;
using Grammophone.Domos.Domain;
using Grammophone.Domos.Domain.Files;
using Grammophone.Storage;

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
		/// Upload the contents of a <see cref="IFile"/> to the storage and update
		/// its properties.
		/// </summary>
		/// <param name="contentType">The content type (MIME) of the file.</param>
		/// <param name="file">The file entity to be updated.</param>
		/// <param name="containerName">The name of the storage container.</param>
		/// <param name="name">The friendly name of the file.</param>
		/// <param name="fullName">The full name of the file relative to its container.</param>
		/// <param name="stream">The source of the file's contents.</param>
		/// <param name="encrypt">If true, the file contents are encrypted.</param>
		/// <param name="providerName">The name of a storage provider or null for the default provider.</param>
		/// <returns>Returns a task completing the action.</returns>
		protected async Task UploadFileAsync(
			string contentType,
			IFile file,
			string containerName,
			string name,
			string fullName,
			System.IO.Stream stream,
			bool encrypt,
			string providerName = null)
		{
			if (contentType == null) throw new ArgumentNullException(nameof(contentType));
			if (file == null) throw new ArgumentNullException(nameof(file));
			if (containerName == null) throw new ArgumentNullException(nameof(containerName));
			if (name == null) throw new ArgumentNullException(nameof(name));
			if (fullName == null) throw new ArgumentNullException(nameof(fullName));
			if (stream == null) throw new ArgumentNullException(nameof(stream));

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
				file.IsEncrypted = encrypt;

				var storageProvider = environment.GetStorageProvider(providerName);

				var storageClient = storageProvider.GetClient();

				var container = await storageClient.GetContainerAsync(containerName);

				transaction.RollingBack += () => // Compensate for transaction failure and remove leftover files.
				{
					container.DeleteFileAsync(fullName).Wait();
				};

				var storageFile = await container.CreateFileAsync(fullName, contentType);

				await storageFile.UploadFromStreamAsync(stream, encrypt);

				await transaction.CommitAsync();
			}
		}

		/// <summary>
		/// Upload the contents of a <see cref="IFile"/> of a user to the storage and update
		/// its properties.
		/// </summary>
		/// <param name="user">The owner of the file.</param>
		/// <param name="file">The file entity to be updated.</param>
		/// <param name="containerName">The name of the storage container.</param>
		/// <param name="filename">The name of the file.</param>
		/// <param name="stream">The source of the file's contents.</param>
		/// <param name="encrypt">If true, the file contents are encrypted.</param>
		/// <param name="providerName">The name of a storage provider or null for the default provider.</param>
		/// <returns>Returns a task completing the action.</returns>
		protected async Task UploadUserFileAsync(
			User user,
			IFile file,
			string containerName,
			string filename,
			System.IO.Stream stream,
			bool encrypt,
			string providerName = null)
		{
			if (user == null) throw new ArgumentNullException(nameof(user));
			if (file == null) throw new ArgumentNullException(nameof(file));
			if (containerName == null) throw new ArgumentNullException(nameof(containerName));
			if (stream == null) throw new ArgumentNullException(nameof(stream));

			string contentType = GetFilenameContentType(filename);

			if (contentType == null)
			{
				throw new FileException(FilesManagerMessages.UNSUPPORTED_CONTENT_TYPE);
			}

			var fileGuid = new Guid();

			string fullName = $"{user.Guid}/{fileGuid}_{filename}";

			await UploadFileAsync(
				contentType,
				file,
				containerName,
				filename,
				fullName,
				stream,
				encrypt,
				providerName);
		}

		/// <summary>
		/// Upload the contents of a <see cref="IFile"/> of the current user to the storage and update
		/// its properties.
		/// </summary>
		/// <param name="file">The file entity to be updated.</param>
		/// <param name="containerName">The name of the storage container.</param>
		/// <param name="filename">The name of the file.</param>
		/// <param name="stream">The source of the file's contents.</param>
		/// <param name="encrypt">If true, the file contents are encrypted.</param>
		/// <param name="providerName">The name of a storage provider or null for the default provider.</param>
		/// <returns>Returns a task completing the action.</returns>
		protected async Task UploadUserFileAsync(
			IFile file,
			string containerName,
			string filename,
			System.IO.Stream stream,
			bool encrypt,
			string providerName = null)
		{
			await UploadUserFileAsync(this.Session.User, file, containerName, filename, stream, encrypt, providerName);
		}

		/// <summary>
		/// Download the contents of a <see cref="IFile"/> to a stream.
		/// </summary>
		/// <param name="file">The file to download.</param>
		/// <param name="stream">The stream to write the contents to.</param>
		/// <returns>Returns a task completing the action.</returns>
		protected async Task DownloadFileAsync(
			IFile file,
			System.IO.Stream stream)
		{
			if (file == null) throw new ArgumentNullException(nameof(file));
			if (stream == null) throw new ArgumentNullException(nameof(stream));

			var storageFile = await OpenStorageFileAsync(file);

			await storageFile.DownloadToStreamAsync(stream);
		}

		/// <summary>
		/// Open a stream for reading the contants of a <see cref="IFile"/>.
		/// </summary>
		/// <param name="file">The file to open.</param>
		/// <returns>Returns a task whose result contains the input stream.</returns>
		protected async Task<System.IO.Stream> ReadFileAsync(IFile file)
		{
			if (file == null) throw new ArgumentNullException(nameof(file));

			var storageFile = await OpenStorageFileAsync(file);

			return await storageFile.OpenReadAsync();
		}

		/// <summary>
		/// Get the content type matching the extension of
		/// a <paramref name="filename"/>.
		/// </summary>
		/// <param name="filename">The file name.</param>
		/// <returns>
		/// Returns the corresponding extension according to the 
		/// configured <see cref="Configuration.ContentTypeAssociations"/> or null if no match.
		/// </returns>
		protected string GetFilenameContentType(string filename)
		{
			if (filename == null) throw new ArgumentNullException(nameof(filename));

			filename = filename.Trim();

			int dotIndex = filename.LastIndexOf('.');

			if (dotIndex == -1) return null;

			string extension = filename.Substring(dotIndex).ToLower();

			string contentType;

			if (this.Session.Environment.ContentTypesByExtension.TryGetValue(extension, out contentType))
			{
				return contentType;
			}
			else
			{
				return null;
			}
		}

		#endregion

		#region Private methods

		private async Task<IStorageFile> OpenStorageFileAsync(IFile file)
		{
			if (file == null) throw new ArgumentNullException(nameof(file));

			var environment = this.Session.Environment;

			var storageProvider = environment.GetStorageProvider(file.ProviderName);

			var client = storageProvider.GetClient();

			var container = await client.GetContainerAsync(file.ContainerName);

			if (container == null)
			{
				throw new LogicException(
					$"The storage container '{file.ContainerName}' does not exist.");
			}

			var storageFile = await container.GetFileAsync(file.FullName);

			if (storageFile == null)
			{
				throw new LogicException(
					$"The file '{file.FullName}' does not exist in container '{file.ContainerName}'.");
			}

			return storageFile;
		}

		#endregion
	}
}
