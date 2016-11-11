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
	/// and the <see cref="IStorageProvider"/> implementations are registered.
	/// </summary>
	/// <typeparam name="U">The type of the user in the domain container, derived from <see cref="User"/>.</typeparam>
	/// <typeparam name="D">The type of the domain container, derived from <see cref="IUsersDomainContainer{U}"/>.</typeparam>
	/// <typeparam name="S">The type of the session, derived from <see cref="Session{U, D}"/>.</typeparam>
	public abstract class FilesManager<U, D, S> : ConfiguredManager<U, D, S>
		where U : User
		where D : IUsersDomainContainer<U>
		where S : Session<U, D>
	{
		#region Constants

		/// <summary>
		/// The size of <see cref="environmentsCache"/>.
		/// </summary>
		private const int FilesEnvironmentsCacheSize = 4096;

		#endregion

		#region Auxilliary classes

		private class FilesEnvironment
		{
			#region Private fields

			private IUnityContainer diContainer;

			private Configuration.FilesConfiguration filesConfiguration;

			private Lazy<IReadOnlyDictionary<string, int>> lazyContentTypeIDsByMIME;

			private Lazy<IReadOnlyDictionary<string, string>> lazyContentTypesByExtension;

			private byte[] encryptionKey;

			#endregion

			#region Construction

			public FilesEnvironment(IUnityContainer diContainer)
			{
				if (diContainer == null) throw new ArgumentNullException(nameof(diContainer));

				this.diContainer = diContainer;

				filesConfiguration = diContainer.Resolve<Configuration.FilesConfiguration>();

				if (filesConfiguration == null)
					throw new LogicException("No FilesConfiguration has been defined in the manager's configuration section.");

				lazyContentTypeIDsByMIME = new Lazy<IReadOnlyDictionary<string, int>>(LoadContentTypeIDsByMIME, true);

				lazyContentTypesByExtension = new Lazy<IReadOnlyDictionary<string, string>>(LoadContentTypesByExtension, true);
			}

			#endregion

			#region Public properties

			/// <summary>
			/// Dictionary of content type IDs by MIME.
			/// </summary>
			public IReadOnlyDictionary<string, int> ContentTypeIDsByMIME
			{
				get
				{
					return lazyContentTypeIDsByMIME.Value;
				}
			}

			/// <summary>
			/// Map of MIME content types by file extensions.
			/// The file extensions include the leading dot and are specified in lower case.
			/// </summary>
			public IReadOnlyDictionary<string, string> ContentTypesByExtension
			{
				get
				{
					return lazyContentTypesByExtension.Value;
				}
			}

			/// <summary>
			/// The encruption key specified in <see cref="Configuration.FilesConfiguration.EncryptionKey"/>,
			/// decoded from base64.
			/// </summary>
			public byte[] EncryptionKey
			{
				get
				{
					if (encryptionKey == null)
					{
						try
						{
							encryptionKey = Convert.FromBase64String(filesConfiguration.EncryptionKey);
						}
						catch	(FormatException ex)
						{
							throw new LogicException(
								"The FilesConfiguration.EncryptionKey property does not hold a valid base64 string.", 
								ex);
						}
					}

					return encryptionKey;
				}
			}

			#endregion

			#region Private methods

			private IReadOnlyDictionary<string, int> LoadContentTypeIDsByMIME()
			{
				using (var domainContainer = diContainer.Resolve<D>())
				{
					var query = from ct in domainContainer.ContentTypes
											select new { ct.ID, ct.MIME };

					return query.ToDictionary(r => r.MIME, r => r.ID);
				}
			}

			private IReadOnlyDictionary<string, string> LoadContentTypesByExtension()
			{
				if (String.IsNullOrWhiteSpace(filesConfiguration.ContentTypeAssociationsXamlPath))
					throw new LogicException("The ContentTypeAssociationsXamlPath property of FilesConfiguration is not specified.");

				var contentTypeAssociations =
					XamlConfiguration<Configuration.ContentTypeAssociations>.LoadSettings(filesConfiguration.ContentTypeAssociationsXamlPath);

				return contentTypeAssociations.ToDictionary(a => a.FileExtension.Trim().ToLower(), a => a.MIMEType.Trim());
			}

			#endregion
		}

		#endregion

		#region Private fields

		/// <summary>
		/// Cache of <see cref="FilesEnvironment"/> by <see cref="ConfiguredManager{U, D, S}.ManagerDIContainer"/>.
		/// </summary>
		private static MRUCache<IUnityContainer, FilesEnvironment> environmentsCache;

		/// <summary>
		/// Backing field for <see cref="Environment"/> property.
		/// </summary>
		private FilesEnvironment environment;

		#endregion

		#region Construction

		/// <summary>
		/// Create.
		/// </summary>
		/// <param name="session">The session handing off the manager.</param>
		/// <param name="configurationSectionName">
		/// The name of the Unity configuration section dedicated to the files manager.
		/// </param>
		protected FilesManager(S session, string configurationSectionName) : base(session, configurationSectionName)
		{
		}

		/// <summary>
		/// Static initialization.
		/// </summary>
		static FilesManager()
		{
			environmentsCache = new MRUCache<IUnityContainer, FilesEnvironment>(
				diContainer => new FilesEnvironment(diContainer),
				FilesEnvironmentsCacheSize);
		}

		#endregion

		#region Public properties

		/// <summary>
		/// Dictionary of content type IDs by MIME.
		/// </summary>
		public IReadOnlyDictionary<string, int> ContentTypeIDsByMIME
		{
			get
			{
				return this.Environment.ContentTypeIDsByMIME;
			}
		}

		/// <summary>
		/// Map of MIME content types by file extensions.
		/// The file extensions include the leading dot and are specified in lower case.
		/// </summary>
		public IReadOnlyDictionary<string, string> ContentTypesByExtension
		{
			get
			{
				return this.Environment.ContentTypesByExtension;
			}
		}

		#endregion

		#region Private properties

		/// <summary>
		/// Get the setup environment for this files manager.
		/// </summary>
		private FilesEnvironment Environment
		{
			get
			{
				if (environment == null)
				{
					environment = environmentsCache.Get(this.ManagerDIContainer);
				}

				return environment;
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
		/// <param name="encrypt">If true, the file contents are encrypted.</param>
		/// <param name="contentType">The content type (MIME) of the file.</param>
		/// <returns>Returns a task completing the action.</returns>
		protected async Task UploadFileAsync(
			File file, 
			string containerName,
			string name, 
			string fullName, 
			System.IO.Stream stream, 
			bool encrypt,
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
			
			using (var transaction = this.DomainContainer.BeginTransaction())
			{
				file.ContentTypeID = contentTypeID;
				file.Name = name;
				file.FullName = fullName;

				var storageClient = GetStorageClient();

				var container = await storageClient.GetContainerAsync(containerName);

				transaction.RollingBack += () => // Compensate for transaction failure and remove leftover files.
				{
					container.DeleteFileAsync(fullName).Wait();
				};

				await container.CreateFileAsync(fullName, contentType, stream);

				await transaction.CommitAsync();
			}
		}

		/// <summary>
		/// Get a storage client.
		/// </summary>
		protected IStorageProvider GetStorageClient()
		{
			return this.ManagerDIContainer.Resolve<IStorageProvider>();
		}

		#endregion
	}
}
