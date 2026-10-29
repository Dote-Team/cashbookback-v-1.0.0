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

	public string Name { get; set; } = string.Empty;

	[Required(ErrorMessage = "Username is required")]
	public string Username { get; set; } = string.Empty;

	[Required(ErrorMessage = "Email is required")]
	public string Email { get; set; } = string.Empty;

	[Required(ErrorMessage = "Password is required")]
	public string Password { get; set; } = "";

	[Required]
	public DateTime CreatedAt { get; set; } = DateTime.Now;

	[Required]
	public DateTime UpdatedAt { get; set; } = DateTime.Now;

	public string? ProfileImage { get; set; }

	public bool IsSuperAdmin { get; set; }

	/// <summary>1 = سوبر أدمن، 2 = أدمن، 3 = مستخدم. انظر <c>UserLevels</c>.</summary>
	public int Level { get; set; } = 3;

	/// <summary>من أنشأ هذا المستخدم. الأدمن يدير المستخدمين الذين أنشأهم فقط.</summary>
	public Guid? ParentUserId { get; set; }

	/// <summary>المستخدم لا يُحذف: التعطيل يمنعه من الدخول ويُبطل جلساته.</summary>
	public bool IsActive { get; set; } = true;

	/// <summary>يُخفى من قوائم المستخدمين (السوبر أدمن).</summary>
	public bool IsHidden { get; set; }

	public DateTime? DeactivatedAt { get; set; }

	public Guid? DeactivatedByUserId { get; set; }

	public ICollection<UserWalletPermission> WalletPermissions { get; set; } = new List<UserWalletPermission>();

	public ICollection<Session> Sessions { get; set; } = new List<Session>();

	public ICollection<BusinessUser> BusinessUsers { get; set; } = new List<BusinessUser>();

	public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();

	public ICollection<TransactionHistory> TransactionHistories { get; set; } = new List<TransactionHistory>();
}
