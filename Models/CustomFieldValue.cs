using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace cashbook.Models;

public class CustomFieldValue
{
	[Required]
	[Key]
	[DatabaseGenerated(DatabaseGeneratedOption.Identity)]
	public Guid Id { get; set; }

	[Required]
	public string Value { get; set; }

	[Required]
	[ForeignKey("Transaction")]
	public Guid TransactionId { get; set; }

	public Transaction Transaction { get; set; }

	[Required]
	[ForeignKey("CustomField")]
	public Guid CustomFieldId { get; set; }

	public CustomField CustomField { get; set; }
}
