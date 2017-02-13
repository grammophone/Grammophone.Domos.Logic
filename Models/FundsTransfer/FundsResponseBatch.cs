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
	public class FundsResponseBatch
	{
		#region Private fields

		private DateTime date;

		private FundsResponseBatchItems items;

		private string creditSystemCodeName;

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
		/// <param name="batchID">Optional ID of the batch.</param>
		public FundsResponseBatch(
			string creditSystemCodeName,
			DateTime date,
			int capacity,
			string batchID = null)
		{
			if (creditSystemCodeName == null) throw new ArgumentNullException(nameof(creditSystemCodeName));
			if (date.Kind != DateTimeKind.Utc) throw new ArgumentException("The date is not UTC.", nameof(date));

			this.items = new FundsResponseBatchItems(capacity);

			this.creditSystemCodeName = creditSystemCodeName;
			this.date = date;
			this.BatchID = batchID;
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
		/// The optional ID of the batch.
		/// </summary>
		[MaxLength(225)]
		[XmlAttribute]
		public string BatchID { get; set; }

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
