using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;
using cashbook.Dto;

namespace cashbook.Middleware;

public class ApiErrorSanitizerFilter : IAsyncResultFilter, IFilterMetadata
{
	public const string GenericMessage = "حدث خطأ غير متوقع في الخادم. حاول مرة أخرى، وإذا استمرت المشكلة راجع مسؤول النظام.";

	private readonly ILogger<ApiErrorSanitizerFilter> _logger;

	public ApiErrorSanitizerFilter(ILogger<ApiErrorSanitizerFilter> logger)
	{
		_logger = logger;
	}

	public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
	{
		APIResponse response = default(APIResponse);
		int num;
		if (context.Result is ObjectResult objectResult)
		{
			object value = objectResult.Value;
			response = value as APIResponse;
			if (response != null)
			{
				List<string> errorMessages = response.ErrorMessages;
				num = ((errorMessages != null && errorMessages.Count > 0) ? 1 : 0);
				goto IL_0062;
			}
		}
		num = 0;
		goto IL_0062;
		IL_0062:
		if (num != 0)
		{
			List<string> sanitized = null;
			for (int i = 0; i < response.ErrorMessages.Count; i++)
			{
				string message = response.ErrorMessages[i];
				if (LooksLikeInternalDetail(message))
				{
					_logger.LogError("تم حجب تفاصيل داخلية من استجابة الـ API على المسار {Path}: {Detail}", context.HttpContext.Request.Path, message);
					if (sanitized == null)
					{
						sanitized = new List<string>(response.ErrorMessages);
					}
					sanitized[i] = "حدث خطأ غير متوقع في الخادم. حاول مرة أخرى، وإذا استمرت المشكلة راجع مسؤول النظام.";
				}
			}
			if (sanitized != null)
			{
				response.ErrorMessages = sanitized;
			}
		}
		await next();
	}

	private static bool LooksLikeInternalDetail(string? message)
	{
		if (string.IsNullOrWhiteSpace(message))
		{
			return false;
		}
		return message.Contains("   at ", StringComparison.Ordinal) || message.Contains("Stack:", StringComparison.Ordinal) || message.Contains(".cs:line", StringComparison.Ordinal) || message.Contains("Microsoft.", StringComparison.Ordinal) || message.Contains("System.", StringComparison.Ordinal) || message.Contains("Swashbuckle", StringComparison.Ordinal) || message.Contains("cashbook.", StringComparison.Ordinal) || message.Contains("Server=", StringComparison.OrdinalIgnoreCase) || message.Contains("SqlException", StringComparison.Ordinal);
	}
}
