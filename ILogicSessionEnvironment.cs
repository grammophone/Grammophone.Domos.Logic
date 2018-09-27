using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Grammophone.Setup;

namespace Grammophone.Domos.Logic
{
	/// <summary>
	/// Abstraction of an environment for a <see cref="LogicSession{U, D}"/>
	/// </summary>
	public interface ILogicSessionEnvironment
	{
		/// <summary>
		/// Dictionary of content type IDs by MIME.
		/// </summary>
		IReadOnlyDictionary<string, int> ContentTypeIDsByMIME { get; }

		/// <summary>
		/// Map of MIME content types by file extensions.
		/// The file extensions include the leading dot and are specified in lower case.
		/// </summary>
		IReadOnlyDictionary<string, string> ContentTypesByExtension { get; }

		/// <summary>
		/// The Dependency Injection container associated with the environment.
		/// </summary>
		Settings Settings { get; }

		/// <summary>
		/// Get the logger registered under a specified name.
		/// </summary>
		/// <param name="loggerName">The name under which the logger is registered.</param>
		/// <returns>Returns the <see cref="Logging.ILogger"/> requested.</returns>
		Logging.ILogger GetLogger(string loggerName);

		/// <summary>
		/// Get a registered storage provider.
		/// </summary>
		/// <param name="providerName">The name under which the provider is registered or null for the default.</param>
		/// <returns>Returns the requested storage provider.</returns>
		Storage.IStorageProvider GetStorageProvider(string providerName = null);

		/// <summary>
		/// Create the configured client for sending e-mail.
		/// </summary>
		Email.EmailClient CreateEmailClient();

		/// <summary>
		/// Send an e-mail.
		/// </summary>
		/// <param name="mailMessage">
		/// The message to send. If its Sender property is not set, 
		/// the configured <see cref="Email.EmailSettings.DefaultSenderAddress"/> is used.
		/// </param>
		/// <returns>Returns a task completing the action.</returns>
		Task SendEmailAsync(System.Net.Mail.MailMessage mailMessage);

		/// <summary>
		/// Send an e-mail.
		/// </summary>
		/// <param name="recepients">A list of recepients separated by comma or semicolon.</param>
		/// <param name="subject">The subject of the message.</param>
		/// <param name="body">The body of the message.</param>
		/// <param name="isBodyHTML">If true, the format of the body message is HTML.</param>
		/// <param name="sender">
		/// The sender of the message, is specified, else 
		/// the configured <see cref="Email.EmailSettings.DefaultSenderAddress"/> is used.
		/// </param>
		/// <returns>Returns a task completing the action.</returns>
		/// <remarks>
		/// The message's subject, headers and body encoding is set to UTF8.
		/// </remarks>
		Task SendEmailAsync(
			string recepients,
			string subject,
			string body,
			bool isBodyHTML,
			string sender = null);

		/// <summary>
		/// Queue an e-mail message to be sent asynchronously.
		/// </summary>
		/// <param name="recepients">A list of recepients separated by comma or semicolon.</param>
		/// <param name="subject">The subject of the message.</param>
		/// <param name="body">The body of the message.</param>
		/// <param name="isBodyHTML">If true, the format of the body message is HTML.</param>
		/// <param name="sender">
		/// The sender of the message, is specified, else 
		/// the configured <see cref="Email.EmailSettings.DefaultSenderAddress"/> is used.
		/// </param>
		/// <returns>Returns a task completing the action.</returns>
		/// <remarks>
		/// The message's subject, headers and body encoding is set to UTF8.
		/// </remarks>
		void QueueEmail(
			string recepients,
			string subject,
			string body,
			bool isBodyHTML,
			string sender = null);

		/// <summary>
		/// Queue an e-mail message to be sent asynchronously.
		/// </summary>
		/// <param name="mailMessage">The e-mail message to queue.</param>
		void QueueEmail(System.Net.Mail.MailMessage mailMessage);

		/// <summary>
		/// Get the configured template rendering provider.
		/// </summary>
		TemplateRendering.IRenderProvider GetRenderProvider();
	}
}
