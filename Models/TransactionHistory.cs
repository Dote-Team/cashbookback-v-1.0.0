using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace cashbook.Models;

public class TransactionHistory
{
	[Required]
	[Key]
	[DatabaseGenerated(DatabaseGeneratedOption.Identity)]
	public Guid Id { get; set; }

	public string Operation { get; set; }

	public string? Description { get; set; }

	public string Type { get; set; }

	public decimal? From { get; set; }

	public decimal? To { get; set; }

	public decimal Amount { get; set; }

	public decimal? ExchangeRate { get; set; }

	public DateTime? ExchangeDate { get; set; }

	[Required]
	[ForeignKey("Book")]
	public Guid BookId { get; set; }

	public Book Book { get; set; }

	[ForeignKey("Transaction")]
	public Guid? TransactionId { get; set; }

	public Transaction? Transaction { get; set; }

	[Required]
	[ForeignKey("User")]
	public Guid UserId { get; set; }

	public User User { get; set; }

	[Required]
	public DateTime CreatedAt { get; set; } = DateTime.Now;

	[Required]
	public DateTime UpdatedAt { get; set; } = DateTime.Now;
}
