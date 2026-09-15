using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using cashbook.Helper;
using cashbook.Interfaces;
using cashbook.Models;
using cashbook.Services;

namespace cashbook.Middleware;

public sealed class AuditLogMiddleware
{
	private const long MaxReadableBodyBytes = 262144L;

	private readonly RequestDelegate _next;

	private readonly IAuditLogger _auditLogger;

	private readonly AuditSettings _settings;

	private readonly ILogger<AuditLogMiddleware> _logger;

	public AuditLogMiddleware(RequestDelegate next, IAuditLogger auditLogger, IOptions<AuditSettings> settings, ILogger<AuditLogMiddleware> logger)
	{
		_next = next;
		_auditLogger = auditLogger;
		_settings = settings.Value;
		_logger = logger;
	}

	public async Task InvokeAsync(HttpContext context)
	{
		if (!_settings.Enabled || ShouldSkip(context.Request))
		{
			await _next(context);
			return;
		}
		AuditScope scope;
		using (AuditScope.Begin(out scope))
		{
			PopulateActor(context, scope);
			scope.HttpMethod = context.Request.Method;
			scope.Path = context.Request.Path;
			scope.IpAddress = ResolveClientIp(context);
			scope.UserAgent = Truncate(context.Request.Headers.UserAgent.ToString(), 500);
			scope.BusinessId = ExtractBusinessId(context);
			if (_settings.LogRequestBodies)
			{
				AuditScope auditScope = scope;
				auditScope.RequestBody = await ReadRequestBodyAsync(context.Request);
			}
			TryExtractUsernameFromBody(scope);
			scope.Action = AuditNarrator.Classify(context.Request.Method, context.Request.Path, 200, scope.RequestBody).Action;
			Stopwatch stopwatch = Stopwatch.StartNew();
			Exception failure = null;
			try
			{
				await _next(context);
			}
			catch (Exception ex)
			{
				Exception exception = ex;
				failure = exception;
				throw;
			}
			finally
			{
				stopwatch.Stop();
				RecordRequest(context, scope, stopwatch.ElapsedMilliseconds, failure);
			}
		}
	}

	private void RecordRequest(HttpContext context, AuditScope scope, long elapsedMs, Exception? failure)
	{
		try
		{
			int num = ((failure != null) ? 500 : context.Response.StatusCode);
			(string Action, string Category, string Severity) tuple = AuditNarrator.Classify(context.Request.Method, context.Request.Path, num, scope.RequestBody);
			string item = tuple.Action;
			string item2 = tuple.Category;
			string item3 = tuple.Severity;
			bool isSuccess = num < 400;
			bool isSlow = elapsedMs >= _settings.SlowRequestMs;
			AuditLog entry = new AuditLog
			{
				OccurredAt = DateTime.UtcNow,
				CorrelationId = scope.CorrelationId,
				Category = item2,
				Action = item,
				Severity = item3,
				IsSuccess = isSuccess,
				StatusCode = num,
				UserId = scope.UserId,
				Username = scope.ActorName,
				BusinessId = scope.BusinessId,
				SessionId = scope.SessionId,
				DeviceToken = scope.DeviceToken,
				HttpMethod = context.Request.Method,
				Path = Truncate(context.Request.Path.Value, 500),
				QueryString = (_settings.LogQueryStrings ? SensitiveDataMasker.MaskQueryString(context.Request.QueryString.Value) : null),
				IpAddress = scope.IpAddress,
				UserAgent = scope.UserAgent,
				DurationMs = elapsedMs,
				IsSlow = isSlow,
				DataJson = scope.RequestBody,
				Summary = BuildSummary(scope, item, num, context, elapsedMs, isSlow),
				Error = ((failure == null) ? null : Truncate(BuildErrorText(failure), 2000))
			};
			_auditLogger.Enqueue(entry);
		}
		catch (Exception exception)
		{
			_logger.LogError(exception, "تعذ\u0651ر تسجيل سطر التدقيق للمسار {Path}", context.Request.Path);
		}
	}

	private string BuildSummary(AuditScope scope, string action, int statusCode, HttpContext context, long elapsedMs, bool isSlow)
	{
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.Append(AuditNarrator.DescribeAction(action));
		stringBuilder.Append(" — ");
		stringBuilder.Append(context.Request.Method);
		stringBuilder.Append(' ');
		stringBuilder.Append(context.Request.Path);
		if (scope.ChangeCount > 0)
		{
			StringBuilder stringBuilder2 = stringBuilder;
			StringBuilder stringBuilder3 = stringBuilder2;
			StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(29, 1, stringBuilder2);
			handler.AppendLiteral(" (نتج عنه ");
			handler.AppendFormatted(scope.ChangeCount);
			handler.AppendLiteral(" تغيير في البيانات)");
			stringBuilder3.Append(ref handler);
		}
		if (scope.ChangesTruncated)
		{
			stringBuilder.Append(" (وس\u064fج\u0651ل جزء من التغييرات فقط لتجاوزها الحد)");
		}
		if (statusCode >= 400)
		{
			StringBuilder stringBuilder2 = stringBuilder;
			StringBuilder stringBuilder4 = stringBuilder2;
			StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(17, 1, stringBuilder2);
			handler.AppendLiteral(" — ر\u064fفض/فشل برمز ");
			handler.AppendFormatted(statusCode);
			stringBuilder4.Append(ref handler);
		}
		if (isSlow)
		{
			StringBuilder stringBuilder2 = stringBuilder;
			StringBuilder stringBuilder5 = stringBuilder2;
			StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(25, 1, stringBuilder2);
			handler.AppendLiteral(" — طلب بطيء (");
			handler.AppendFormatted(elapsedMs);
			handler.AppendLiteral(" مللي ثانية)");
			stringBuilder5.Append(ref handler);
		}
		return Truncate(stringBuilder.ToString(), 1000);
	}

	private string BuildErrorText(Exception exception)
	{
		return _settings.IncludeStackTrace ? $"{exception.GetType().Name}: {exception.Message}\n{exception.StackTrace}" : (exception.GetType().Name + ": " + exception.Message);
	}

	private static void PopulateActor(HttpContext context, AuditScope scope)
	{
		scope.UserId = context.User.GetUserId();
		scope.Username = context.User.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name")?.Value;
		if (Guid.TryParse(context.User.FindFirst("sessionId")?.Value, out var result))
		{
			scope.SessionId = result;
		}
		if (context.Items.TryGetValue("Audit:DeviceToken", out object value))
		{
			scope.DeviceToken = Truncate(value?.ToString(), 200);
		}
	}

	private static void TryExtractUsernameFromBody(AuditScope scope)
	{
		if (!string.IsNullOrWhiteSpace(scope.Username) || string.IsNullOrWhiteSpace(scope.RequestBody))
		{
			return;
		}
		try
		{
			JObject jObject = JObject.Parse(scope.RequestBody);
			string value = jObject["username"]?.ToString();
			if (!string.IsNullOrWhiteSpace(value))
			{
				scope.Username = Truncate(value, 100);
			}
		}
		catch (JsonException)
		{
		}
	}

	private async Task<string?> ReadRequestBodyAsync(HttpRequest request)
	{
		if (!IsReadableJson(request))
		{
			return null;
		}
		long? contentLength = request.ContentLength;
		if (contentLength.HasValue && contentLength.GetValueOrDefault() > 262144)
		{
			return $"{{\"note\":\"جسم الطلب كبير ({request.ContentLength} بايت) ولم ي\u064fسج\u064e\u0651ل\"}}";
		}
		try
		{
			request.EnableBuffering();
			request.Body.Position = 0L;
			using StreamReader reader = new StreamReader(request.Body, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, -1, leaveOpen: true);
			string body = await reader.ReadToEndAsync();
			request.Body.Position = 0L;
			return SensitiveDataMasker.MaskJson(body, _settings.MaxBodyLength);
		}
		catch (Exception exception)
		{
			_logger.LogWarning(exception, "تعذ\u0651ر قراءة جسم الطلب للمسار {Path}", request.Path);
			return null;
		}
	}

	private static bool IsReadableJson(HttpRequest request)
	{
		long? contentLength = request.ContentLength;
		bool flag;
		if (contentLength.HasValue)
		{
			long valueOrDefault = contentLength.GetValueOrDefault();
			if (valueOrDefault != 0L)
			{
				flag = false;
				goto IL_0024;
			}
		}
		flag = true;
		goto IL_0024;
		IL_0024:
		if (flag)
		{
			return false;
		}
		string contentType = request.ContentType;
		if (string.IsNullOrEmpty(contentType))
		{
			return false;
		}
		return contentType.StartsWith("application/json", StringComparison.OrdinalIgnoreCase) || contentType.Contains("+json", StringComparison.OrdinalIgnoreCase);
	}

	private bool ShouldSkip(HttpRequest request)
	{
		if (!_settings.LogReadRequests && HttpMethods.IsGet(request.Method))
		{
			return true;
		}
		string value = request.Path.Value;
		if (string.IsNullOrEmpty(value))
		{
			return false;
		}
		string[] excludedPaths = _settings.ExcludedPaths;
		foreach (string value2 in excludedPaths)
		{
			if (!string.IsNullOrWhiteSpace(value2) && value.StartsWith(value2, StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}
		}
		return false;
	}

	private static Guid? ExtractBusinessId(HttpContext context)
	{
		if (context.Request.RouteValues.TryGetValue("businessId", out object value) && Guid.TryParse(value?.ToString(), out var result))
		{
			return result;
		}
		if (Guid.TryParse(context.Request.Query["businessId"].ToString(), out var result2))
		{
			return result2;
		}
		return null;
	}

	private static string? ResolveClientIp(HttpContext context)
	{
		IPAddress remoteIpAddress = context.Connection.RemoteIpAddress;
		if (remoteIpAddress != null && !IPAddress.IsLoopback(remoteIpAddress))
		{
			return Truncate(remoteIpAddress.ToString(), 64);
		}
		string text = context.Request.Headers["X-Forwarded-For"].ToString();
		if (!string.IsNullOrWhiteSpace(text))
		{
			string value = text.Split(',')[0].Trim();
			if (!string.IsNullOrWhiteSpace(value))
			{
				return Truncate(value, 64);
			}
		}
		return (remoteIpAddress == null) ? null : Truncate(remoteIpAddress.ToString(), 64);
	}

	private static string? Truncate(string? value, int maxLength)
	{
		if (string.IsNullOrEmpty(value))
		{
			return value;
		}
		return (value.Length <= maxLength) ? value : (value.Substring(0, maxLength) + "…");
	}
}
