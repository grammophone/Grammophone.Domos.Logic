using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Grammophone.Caching;
using Grammophone.Configuration;
using Grammophone.Domos.AccessChecking;
using Grammophone.Domos.DataAccess;
using Grammophone.Domos.Domain;
using Microsoft.Practices.Unity;
using Grammophone.TemplateRendering;
using Grammophone.Setup;

namespace Grammophone.Domos.Logic
{
	/// <summary>
	/// Binds a session to its configuration environment
	/// and sets up a unity container using configurator 
	/// of type <see cref="DefaultConfigurator"/>.
	/// </summary>
	/// <typeparam name="U">The type of users in the session.</typeparam>
	/// <typeparam name="D">The type of domain container of the session.</typeparam>
	public class SessionEnvironment<U, D> : IDisposable
		where U : User
		where D : IUsersDomainContainer<U>
	{
		#region Constants

		/// <summary>
		/// Size for <see cref="storageProvidersCache"/>.
		/// </summary>
		private const int StorageProvidersCacheSize = 16;

		#endregion

		#region Private fields

		private Lazy<IReadOnlyDictionary<string, int>> lazyContentTypeIDsByMIME;

		private Lazy<IReadOnlyDictionary<string, string>> lazyContentTypesByExtension;

		private MRUCache<string, Storage.IStorageProvider> storageProvidersCache;

		#endregion

		#region Construction

		/// <summary>
		/// Create.
		/// </summary>
		/// <param name="configurationSectionName">The name of the Unity configuration section.</param>
		public SessionEnvironment(string configurationSectionName)
		{
			if (configurationSectionName == null) throw new ArgumentNullException(nameof(configurationSectionName));

			this.Settings = LoadSettings(configurationSectionName);

			var permissionsSetupProvider = this.Settings.Resolve<IPermissionsSetupProvider>();

			this.AccessResolver = new AccessResolver<U>(permissionsSetupProvider);

			this.lazyContentTypeIDsByMIME = new Lazy<IReadOnlyDictionary<string, int>>(
				this.LoadContentTypeIDsByMIME,
				true);

			this.lazyContentTypesByExtension = new Lazy<IReadOnlyDictionary<string, string>>(
				this.LoadContentTypesByExtension,
				true);

			this.storageProvidersCache = new MRUCache<string, Storage.IStorageProvider>(
				name => this.Settings.Resolve<Storage.IStorageProvider>(name),
				StorageProvidersCacheSize);
		}

		#endregion

		#region Public properties

		/// <summary>
		/// The access resolver using the <see cref="IPermissionsSetupProvider"/>
		/// specified in <see cref="Settings"/>.
		/// </summary>
		public AccessResolver<U> AccessResolver { get; private set; }

		/// <summary>
		/// Dictionary of content type IDs by MIME.
		/// </summary>
		public IReadOnlyDictionary<string, int> ContentTypeIDsByMIME => lazyContentTypeIDsByMIME.Value;

		/// <summary>
		/// Map of MIME content types by file extensions.
		/// The file extensions include the leading dot and are specified in lower case.
		/// </summary>
		public IReadOnlyDictionary<string, string> ContentTypesByExtension => lazyContentTypesByExtension.Value;

		#endregion

		#region Internal properties

		/// <summary>
		/// The Unity DI container.
		/// </summary>
		internal Settings Settings { get; private set; }

		#endregion

		#region Public methods

		/// <summary>
		/// Get a registered storage provider.
		/// </summary>
		/// <param name="providerName">The name under which the provider is registered or null for the default.</param>
		/// <returns>Returns the requested storage provider.</returns>
		public Storage.IStorageProvider GetStorageProvider(string providerName = null)
		{
			if (providerName == null) providerName = String.Empty;

			return storageProvidersCache.Get(providerName);
		}

		/// <summary>
		/// Create the configured client for sending e-mail.
		/// </summary>
		public Email.EmailClient CreateEmailClient()
		{
			var emailSettings = this.Settings.Resolve<Email.EmailSettings>();

			return new Email.EmailClient(emailSettings);
		}

		/// <summary>
		/// Get the configured template rendering provider.
		/// </summary>
		public IRenderProvider GetRenderProvider() => this.Settings.Resolve<IRenderProvider>();

		/// <summary>
		/// Dispose the environment.
		/// </summary>
		public void Dispose()
		{
			this.Settings.Dispose();
			storageProvidersCache.Clear();
		}

		#endregion

		#region Protected methods

		/// <summary>
		/// Load the settings corresponding to a configuration section.
		/// </summary>
		/// <param name="configurationSectionName">The name of the configuration section.</param>
		protected virtual Settings LoadSettings(string configurationSectionName)
			=> Settings.Load(configurationSectionName);

		#endregion

		#region Private methods

		private IReadOnlyDictionary<string, int> LoadContentTypeIDsByMIME()
		{
			using (var domainContainer = this.Settings.Resolve<D>())
			{
				var query = from ct in domainContainer.ContentTypes
										select new { ct.ID, ct.MIME };

				return query.ToDictionary(r => r.MIME, r => r.ID);
			}
		}

		private IReadOnlyDictionary<string, string> LoadContentTypesByExtension()
		{
			var filesConfiguration = this.Settings.Resolve<Configuration.FilesConfiguration>();

			if (String.IsNullOrWhiteSpace(filesConfiguration.ContentTypeAssociationsXamlPath))
				throw new LogicException("The ContentTypeAssociationsXamlPath property of FilesConfiguration is not specified.");

			var contentTypeAssociations =
				XamlConfiguration<Configuration.ContentTypeAssociations>.LoadSettings(
					filesConfiguration.ContentTypeAssociationsXamlPath);

			return contentTypeAssociations.ToDictionary(
				a => a.FileExtension.Trim().ToLower(),
				a => a.MIMEType.Trim());
		}

		#endregion
	}

	/// <summary>
	/// Binds a session to its configuration environment
	/// and sets up a unity container using a specified configurator
	/// of type <typeparamref name="C"/>.
	/// </summary>
	/// <typeparam name="U">The type of users in the session.</typeparam>
	/// <typeparam name="D">The type of domain container of the session.</typeparam>
	/// <typeparam name="C">
	/// The type of configurator to use to setup the <see cref="Settings"/>.
	/// property.
	/// </typeparam>
	public class SessionEnvironment<U, D, C> : SessionEnvironment<U, D>
		where U : User
		where D : IUsersDomainContainer<U>
		where C : Configurator, new()
	{
		#region Construiction

		/// <summary>
		/// Create.
		/// </summary>
		/// <param name="configurationSectionName">The name of the Unity configuration section.</param>
		public SessionEnvironment(string configurationSectionName) : base(configurationSectionName)
		{
		}

		#endregion

		#region Protected methods

		/// <summary>
		/// Load the settings corresponding to a configuration section.
		/// </summary>
		/// <param name="configurationSectionName">The name of the configuration section.</param>
		protected override Settings LoadSettings(string configurationSectionName)
			=> Settings.Load<C>(configurationSectionName);

		#endregion
	}
}
