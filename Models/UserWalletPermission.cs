using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace cashbook.Models;

/// <summary>صلاحية واحدة لمستخدم على خزنة واحدة، ومن منحها ومتى.</summary>
public class UserWalletPermission
{
	[Key]
	[DatabaseGenerated(DatabaseGeneratedOption.Identity)]
	public Guid Id { get; set; }

	[ForeignKey("User")]
	public Guid UserId { get; set; }
	public User User { get; set; } = null!;

	[ForeignKey("Book")]
	public Guid BookId { get; set; }
	public Book Book { get; set; } = null!;

	[Required]
	[MaxLength(64)]
	public string Permission { get; set; } = string.Empty;

	public Guid? GrantedByUserId { get; set; }

	public DateTime GrantedAt { get; set; } = DateTime.UtcNow;
}
