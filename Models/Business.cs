using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace cashbook.Models;

public class Business : IArchivable
{
	[Required]
	[Key]
	[DatabaseGenerated(DatabaseGeneratedOption.Identity)]
	public Guid Id { get; set; }

	[Required]
	public string Name { get; set; } = string.Empty;

	[Required]
	public DateTime CreatedAt { get; set; } = DateTime.Now;

	[Required]
	public DateTime UpdatedAt { get; set; } = DateTime.Now;

	public ICollection<Category> Categories { get; set; } = new List<Category>();

	public ICollection<PaymentMethod> PaymentMethods { get; set; } = new List<PaymentMethod>();

	public ICollection<Contact> Contacts { get; set; } = new List<Contact>();

	public ICollection<Book> Books { get; set; } = new List<Book>();

	public ICollection<BusinessUser> BusinessUsers { get; set; } = new List<BusinessUser>();

	public bool IsArchived { get; set; }

	public DateTime? ArchivedAt { get; set; }

	public Guid? ArchivedByUserId { get; set; }

	[MaxLength(500)]
	public string? ArchiveReason { get; set; }
}
