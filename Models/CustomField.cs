using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace cashbook.Models;

public class CustomField
{
	[Required]
	[Key]
	[DatabaseGenerated(DatabaseGeneratedOption.Identity)]
	public Guid Id { get; set; }

	[Required]
	public string Key { get; set; }

	[Required]
	[ForeignKey("Book")]
	public Guid BookId { get; set; }

	public Book Book { get; set; }

	[Required]
	public bool IsRequired { get; set; }

	public ICollection<CustomFieldValue> CustomFieldValues { get; set; } = new List<CustomFieldValue>();
}
