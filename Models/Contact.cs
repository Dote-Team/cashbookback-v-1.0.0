using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace cashbook.Models;

public class Contact : IArchivable, IBusinessScoped
{
	[Required]
	[Key]
	[DatabaseGenerated(DatabaseGeneratedOption.Identity)]
	public Guid Id { get; set; }

	[Required]
	public string Name { get; set; }

	public string Phone { get; set; }

	[Required]
	[ForeignKey("Business")]
	public Guid BusinessId { get; set; }

	public Business Business { get; set; }

	[Required]
	public DateTime CreatedAt { get; set; } = DateTime.Now;

	[Required]
	public DateTime UpdatedAt { get; set; } = DateTime.Now;

	public ICollection<Transaction> Transactions { get; set; }

	public bool IsArchived { get; set; }

	public DateTime? ArchivedAt { get; set; }

	public Guid? ArchivedByUserId { get; set; }

	[MaxLength(500)]
	public string? ArchiveReason { get; set; }
}
