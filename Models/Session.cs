using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace cashbook.Models;

public class Session
{
	[Required]
	[Key]
	[DatabaseGenerated(DatabaseGeneratedOption.Identity)]
	public Guid Id { get; set; }

	[Required]
	public string RefreshToken { get; set; } = string.Empty;

	[Required]
	public string DeviceToken { get; set; } = string.Empty;

	[Required]
	[ForeignKey("User")]
	public Guid UserId { get; set; }

	public User User { get; set; } = null!;

	public DateTime ExpiredAt { get; set; }

	/// <summary>آخر طلب من المستخدم بهذه الجلسة (UTC). بعد مدة الخمول بلا نشاط تنتهي الجلسة. انظر SessionActivity.</summary>
	public DateTime LastActivityAt { get; set; }
}
