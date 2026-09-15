using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace cashbook.Models;

public class User
{
	[Required]
	[Key]
	[DatabaseGenerated(DatabaseGeneratedOption.Identity)]
	public Guid Id { get; set; }

	public string Name { get; set; }

	[Required(ErrorMessage = "Username is required")]
	public string Username { get; set; }

	[Required(ErrorMessage = "Email is required")]
	public string Email { get; set; }

	[Required(ErrorMessage = "Password is required")]
	public string Password { get; set; } = "";

	[Required]
	public DateTime CreatedAt { get; set; } = DateTime.Now;

	[Required]
	public DateTime UpdatedAt { get; set; } = DateTime.Now;

	public string? ProfileImage { get; set; }

	public bool IsSuperAdmin { get; set; }

	public ICollection<Session> Sessions { get; set; }

	public ICollection<BusinessUser> BusinessUsers { get; set; }

	public ICollection<Transaction> Transactions { get; set; }

	public ICollection<TransactionHistory> TransactionHistories { get; set; }
}
