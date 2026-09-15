using cashbook.Data;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

public class SessionValidationMiddleware
{
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
        }

        await _next(context);
    }
}
