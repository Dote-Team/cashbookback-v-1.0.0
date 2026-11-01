using System;
using Microsoft.Extensions.Configuration;

namespace cashbook.Services;

/// <summary>
/// الخروج التلقائي لعدم النشاط: كل طلب من المستخدم يسجّل وقت آخر نشاط في جلسته،
/// وإن مرت مدة الخمول (Session:IdleTimeoutMinutes، 5 دقائق افتراضياً) بلا طلب تنتهي الجلسة
/// ويُرفض حتى تجديد الرمز. الواجهة ترسل POST /API/User/activity عند تحريك الفأرة أو الكتابة،
/// فيبقى المستخدم داخلاً ما دام يعمل ولو لم يرسل طلبات بيانات. القيمة 0 تعطّل الميزة.
/// </summary>
public static class SessionActivity
{
	public const int DefaultIdleTimeoutMinutes = 5;

	public const string ExpiredHeader = "X-Session-Expired";

	public static TimeSpan? IdleTimeout(IConfiguration configuration)
	{
		int minutes = configuration.GetValue<int?>("Session:IdleTimeoutMinutes") ?? DefaultIdleTimeoutMinutes;
		return minutes <= 0 ? null : TimeSpan.FromMinutes(minutes);
	}

	/// <summary>
	/// لا يُكتب وقت النشاط في قاعدة البيانات مع كل طلب: مرة كل 30 ثانية على الأكثر،
	/// أو عُشر مدة الخمول إن كانت أقصر. فيخرج المستخدم قبل الموعد بهذا القدر في أسوأ حال.
	/// </summary>
	public static TimeSpan WriteInterval(TimeSpan idleTimeout) => TimeSpan.FromTicks(Math.Min(TimeSpan.FromSeconds(30).Ticks, idleTimeout.Ticks / 10));

	public static bool IsIdle(DateTime lastActivityAt, TimeSpan? idleTimeout, DateTime now) => idleTimeout.HasValue && now - lastActivityAt > idleTimeout.Value;
}
