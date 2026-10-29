using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace cashbook.Models;

public class Book : IArchivable
{
	[Required]
	[Key]
	[DatabaseGenerated(DatabaseGeneratedOption.Identity)]
	public Guid Id { get; set; }

	[Required]
	public string Name { get; set; } = string.Empty;

	[Required]
	[ForeignKey("Business")]
	public Guid BusinessId { get; set; }

	public Business Business { get; set; } = null!;

	[Required]
	public DateTime CreatedAt { get; set; } = DateTime.Now;

	[Required]
	public DateTime UpdatedAt { get; set; } = DateTime.Now;

	public ICollection<CustomField> CustomFields { get; set; } = new List<CustomField>();

	public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();

	public ICollection<Setting> Settings { get; set; } = new List<Setting>();

	public ICollection<TransactionHistory> TransactionHistories { get; set; } = new List<TransactionHistory>();

	public bool IsArchived { get; set; }

	public DateTime? ArchivedAt { get; set; }

	public Guid? ArchivedByUserId { get; set; }

	[MaxLength(500)]
	public string? ArchiveReason { get; set; }
}
