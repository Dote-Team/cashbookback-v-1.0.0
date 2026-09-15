using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace cashbook.Models;

public class Setting
{
	[Required]
	[Key]
	[DatabaseGenerated(DatabaseGeneratedOption.Identity)]
	public Guid Id { get; set; }

	public bool CategoryStatus { get; set; } = true;

	public bool PaymentMethodStatus { get; set; } = true;

	public bool ContactStatus { get; set; } = true;

	[Required]
	[ForeignKey("Book")]
	public Guid BookId { get; set; }

	public Book Book { get; set; }

	[Required]
	public DateTime CreatedAt { get; set; } = DateTime.Now;

	[Required]
	public DateTime UpdatedAt { get; set; } = DateTime.Now;
}
