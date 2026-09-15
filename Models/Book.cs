using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace cashbook.Models;

public class Book
{
	[Required]
	[Key]
	[DatabaseGenerated(DatabaseGeneratedOption.Identity)]
	public Guid Id { get; set; }

	[Required]
	public string Name { get; set; }

	[Required]
	[ForeignKey("Business")]
	public Guid BusinessId { get; set; }

	public Business Business { get; set; }

	[Required]
	public DateTime CreatedAt { get; set; } = DateTime.Now;

	[Required]
	public DateTime UpdatedAt { get; set; } = DateTime.Now;

	public ICollection<CustomField> CustomFields { get; set; }

	public ICollection<Transaction> Transactions { get; set; }

	public ICollection<Setting> Settings { get; set; }

	public ICollection<TransactionHistory> TransactionHistories { get; set; }
}
