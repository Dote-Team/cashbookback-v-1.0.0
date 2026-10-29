using System;

namespace cashbook.Dto.audit;

public class AuditLogRequest
{
	/// <summary>المنشأة. إلزامي لغير السوبر أدمن.</summary>
	public Guid? BusinessId { get; set; }

	/// <summary>خزنة واحدة فقط.</summary>
	public Guid? BookId { get; set; }

	/// <summary>كل ما يتعلق بطلب واحد.</summary>
	public Guid? CorrelationId { get; set; }

	/// <summary>آخر عدد من الأيام (الافتراضي 30). يُتجاهل إن أُرسل From أو To.</summary>
	public int? Days { get; set; }

	/// <summary>من وقت (UTC).</summary>
	public DateTime? From { get; set; }

	/// <summary>إلى وقت (UTC). المدى الأقصى سنة.</summary>
	public DateTime? To { get; set; }

	/// <summary>مستخدم معيّن.</summary>
	public Guid? UserId { get; set; }

	/// <summary>جلسة دخول معيّنة.</summary>
	public Guid? SessionId { get; set; }

	/// <summary>جزء من اسم المستخدم.</summary>
	public string? Username { get; set; }

	/// <summary>عنوان IP.</summary>
	public string? IpAddress { get; set; }

	/// <summary>نوع السجل المتغيّر، مثل Transaction أو Book (القائمة من /vocabulary).</summary>
	public string? EntityName { get; set; }

	/// <summary>معرّف السجل المتغيّر.</summary>
	public string? EntityId { get; set; }

	/// <summary>التصنيف، مثل Auth أو Data (القائمة من /vocabulary).</summary>
	public string? Category { get; set; }

	/// <summary>العملية، مثل transaction.create.</summary>
	public string? Action { get; set; }

	/// <summary>Info أو Warning أو Critical.</summary>
	public string? Severity { get; set; }

	/// <summary>true للناجحة فقط، false للفاشلة فقط.</summary>
	public bool? IsSuccess { get; set; }

	/// <summary>الطلبات الفاشلة فقط.</summary>
	public bool? OnlyFailures { get; set; }

	/// <summary>الطلبات البطيئة فقط (أبطأ من SlowRequestMs).</summary>
	public bool? OnlySlow { get; set; }

	/// <summary>أحداث الأمان فقط: الدخول والخروج ورفض الصلاحيات.</summary>
	public bool? OnlySecurityEvents { get; set; }

	/// <summary>بحث حر في الملخص والمسار.</summary>
	public string? Search { get; set; }

	/// <summary>رقم الصفحة، يبدأ من 1.</summary>
	public int Page { get; set; } = 1;

	/// <summary>عدد السطور في الصفحة.</summary>
	public int Take { get; set; } = 25;

	/// <summary>duration (المدة) أو severity (الأهمية) أو user (المستخدم). الافتراضي الوقت.</summary>
	public string? SortBy { get; set; }

	/// <summary>asc أو desc (الافتراضي desc).</summary>
	public string? SortDirection { get; set; }
}
