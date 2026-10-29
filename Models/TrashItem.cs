using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace cashbook.Models;

public class TrashItem
{
	[Key]
	[DatabaseGenerated(DatabaseGeneratedOption.Identity)]
	public Guid Id { get; set; }

	[Required]
	public string EntityType { get; set; }

	public Guid EntityId { get; set; }

	public Guid? BusinessId { get; set; }

	public Guid? BookId { get; set; }

	public Guid? DeletedByUserId { get; set; }

	public string? Name { get; set; }

	public string? DataJson { get; set; }

	/// <summary>
	/// نسخة الصفوف المرتبطة (مرفقات، قيم حقول مخصصة، حركات، إعدادات...) كي ترجع مع الاستعادة.
	/// </summary>
	public string? RelatedJson { get; set; }

	public DateTime DeletedAt { get; set; } = DateTime.Now;
}
