using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Grammophone.Domos.Logic.Models.FundsTransfer
{
	/// <summary>
	/// A flattened funds transfer batch and generated event collation info.
	/// </summary>
	[Serializable]
	public class FundsRequestParameters
	{
		#region Private fields

		/// <summary>
		/// Backing field for the <see cref="Date"/> property.
		/// </summary>
		private DateTime date;

		#endregion

		#region Construction

		/// <summary>
		/// Create.
		/// </summary>
		public FundsRequestParameters()
		{
			this.Date = DateTime.UtcNow;
		}

		#endregion

		#region Public properties

		/// <summary>
		/// The code name of the credit system where this
		/// batch request is executed.
		/// </summary>
		[Required]
		[Display(
			ResourceType = typeof(FundsRequestParametersResources),
			Name = nameof(FundsRequestParametersResources.CreditSystemCodeName_Name))]
		public string CreditSystemCodeName { get; set; }

		/// <summary>
		/// Optional ID of the batch where the funds transfer request belongs.
		/// </summary>
		[Display(
			ResourceType = typeof(FundsRequestParametersResources),
			Name = nameof(FundsRequestParametersResources.BatchID_Name))]
		public Guid? BatchID { get; set; }

		/// <summary>
		/// Optional ID of the collation where the funds request queueing event belongs.
		/// </summary>
		[Display(
			ResourceType = typeof(FundsRequestParametersResources),
			Name = nameof(FundsRequestParametersResources.CollationID_Name))]
		public Guid? CollationID { get; set; }

		/// <summary>
		/// The date and time, in UTC.
		/// </summary>
		[Display(
			ResourceType = typeof(FundsRequestParametersResources),
			Name = nameof(FundsRequestParametersResources.Date_Name))]
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

		#endregion
	}
}
