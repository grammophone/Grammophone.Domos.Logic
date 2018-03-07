using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Data.Entity;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Grammophone.Domos.Accounting;
using Grammophone.Domos.DataAccess;
using Grammophone.Domos.Domain.Accounting;

namespace Grammophone.Domos.Logic.Models.FundsTransfer
{
	/// <summary>
	/// A batch of fund requests.
	/// </summary>
	[Serializable]
	[XmlRoot(Namespace = "urn:grammophone-domos/fundstransfer/requestfile")]
	public class FundsRequestFile
	{
		#region Private fields

		private FundsRequestFileItems items;

		private DateTime time;

		private string creditSystemCodeName;

		private long batchID;

		private long batchMessageID;

		#endregion

		#region Construction

		/// <summary>
		/// Create.
		/// </summary>
		public FundsRequestFile()
		{
		}

		/// <summary>
		/// Create.
		/// </summary>
		/// <param name="pendingBatchMessage">
		/// The batch message. Must be complete with <see cref="FundsTransferBatchMessage.Batch"/>,
		/// <see cref="FundsTransferBatchMessage.Events"/>
		/// and have <see cref="FundsTransferBatchMessage.Type"/>
		/// set to <see cref="FundsTransferBatchMessageType.Pending"/>.
		/// </param>
		/// <exception cref="LogicException">
		/// Thrown when the specified message has <see cref="FundsTransferBatchMessage.Type"/> other
		/// than <see cref="FundsTransferBatchMessageType.Pending"/> or when
		/// it has the <see cref="FundsTransferBatchMessage.Batch"/> not properly set up.
		/// </exception>
		/// <remarks>
		/// For best performance, eager fetch the Batch.CreditSystem and Events.Request
		/// relationships of the <paramref name="pendingBatchMessage"/>.
		/// </remarks>
		public FundsRequestFile(FundsTransferBatchMessage pendingBatchMessage)
		{
			if (pendingBatchMessage == null) throw new ArgumentNullException(nameof(pendingBatchMessage));

			if (pendingBatchMessage.Type != FundsTransferBatchMessageType.Pending)
				throw new LogicException(
					$"The given batch message has type '{pendingBatchMessage.Type}' instead of '{FundsTransferBatchMessageType.Pending}'.");

			creditSystemCodeName = pendingBatchMessage?.Batch?.CreditSystem?.CodeName;

			if (creditSystemCodeName == null)
				throw new LogicException("The Batch of the message is not properly set up.");

			time = pendingBatchMessage.Time;
			batchID = pendingBatchMessage.BatchID;
			batchMessageID = pendingBatchMessage.ID;

			var items = from e in pendingBatchMessage.Events
											 let r = e.Request
											 group r by r.Group into g
											 select new FundsRequestFileItem()
											 {
												 Amount = g.Sum(r => r.Amount),
												 LineID = g.Key.ID,
												 BankAccountInfo = g.Key.EncryptedBankAccountInfo.Decrypt()
											 };

			items = new FundsRequestFileItems(items);
		}

		/// <summary>
		/// Load a <see cref="FundsTransferBatch"/> from the storage and generate the corresponding file.
		/// </summary>
		/// <param name="pendingBatchID">The ID of the batch.</param>
		/// <param name="batchMessagesSet">The set of batch messages to search in for the given <paramref name="pendingBatchID"/>.</param>
		/// <returns>Returns the created file.</returns>
		/// <exception cref="InvalidOperationException">
		/// Thrown when no <see cref="FundsTransferBatchMessage"/> was found in <paramref name="batchMessagesSet"/>
		/// having ID equal to <paramref name="pendingBatchID"/>.
		/// </exception>
		/// <exception cref="LogicException">
		/// Thrown when the specified message has <see cref="FundsTransferBatchMessage.Type"/> other
		/// than <see cref="FundsTransferBatchMessageType.Pending"/> or when
		/// it has the <see cref="FundsTransferBatchMessage.Batch"/> not properly set up.
		/// </exception>
		public static async Task<FundsRequestFile> CreateAsync(long pendingBatchID, IQueryable<FundsTransferBatchMessage> batchMessagesSet)
		{
			if (batchMessagesSet == null) throw new ArgumentNullException(nameof(batchMessagesSet));

			var batchMessage = await batchMessagesSet
				.Include(m => m.Batch.CreditSystem)
				.Include(m => m.Events.Select(e => e.Request.Group))
				.SingleOrDefaultAsync(e => e.BatchID == pendingBatchID && e.Type == FundsTransferBatchMessageType.Pending);

			if (batchMessage == null)
				throw new LogicException($"A message with batch ID '{pendingBatchID}' was not found in the specified set.");

			return new FundsRequestFile(batchMessage);
		}

		#endregion

		#region Public properties

		/// <summary>
		/// The ID of the batch.
		/// </summary>
		public long BatchID
		{
			get
			{
				return batchID;
			}
			set
			{
				batchID = value;
			}
		}

		/// <summary>
		/// The ID of the batch message.
		/// </summary>
		public long BatchMessageID
		{
			get
			{
				return batchMessageID;
			}
			set
			{
				batchMessageID = value;
			}
		}

		/// <summary>
		/// The date and time, in UTC.
		/// </summary>
		[XmlAttribute]
		public DateTime Time
		{
			get
			{
				return time;
			}
			set
			{
				if (value.Kind == DateTimeKind.Local)
					throw new ArgumentException("The time must not be local.");

				time = value;
			}
		}

		/// <summary>
		/// The code name of the credit system where this
		/// batch request is executed.
		/// </summary>
		[Required]
		[XmlAttribute]
		public string CreditSystemCodeName
		{
			get
			{
				return creditSystemCodeName;
			}
			set
			{
				if (value == null) throw new ArgumentNullException(nameof(value));

				creditSystemCodeName = value;
			}
		}

		/// <summary>
		/// The request items in the batch.
		/// </summary>
		public FundsRequestFileItems Items
		{
			get
			{
				return items ?? (items = new FundsRequestFileItems());
			}
			set
			{
				if (value == null) throw new ArgumentNullException(nameof(value));

				items = value;
			}
		}

		#endregion
	}
}
