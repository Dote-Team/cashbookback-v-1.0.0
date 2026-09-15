using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using cashbook.Models.Enums;

namespace cashbook.Models;

public class ExchangeRate
{
	[Required]
	[Key]
	[DatabaseGenerated(DatabaseGeneratedOption.Identity)]
	public Guid Id { get; set; }

	[Required]
	[ForeignKey("Book")]
	public Guid BookId { get; set; }

	public Book Book { get; set; }

	public CurrencyCode Currency { get; set; } = CurrencyCode.USD;

	public decimal Rate { get; set; }

	[Column(TypeName = "date")]
	public DateTime RateDate { get; set; }

	[ForeignKey("SetByUser")]
	public Guid? SetByUserId { get; set; }

	public User? SetByUser { get; set; }

	[Required]
	public DateTime CreatedAt { get; set; } = DateTime.Now;

	[Required]
	public DateTime UpdatedAt { get; set; } = DateTime.Now;
}
