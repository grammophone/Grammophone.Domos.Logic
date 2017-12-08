﻿using System;
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
	[XmlRoot(Namespace = "urn:grammophone-domos/fundstransfer/requestfile")]
	public class FundsRequestFile
	{
		#region Private fields

		private FundsRequestFileItems items;

		private DateTime date;

		private string creditSystemCodeName;

		private Guid batchID;

		private Guid collationID;

		#endregion

		#region Construction

		/// <summary>
		/// Create.
		/// </summary>
		public FundsRequestFile()
		{
			this.Items = new FundsRequestFileItems();
		}

		/// <summary>
		/// Create with initial reserved capacity of <see cref="Items"/>.
		/// </summary>
		/// <param name="creditSystemCodeName">The code name of the credit system.</param>
		/// <param name="date">The date of the batch in UTC.</param>
		/// <param name="capacity">The initial capacity of items to reserve.</param>
		/// <param name="batchID">Optional ID of the batch. If not set, a new GUID will be assigned.</param>
		/// <param name="collationID">Optional ID of the collation of events in the batch. If not set, a new GUID will be assigned.</param>
		public FundsRequestFile(
			string creditSystemCodeName,
			DateTime date,
			int capacity,
			Guid? batchID = null,
			Guid? collationID = null)
		{
			if (creditSystemCodeName == null) throw new ArgumentNullException(nameof(creditSystemCodeName));
			if (date.Kind != DateTimeKind.Utc) throw new ArgumentException("The date is not UTC.", nameof(date));

			this.items = new FundsRequestFileItems(capacity);

			this.creditSystemCodeName = creditSystemCodeName;
			this.date = date;
			this.batchID = batchID ?? Guid.NewGuid();
			this.collationID = collationID ?? Guid.NewGuid();
		}

		#endregion

		#region Public properties

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
				if (value.Kind == DateTimeKind.Local)
					throw new ArgumentException("The value must not be local.");

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
		public FundsRequestFileItems Items
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