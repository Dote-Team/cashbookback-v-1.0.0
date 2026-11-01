using cashbook.Data;
using cashbook.Dto;
using cashbook.Interfaces;
using cashbook.Models;
using cashbook.Models.Constants;
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

    private readonly TimeSpan? _idleTimeout;

    public SessionValidationMiddleware(RequestDelegate next, IConfiguration configuration)
    {
        _next = next;
        _idleTimeout = SessionActivity.IdleTimeout(configuration);
    }

    public async Task InvokeAsync(HttpContext context, ApplicationDbContext dbContext, ISessionCache sessionCache, IAuditLogger auditLogger)
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

            // رمز التحديث لا يصلح للدخول إلى الـ API، ولو كان موقّعاً وصالح المدة.
            if (context.User.FindFirst(TokenService.TokenTypeClaim)?.Value != TokenService.AccessTokenType)
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsync("Invalid token type.");
                return;
            }

            // من الذاكرة إن قُرئت الجلسة قبل لحظات، وإلا من قاعدة البيانات. انظر SessionCache.
            CachedSession? session = sessionCache.Get(sessionId, userId);
            if (session == null)
            {
                session = await dbContext.Sessions
                    .Where(s => s.Id == sessionId && s.UserId == userId)
                    .Select(s => new CachedSession(s.Id, s.UserId, s.ExpiredAt, s.DeviceToken, s.User.IsActive, s.LastActivityAt))
                    .FirstOrDefaultAsync();
                if (session != null)
                {
                    sessionCache.Set(session);
                }
            }

            if (session == null || session.ExpiredAt < DateTime.UtcNow || !session.UserActive)
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsync("Session expired or not found.");
                return;
            }

            DateTime now = DateTime.UtcNow;
            if (SessionActivity.IsIdle(session.LastActivityAt, _idleTimeout, now))
            {
                // تنتهي الجلسة نفسها، لا هذا الطلب فقط: رمز التحديث لا يعيدها.
                await dbContext.Sessions.Where(s => s.Id == sessionId)
                    .ExecuteUpdateAsync(u => u.SetProperty(s => s.ExpiredAt, now));
                sessionCache.InvalidateSession(sessionId);
                if (AuditScope.Current is { } idleAudit)
                {
                    idleAudit.SessionId = sessionId;
                    idleAudit.OutcomeAction = AuditAction.SessionIdleTimeout;
                }
                else
                {
                    // طلبات القراءة لا تمر بسجل التدقيق (LogReadRequests)، والخروج حدث أمني يُسجَّل دائماً.
                    auditLogger.Enqueue(new AuditLog
                    {
                        CorrelationId = Guid.NewGuid(),
                        Category = "Auth",
                        Action = AuditAction.SessionIdleTimeout,
                        Severity = "Warning",
                        Summary = AuditAction.Label(AuditAction.SessionIdleTimeout),
                        IsSuccess = false,
                        StatusCode = StatusCodes.Status401Unauthorized,
                        UserId = userId,
                        SessionId = sessionId,
                        DeviceToken = session.DeviceToken,
                        HttpMethod = context.Request.Method,
                        Path = context.Request.Path.Value
                    });
                }
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.Headers[SessionActivity.ExpiredHeader] = "idle";
                await context.Response.WriteAsJsonAsync(new APIResponse
                {
                    StatusCode = System.Net.HttpStatusCode.Unauthorized,
                    IsSuccess = false,
                    ErrorMessages = { "انتهت الجلسة لعدم النشاط. سجّل الدخول مجدداً." }
                });
                return;
            }
            if (_idleTimeout is { } idle && now - session.LastActivityAt >= SessionActivity.WriteInterval(idle))
            {
                await dbContext.Sessions.Where(s => s.Id == sessionId)
                    .ExecuteUpdateAsync(u => u.SetProperty(s => s.LastActivityAt, now));
                session = session with { LastActivityAt = now };
                sessionCache.Set(session);
            }

            context.Items[DeviceTokenItemKey] = session.DeviceToken;

            // الكتابة في سياق التدقيق مباشرةً — لا في عنصر الطلب فقط.
            // السبب: وسيط سجل التدقيق يعمل *قبل* هذا الوسيط، فلو قرأ البصمة في بدايته
            // لوجدها فارغة. أما الكتابة هنا فتحدث قبل تنفيذ المتحكّم، فتلتقطها أيضاً
            // سطور تغييرات البيانات التي ينتجها المتحكّم بعد قليل.
            if (AuditScope.Current is { } audit)
            {
                audit.DeviceToken = session.DeviceToken;
                audit.SessionId = session.SessionId;
            }
        }

        await _next(context);
    }
}
