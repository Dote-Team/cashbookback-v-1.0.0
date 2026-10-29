using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace cashbook.Models;

/// <summary>
/// قالب بصمة مسجّل لمستخدم. القالب يُخزن مشفراً (AES-GCM) ولا يُعاد في أي استجابة.
/// البصمة لا تُحذف: إلغاؤها يجعل IsActive = false.
/// </summary>
public class UserFingerprint
{
	[Key]
	[DatabaseGenerated(DatabaseGeneratedOption.Identity)]
	public Guid Id { get; set; }

	[ForeignKey("User")]
	public Guid UserId { get; set; }
	public User User { get; set; } = null!;

	/// <summary>رقم الإصبع 0-9 (0 = إبهام اليد اليمنى ... 9 = خنصر اليسرى).</summary>
	public int FingerIndex { get; set; }

	[Required]
	public byte[] TemplateEncrypted { get; set; } = null!;

	/// <summary>خوارزمية القالب، لرفض مطابقة قوالب من محركات مختلفة.</summary>
	[Required]
	[MaxLength(32)]
	public string Algorithm { get; set; } = string.Empty;

	public bool IsActive { get; set; } = true;

	public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

	public Guid? CreatedByUserId { get; set; }

	public DateTime? RevokedAt { get; set; }

	public Guid? RevokedByUserId { get; set; }

	public DateTime? LastUsedAt { get; set; }
}
