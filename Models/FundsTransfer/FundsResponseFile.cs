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
	[XmlRoot(Namespace = "urn:grammophone-domos/fundstransfer/responsefile")]
	public class FundsResponseFile
	{
		#region Private fields

		private DateTime time;

		private FundsResponseFileItems items;

		private Guid batchMessageID;

		#endregion

		#region Construction

		/// <summary>
		/// Create.
		/// </summary>
		public FundsResponseFile()
		{
		}

		/// <summary>
		/// Create.
		/// </summary>
		/// <param name="time">The date and time of the batch in UTC.</param>
		public FundsResponseFile(DateTime time)
		{
			if (time.Kind != DateTimeKind.Utc) throw new ArgumentException("The date is not UTC.", nameof(time));

			this.time = time;
			this.batchMessageID = Guid.NewGuid();
		}

		/// <summary>
		/// Create with initial reserved capacity of <see cref="Items"/>.
		/// </summary>
		/// <param name="time">The date and time of the batch in UTC.</param>
		/// <param name="capacity">The initial capacity of items to reserve.</param>
		public FundsResponseFile(DateTime time, int capacity)
			: this(time)
		{
			this.items = new FundsResponseFileItems(capacity);
		}

		/// <summary>
		/// Create with initial reserved capacity of <see cref="Items"/>.
		/// </summary>
		/// <param name="time">The date and time of the batch in UTC.</param>
		/// <param name="capacity">The initial capacity of items to reserve.</param>
		/// <param name="batchMessageID">The ID of the batch messgage ID whare the items of the file correspond to.</param>
		public FundsResponseFile(
			DateTime time,
			int capacity,
			Guid batchMessageID)
		{
			if (time.Kind != DateTimeKind.Utc) throw new ArgumentException("The date is not UTC.", nameof(time));

			this.items = new FundsResponseFileItems(capacity);

			this.time = time;
			this.batchMessageID = batchMessageID;
		}

		#endregion

		#region Public properties

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
					throw new ArgumentException("The value must not be lcoal.");

				time = value;
			}
		}

		/// <summary>
		/// The ID of the events collation.
		/// </summary>
		public Guid BatchMessageID
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
		/// The response items in the batch.
		/// </summary>
		public FundsResponseFileItems Items
		{
			get
			{
				return items ?? (items = new FundsResponseFileItems());
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
