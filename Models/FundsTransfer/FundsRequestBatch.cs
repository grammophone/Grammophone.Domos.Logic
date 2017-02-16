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
	/// A batch of fund requests.
	/// </summary>
	[Serializable]
	[XmlRoot(Namespace = "urn:grammophone-domos/fundstransfer/requestbatch")]
	public class FundsRequestBatch
	{
		#region Private fields

		private FundsRequestBatchItems items;

		private DateTime date;

		private string creditSystemCodeName;

		#endregion

		#region Construction

		/// <summary>
		/// Create.
		/// </summary>
		public FundsRequestBatch()
		{
			this.Items = new FundsRequestBatchItems();
		}

		/// <summary>
		/// Create with initial reserved capacity of <see cref="Items"/>.
		/// </summary>
		/// <param name="creditSystemCodeName">The code name of the credit system.</param>
		/// <param name="date">The date of the batch in UTC.</param>
		/// <param name="capacity">The initial capacity of items to reserve.</param>
		/// <param name="batchID">Optional ID of the batch.</param>
		public FundsRequestBatch(
			string creditSystemCodeName,
			DateTime date,
			int capacity,
			string batchID = null)
		{
			if (creditSystemCodeName == null) throw new ArgumentNullException(nameof(creditSystemCodeName));
			if (date.Kind != DateTimeKind.Utc) throw new ArgumentException("The date is not UTC.", nameof(date));

			this.items = new FundsRequestBatchItems(capacity);

			this.creditSystemCodeName = creditSystemCodeName;
			this.Date = date;
			this.BatchID = batchID;
		}

		#endregion

		#region Public properties

		/// <summary>
		/// The optional ID of the batch.
		/// </summary>
		[MaxLength(225)]
		public string BatchID { get; set; }

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
		public FundsRequestBatchItems Items
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
