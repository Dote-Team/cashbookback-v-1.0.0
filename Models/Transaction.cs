using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using cashbook.Models.Enums;

namespace cashbook.Models;

public class Transaction : IArchivable
{
	[Required]
	[Key]
	[DatabaseGenerated(DatabaseGeneratedOption.Identity)]
	public Guid Id { get; set; }

	[Required]
	public string Type { get; set; } = string.Empty;

	public DateTime Date { get; set; }

	public string? Description { get; set; }

	public decimal Amount { get; set; }

	public CurrencyCode Currency { get; set; } = CurrencyCode.IQD;

	public decimal? ExchangeRate { get; set; }

	public DateTime? ExchangeDate { get; set; }

	[ForeignKey("Category")]
	public Guid? CategoryId { get; set; }

	public Category? Category { get; set; }

	[ForeignKey("PaymentMethod")]
	public Guid? PaymentMethodId { get; set; }

	public PaymentMethod? PaymentMethod { get; set; }

	[Required]
	[ForeignKey("Book")]
	public Guid BookId { get; set; }

	public Book Book { get; set; } = null!;

	[Required]
	[ForeignKey("User")]
	public Guid UserId { get; set; }

	public User User { get; set; } = null!;

	[ForeignKey("Contact")]
	public Guid? ContactId { get; set; }

	public Contact? Contact { get; set; }

	[Required]
	public DateTime CreatedAt { get; set; } = DateTime.Now;

	[Required]
	public DateTime UpdatedAt { get; set; } = DateTime.Now;

	public ICollection<CustomFieldValue> CustomFieldValues { get; set; } = new List<CustomFieldValue>();

	public ICollection<Attachement> Attachements { get; set; } = new List<Attachement>();

	public ICollection<TransactionHistory> TransactionHistories { get; set; } = new List<TransactionHistory>();

	public bool IsArchived { get; set; }

	public DateTime? ArchivedAt { get; set; }

	public Guid? ArchivedByUserId { get; set; }

	[MaxLength(500)]
	public string? ArchiveReason { get; set; }
}
