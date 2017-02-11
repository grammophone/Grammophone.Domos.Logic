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
	public class FundsRequestBatch
	{
		#region Private fields

		private FundsRequestBatchItems items;

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
		/// <param name="capacity">The initial capacity of items to reserve.</param>
		public FundsRequestBatch(int capacity)
		{
			this.Items = new FundsRequestBatchItems(capacity);
		}

		#endregion

		#region Public properties

		/// <summary>
		/// The optional ID of the batch.
		/// </summary>
		[MaxLength(225)]
		[XmlAttribute]
		public string BatchID { get; set; }

		/// <summary>
		/// The date and time, in UTC.
		/// </summary>
		public DateTime Date { get; set; }

		/// <summary>
		/// The code name of the credit system where this
		/// batch request is executed.
		/// </summary>
		[Required]
		[XmlAttribute]
		public string CreditSystemCodeName { get; set; }

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
