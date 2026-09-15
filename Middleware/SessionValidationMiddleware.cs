using cashbook.Data;
using cashbook.Services;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

/// <summary>
/// التحقق من صلاحية الجلسة قبل تمرير الطلب إلى المتحكّمات.
///
/// <para>إضافة خاصة بسجل التدقيق: هذا الوسيط هو المكان الوحيد الذي يُحمّل صفّ الجلسة أصلاً،
/// فبصمة الجهاز تُمرَّر من هنا إلى سجل التدقيق بدل أن يدفع استعلاماً ثانياً في كل طلب
/// لمجرد معرفة «من أي جهاز جاء هذا الطلب؟».</para>
/// </summary>
public class SessionValidationMiddleware
{
    /// <summary>
    /// مفتاح عنصر في <c>HttpContext.Items</c> يحمل بصمة الجهاز.
    /// يُستخدم أيضاً من خارج هذا الملف، ويبقى نصاً ثابتاً ليعمل حتى مع الكود المُجمّع مسبقاً.
    /// </summary>
    public const string DeviceTokenItemKey = "Audit:DeviceToken";

    private readonly RequestDelegate _next;

    public SessionValidationMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, ApplicationDbContext dbContext)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var userIdStr = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var sessionIdStr = context.User.FindFirst("sessionId")?.Value;

            if (!Guid.TryParse(userIdStr, out var userId) || !Guid.TryParse(sessionIdStr, out var sessionId))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsync("Invalid user or session ID.");
                return;
            }

            var session = await dbContext.Sessions.FirstOrDefaultAsync(s => s.Id == sessionId && s.UserId == userId);

            if (session == null || (session.ExpiredAt != null && session.ExpiredAt < DateTime.UtcNow))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsync("Session expired or not found.");
                return;
            }

            context.Items[DeviceTokenItemKey] = session.DeviceToken;

            // الكتابة في سياق التدقيق مباشرةً — لا في عنصر الطلب فقط.
            // السبب: وسيط سجل التدقيق يعمل *قبل* هذا الوسيط، فلو قرأ البصمة في بدايته
            // لوجدها فارغة. أما الكتابة هنا فتحدث قبل تنفيذ المتحكّم، فتلتقطها أيضاً
            // سطور تغييرات البيانات التي ينتجها المتحكّم بعد قليل.
            if (AuditScope.Current is { } audit)
            {
                audit.DeviceToken = session.DeviceToken;
                audit.SessionId = session.Id;
            }
        }

        await _next(context);
    }
}
