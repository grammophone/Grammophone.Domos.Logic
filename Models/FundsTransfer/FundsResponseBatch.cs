using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace Grammophone.Domos.Logic.Models.FundsTransfer
{
	/// <summary>
	/// A batch of responses for funds requests.
	/// </summary>
	[Serializable]
	[XmlRoot(Namespace = "urn:grammophone-domos/fundstransfer/responsebatch")]
	public class FundsResponseBatch
	{
		#region Private fields

		private DateTime date;

		private FundsResponseBatchItems items;

		private string creditSystemCodeName;

		private Guid batchID;

		private Guid collationID;

		#endregion

		#region Construction

		/// <summary>
		/// Create.
		/// </summary>
		public FundsResponseBatch()
		{
		}

		/// <summary>
		/// Create with initial reserved capacity of <see cref="Items"/>.
		/// </summary>
		/// <param name="creditSystemCodeName">The code name of the credit system.</param>
		/// <param name="date">The date of the batch in UTC.</param>
		/// <param name="capacity">The initial capacity of items to reserve.</param>
		/// <param name="batchID">The ID of the batch.</param>
		/// <param name="collationID">The ID of the collation of events in the batch.</param>
		public FundsResponseBatch(
			string creditSystemCodeName,
			DateTime date,
			int capacity,
			Guid batchID,
			Guid collationID)
		{
			if (creditSystemCodeName == null) throw new ArgumentNullException(nameof(creditSystemCodeName));
			if (date.Kind != DateTimeKind.Utc) throw new ArgumentException("The date is not UTC.", nameof(date));

			this.items = new FundsResponseBatchItems(capacity);

			this.creditSystemCodeName = creditSystemCodeName;
			this.date = date;
			this.batchID = batchID;
			this.collationID = collationID;
		}

		#endregion

		#region Public properties

		/// <summary>
		/// The code name of the credit system.
		/// </summary>
		[XmlAttribute]
		[Required]
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
		/// The date and time, in UTC.
		/// </summary>
		[XmlAttribute]
		public DateTime Date
		{
			get
			{
				return date;
			}
			set
			{
				if (value.Kind != DateTimeKind.Utc)
					throw new ArgumentException("The value must be UTC.");

				date = value;
			}
		}

		/// <summary>
		/// The ID of the batch.
		/// </summary>
		public Guid BatchID
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
		/// The ID of the events collation.
		/// </summary>
		public Guid CollationID
		{
			get
			{
				return collationID;
			}
			set
			{
				collationID = value;
			}
		}

		/// <summary>
		/// The response items in the batch.
		/// </summary>
		public FundsResponseBatchItems Items
		{
			get
			{
				return items;
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
