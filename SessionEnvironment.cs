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

namespace Grammophone.Domos.Logic
{
	/// <summary>
	/// Binds a session to its configuration environment
	/// and sets up a unity container using <see cref="Configurator"/>.
	/// </summary>
	/// <typeparam name="U">The type of users in the session.</typeparam>
	/// <typeparam name="D">The type of domain container of the session.</typeparam>
	public class SessionEnvironment<U, D>
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

			this.DIContainer = CreateDIContainer(configurationSectionName);

			var permissionsSetupProvider = this.DIContainer.Resolve<IPermissionsSetupProvider>();

			this.AccessResolver = new AccessResolver<U>(permissionsSetupProvider);

			this.lazyContentTypeIDsByMIME = new Lazy<IReadOnlyDictionary<string, int>>(
				this.LoadContentTypeIDsByMIME,
				true);

			this.lazyContentTypesByExtension = new Lazy<IReadOnlyDictionary<string, string>>(
				this.LoadContentTypesByExtension,
				true);

			this.storageProvidersCache = new MRUCache<string, Storage.IStorageProvider>(
				name => this.DIContainer.Resolve<Storage.IStorageProvider>(name),
				StorageProvidersCacheSize);
		}

		#endregion

		#region Public properties

		/// <summary>
		/// The access resolver using the <see cref="IPermissionsSetupProvider"/>
		/// specified in <see cref="DIContainer"/>.
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
		internal IUnityContainer DIContainer { get; private set; }

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
			var emailSettings = this.DIContainer.Resolve<Email.EmailSettings>();

			return new Email.EmailClient(emailSettings);
		}

		/// <summary>
		/// Get the configured template rendering provider.
		/// </summary>
		public IRenderProvider GetRenderProvider() => this.DIContainer.Resolve<IRenderProvider>();

		#endregion

		#region Protected methods

		/// <summary>
		/// Specifies the <see cref="Configurator"/> to use for 
		/// setting up the <see cref="DIContainer"/>.
		/// This implementation uses <see cref="DefaultConfigurator"/>.
		/// </summary>
		protected virtual Configurator GetConfigurator()
		{
			return new DefaultConfigurator();
		}

		#endregion

		#region Private methods

		/// <summary>
		/// Create a unity container and set it up using the configurator
		/// provided by <see cref="GetConfigurator"/> method.
		/// </summary>
		/// <param name="configurationSectionName">The name of the configuration section.</param>
		/// <returns>Returns the configured unity container.</returns>
		private IUnityContainer CreateDIContainer(string configurationSectionName)
		{
			if (configurationSectionName == null) throw new ArgumentNullException(nameof(configurationSectionName));

			var diContainer = new UnityContainer();

			var configurator = GetConfigurator();

			configurator.Configure(configurationSectionName, diContainer);

			return diContainer;
		}

		private IReadOnlyDictionary<string, int> LoadContentTypeIDsByMIME()
		{
			using (var domainContainer = this.DIContainer.Resolve<D>())
			{
				var query = from ct in domainContainer.ContentTypes
										select new { ct.ID, ct.MIME };

				return query.ToDictionary(r => r.MIME, r => r.ID);
			}
		}

		private IReadOnlyDictionary<string, string> LoadContentTypesByExtension()
		{
			var filesConfiguration = this.DIContainer.Resolve<Configuration.FilesConfiguration>();

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
	/// and sets up a unity container using a specified configurator.
	/// </summary>
	/// <typeparam name="U">The type of users in the session.</typeparam>
	/// <typeparam name="D">The type of domain container of the session.</typeparam>
	/// <typeparam name="C">
	/// The type of configurator to use to setup the <see cref="SessionEnvironment{U, D}.DIContainer"/>
	/// property.
	/// </typeparam>
	public class SessionEnvironment<U, D, C> : SessionEnvironment<U, D>
		where U : User
		where D : IUsersDomainContainer<U>
		where C : Configurator, new()
	{
		#region Construction

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
		/// Returns a configurator of type <typeparamref name="C"/>.
		/// </summary>
		protected override Configurator GetConfigurator()
		{
			return new C();
		}

		#endregion
	}
}
