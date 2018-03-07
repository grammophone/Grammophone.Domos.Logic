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
