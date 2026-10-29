using System;

namespace cashbook.Models;

/// <summary>
/// سجلّ لا يُحذف من النظام، بل يُؤرشف دون إمكانية الاسترجاع.
/// المؤرشف يختفي من القوائم والأرصدة، ويبقى في قاعدة البيانات ويُعرض في شاشة الأرشيف للقراءة فقط.
/// </summary>
public interface IArchivable
{
	bool IsArchived { get; set; }

	DateTime? ArchivedAt { get; set; }

	Guid? ArchivedByUserId { get; set; }

	string? ArchiveReason { get; set; }
}
