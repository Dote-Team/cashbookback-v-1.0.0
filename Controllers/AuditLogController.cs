using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Swashbuckle.AspNetCore.Annotations;
using cashbook.Data;
using cashbook.Dto;
using cashbook.Dto.audit;
using cashbook.Helper;
using cashbook.Interfaces;
using cashbook.Models;
using cashbook.Models.Constants;

namespace cashbook.Controllers;

[Route("API/[Controller]")]
[ApiController]
[Authorize]
public class AuditLogController : ControllerBase
{
	private sealed record AccessResult(bool Allowed, bool Unrestricted, Guid? BusinessId, List<Guid>? BookIds, string? Error);

	private const int DefaultRangeDays = 30;

	private const int MaxRangeDays = 366;

	private const int MaxPageSize = 200;

	private readonly APIResponse _response = new APIResponse();

	private readonly IAuditLogRepository _auditRepository;

	private readonly ApplicationDbContext _context;

	private readonly IAuditLogger _auditLogger;

	private readonly AuditSettings _settings;

	private readonly ILogger<AuditLogController> _logger;

	public AuditLogController(IAuditLogRepository auditRepository, ApplicationDbContext context, IAuditLogger auditLogger, IOptions<AuditSettings> settings, ILogger<AuditLogController> logger)
	{
		_auditRepository = auditRepository;
		_context = context;
		_auditLogger = auditLogger;
		_settings = settings.Value;
		_logger = logger;
	}

	[HttpGet]
	[ProducesResponseType(200)]
	[ProducesResponseType(401)]
	[ProducesResponseType(403)]
	[SwaggerOperation(null, null, Summary = "بحث م\u064fصف\u0651ى في سجل التدقيق", Description = "المدير الرئيسي يرى كل السجل، وصاحب أو شريك المنشأة يرى سجل منشأته فقط. الأوقات بتوقيت UTC. المدى الزمني الافتراضي آخر 30 يوما\u064b وحد\u0651ه الأقصى سنة.")]
	public async Task<ActionResult<APIResponse>> Search([FromQuery] AuditLogRequest request)
	{
		AccessResult access = await ResolveAccessAsync(request.BusinessId);
		if (!access.Allowed)
		{
			return AccessDenied(access.Error);
		}
		try
		{
			AuditLogQuery query = ToQuery(request, access);
			PaginatedResponse<AuditLogListItemDto> page = await _auditRepository.SearchAsync(query);
			_response.Result = page;
			_response.StatusCode = HttpStatusCode.OK;
			_response.IsSuccess = true;
			return Ok(_response);
		}
		catch (Exception exception)
		{
			return ServerError(exception, "GetAuditLogs");
		}
	}

	[HttpGet("{id:long}")]
	[ProducesResponseType(200)]
	[ProducesResponseType(404)]
	[ProducesResponseType(403)]
	[SwaggerOperation(null, null, Summary = "تفاصيل سطر واحد — يشمل جسم الطلب وفروق الحقول قبل/بعد")]
	public async Task<ActionResult<APIResponse>> GetById(long id, [FromQuery] Guid? businessId = null)
	{
		AccessResult access = await ResolveAccessAsync(businessId);
		if (!access.Allowed)
		{
			return AccessDenied(access.Error);
		}
		try
		{
			AuditLogDetailDto row = await _auditRepository.GetByIdAsync(id, ToScopeQuery(access));
			if (row == null)
			{
				return NotFoundError("لا يوجد سطر بهذا الرقم، أو أنه خارج نطاق صلاحيتك.");
			}
			_response.Result = row;
			_response.StatusCode = HttpStatusCode.OK;
			_response.IsSuccess = true;
			return Ok(_response);
		}
		catch (Exception exception)
		{
			return ServerError(exception, "GetAuditLogById");
		}
	}

	[HttpGet("trace/{correlationId:guid}")]
	[ProducesResponseType(200)]
	[ProducesResponseType(403)]
	[SwaggerOperation(null, null, Summary = "القصة الكاملة لنداء واحد", Description = "ي\u064fرجع سطر النداء مع كل تغييرات البيانات التي أنتجها، مرت\u0651بة زمنيا\u064b. هذه أدق\u0651 طريقة لمعرفة ما فعله طلب واحد بالضبط.")]
	public async Task<ActionResult<APIResponse>> GetTrace(Guid correlationId, [FromQuery] Guid? businessId = null)
	{
		AccessResult access = await ResolveAccessAsync(businessId);
		if (!access.Allowed)
		{
			return AccessDenied(access.Error);
		}
		try
		{
			List<AuditLogDetailDto> rows = await _auditRepository.GetTraceAsync(correlationId, ToScopeQuery(access));
			_response.Result = new
			{
				CorrelationId = correlationId,
				Count = rows.Count,
				Entries = rows
			};
			_response.StatusCode = HttpStatusCode.OK;
			_response.IsSuccess = true;
			return Ok(_response);
		}
		catch (Exception exception)
		{
			return ServerError(exception, "GetAuditTrace");
		}
	}

	[HttpGet("stats")]
	[ProducesResponseType(200)]
	[ProducesResponseType(403)]
	[SwaggerOperation(null, null, Summary = "ملخ\u0651ص تحليلي للفترة", Description = "إجماليات وتوزيعات وسلسلة زمنية — يجيب على «هل يجري شيء غير طبيعي؟» بلا قراءة السطور.")]
	public async Task<ActionResult<APIResponse>> GetStats([FromQuery] AuditLogRequest request)
	{
		AccessResult access = await ResolveAccessAsync(request.BusinessId);
		if (!access.Allowed)
		{
			return AccessDenied(access.Error);
		}
		try
		{
			AuditLogQuery query = ToQuery(request, access);
			AuditStatsDto stats = await _auditRepository.GetStatsAsync(query);
			_response.Result = stats;
			_response.StatusCode = HttpStatusCode.OK;
			_response.IsSuccess = true;
			return Ok(_response);
		}
		catch (Exception exception)
		{
			return ServerError(exception, "GetAuditStats");
		}
	}

	[HttpGet("export")]
	[ProducesResponseType(200)]
	[ProducesResponseType(403)]
	[SwaggerOperation(null, null, Summary = "تصدير النتائج الم\u064fصف\u0651اة إلى ملف CSV", Description = "ي\u064fصد\u064e\u0651ر حتى 20000 سطر بنفس معايير البحث. الملف بترميز UTF-8 مع علامة الترتيب حتى يفتح Excel النص\u0651 العربي صحيحا\u064b.")]
	public async Task<IActionResult> Export([FromQuery] AuditLogRequest request)
	{
		AccessResult access = await ResolveAccessAsync(request.BusinessId);
		if (!access.Allowed)
		{
			return AccessDenied(access.Error);
		}
		try
		{
			AuditLogQuery query = ToQuery(request, access);
			query.Take = 20000;
			List<AuditLogDetailDto> rows = await _auditRepository.ExportAsync(query, 20000);
			return File(fileDownloadName: $"audit-log-{DateTime.Now:yyyyMMdd-HHmm}.csv", fileContents: BuildCsv(rows), contentType: "text/csv; charset=utf-8");
		}
		catch (Exception exception)
		{
			return ServerError(exception, "ExportAuditLogs");
		}
	}

	[HttpGet("vocabulary")]
	[ProducesResponseType(200)]
	[SwaggerOperation(null, null, Summary = "قواميس السجل للواجهة", Description = "التصنيفات والأهميات والعمليات والكيانات مع تسمياتها العربية — لت\u064fبنى قوائم الفلترة في الواجهة من مصدر واحد لا من نصوص مكتوبة يدويا\u064b.")]
	public ActionResult<APIResponse> GetVocabulary()
	{
		string[] source = new string[3] { "Info", "Warning", "Critical" };
		string[] source2 = new string[15]
		{
			"Transaction", "Book", "Business", "BusinessUser", "User", "Category", "PaymentMethod", "Contact", "CustomField", "CustomFieldValue",
			"Attachement", "Setting", "ExchangeRate", "Session", "TransactionHistory"
		};
		_response.Result = new
		{
			Categories = AuditCategory.All.Select((string value) => new
			{
				Value = value,
				Label = AuditCategory.Label(value)
			}),
			Severities = source.Select((string value) => new
			{
				Value = value,
				Label = AuditSeverity.Label(value),
				Rank = AuditSeverity.Rank(value)
			}),
			Operations = new string[3] { "Added", "Modified", "Deleted" }.Select((string value) => new
			{
				Value = value,
				Label = AuditOperation.Label(value)
			}),
			Entities = source2.Select((string value) => new
			{
				Value = value,
				Label = AuditNarrator.LabelEntity(value)
			}),
			Actions = from value in DeclaredActions()
				select new
				{
					Value = value,
					Label = AuditNarrator.DescribeAction(value)
				},
			SortFields = new string[4] { "date", "duration", "severity", "user" },
			Defaults = new
			{
				RangeDays = 30,
				MaxRangeDays = 366,
				MaxPageSize = 200,
				MaxExportRows = 20000
			}
		};
		_response.StatusCode = HttpStatusCode.OK;
		_response.IsSuccess = true;
		return Ok(_response);
	}

	[HttpGet("status")]
	[ProducesResponseType(200)]
	[ProducesResponseType(403)]
	[SwaggerOperation(null, null, Summary = "حالة نظام التدقيق (مدير رئيسي فقط)", Description = "هل التسجيل يعمل؟ هل الطابور ممتلئ وت\u064fسق\u064eط سطور؟ كم عمر أقدم سطر؟ هذه الأسئلة يجب أن يكون لها جواب قبل أن ي\u064fكتشف النقص وقت الحاجة إليه.")]
	public async Task<ActionResult<APIResponse>> GetStatus()
	{
		AccessResult access = await ResolveAccessAsync(null);
		if (!access.Allowed)
		{
			return AccessDenied(access.Error);
		}
		if (!access.Unrestricted)
		{
			return AccessDenied("حالة نظام التدقيق متاحة للمدير الرئيسي فقط.");
		}
		try
		{
			var (totalRows, oldest, newest) = await _auditRepository.GetTableInfoAsync();
			_response.Result = new AuditStatusDto
			{
				Enabled = _settings.Enabled,
				PendingInQueue = _auditLogger.PendingCount,
				DroppedTotal = _auditLogger.DroppedCount,
				WrittenTotal = _auditLogger.WrittenCount,
				RetentionDays = _settings.RetentionDays,
				SlowRequestMs = _settings.SlowRequestMs,
				LogReadRequests = _settings.LogReadRequests,
				TotalRows = totalRows,
				OldestRowUtc = oldest,
				NewestRowUtc = newest
			};
			_response.StatusCode = HttpStatusCode.OK;
			_response.IsSuccess = true;
			return Ok(_response);
		}
		catch (Exception exception)
		{
			return ServerError(exception, "GetAuditStatus");
		}
	}

	private async Task<AccessResult> ResolveAccessAsync(Guid? requestedBusinessId)
	{
		Guid? userId = base.User.GetUserId();
		if (!userId.HasValue)
		{
			return new AccessResult(Allowed: false, Unrestricted: false, null, null, "هوية المستخدم غير صالحة في التوكن.");
		}
		if (await (from u in _context.Users.AsNoTracking()
			where u.Id == ((Guid?)userId).Value
			select u.IsSuperAdmin).FirstOrDefaultAsync())
		{
			return new AccessResult(Allowed: true, Unrestricted: true, requestedBusinessId, null, null);
		}
		if (!requestedBusinessId.HasValue)
		{
			return new AccessResult(Allowed: false, Unrestricted: false, null, null, "يجب تحديد businessId — سجل التدقيق مقصور على منشأتك.");
		}
		string role = await (from bu in _context.BusinessUsers.AsNoTracking()
			where bu.UserId == ((Guid?)userId).Value && bu.BusinessId == ((Guid?)requestedBusinessId).Value
			select bu.Role.ToLower()).FirstOrDefaultAsync();
		if (role == null || !Enumerable.Contains(Roles.Management, role))
		{
			return new AccessResult(Allowed: false, Unrestricted: false, null, null, "الاطلاع على سجل التدقيق متاح لصاحب المنشأة أو شريكها فقط.");
		}
		return new AccessResult(BookIds: await (from b in _context.Books.AsNoTracking()
			where b.BusinessId == ((Guid?)requestedBusinessId).Value
			select b.Id).ToListAsync(), Allowed: true, Unrestricted: false, BusinessId: requestedBusinessId, Error: null);
	}

	private static AuditLogQuery ToScopeQuery(AccessResult access)
	{
		return new AuditLogQuery
		{
			IsUnrestricted = access.Unrestricted,
			ScopeBusinessId = access.BusinessId,
			ScopeBookIds = access.BookIds
		};
	}

	private static AuditLogQuery ToQuery(AuditLogRequest request, AccessResult access)
	{
		DateTime dateTime = request.To ?? DateTime.UtcNow;
		DateTime dateTime2 = request.From ?? dateTime.AddDays(-Math.Max(1, request.Days ?? 30));
		if (dateTime2 > dateTime)
		{
			DateTime dateTime3 = dateTime;
			dateTime = dateTime2;
			dateTime2 = dateTime3;
		}
		if ((dateTime - dateTime2).TotalDays > 366.0)
		{
			dateTime2 = dateTime.AddDays(-366.0);
		}
		int num = Math.Clamp(request.Take, 1, 200);
		int num2 = Math.Max(1, request.Page);
		return new AuditLogQuery
		{
			From = dateTime2,
			To = dateTime,
			BusinessId = request.BusinessId,
			BookId = request.BookId,
			CorrelationId = request.CorrelationId,
			UserId = request.UserId,
			SessionId = request.SessionId,
			Username = request.Username,
			IpAddress = request.IpAddress,
			EntityName = request.EntityName,
			EntityId = request.EntityId,
			Category = request.Category,
			Action = request.Action,
			Severity = request.Severity,
			IsSuccess = request.IsSuccess,
			OnlyFailures = request.OnlyFailures,
			OnlySlow = request.OnlySlow,
			OnlySecurityEvents = request.OnlySecurityEvents,
			Search = request.Search,
			Skip = (num2 - 1) * num,
			Take = num,
			SortBy = (string.IsNullOrWhiteSpace(request.SortBy) ? "date" : request.SortBy),
			SortDirection = (string.IsNullOrWhiteSpace(request.SortDirection) ? "desc" : request.SortDirection),
			IsUnrestricted = access.Unrestricted,
			ScopeBusinessId = access.BusinessId,
			ScopeBookIds = access.BookIds
		};
	}

	private static IEnumerable<string> DeclaredActions()
	{
		return from field in typeof(AuditAction).GetFields(BindingFlags.Static | BindingFlags.Public)
			where field.IsLiteral && field.FieldType == typeof(string)
			select (string)field.GetRawConstantValue() into value
			orderby value
			select value;
	}

	private static byte[] BuildCsv(List<AuditLogDetailDto> rows)
	{
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.AppendLine(string.Join(',', "الوقت (UTC)", "التصنيف", "العملية", "الأهمية", "نجحت", "رمز الاستجابة", "المستخدم", "المنشأة", "الخزنة", "الكيان", "نوع التغيير", "م\u064fعر\u0651ف الكيان", "الطريقة", "المسار", "عنوان الشبكة", "المدة (مللي)", "بطيء", "الخلاصة", "التفاصيل", "الخطأ"));
		foreach (AuditLogDetailDto row in rows)
		{
			stringBuilder.AppendLine(string.Join(',', Csv(row.OccurredAt.ToString("yyyy-MM-dd HH:mm:ss")), Csv(row.CategoryLabel), Csv(row.Action), Csv(row.SeverityLabel), Csv(row.IsSuccess ? "نعم" : "لا"), Csv(row.StatusCode?.ToString()), Csv(row.Username), Csv(row.BusinessId?.ToString("N")), Csv(row.BookId?.ToString("N")), Csv(row.EntityLabel ?? row.EntityName), Csv(row.OperationLabel), Csv(row.EntityId), Csv(row.HttpMethod), Csv(row.Path), Csv(row.IpAddress), Csv(row.DurationMs?.ToString()), Csv(row.IsSlow ? "نعم" : "لا"), Csv(row.Summary), Csv(row.DataJson), Csv(row.Error)));
		}
		return new UTF8Encoding(encoderShouldEmitUTF8Identifier: true).GetBytes(stringBuilder.ToString());
	}

	private static string Csv(string? value)
	{
		if (string.IsNullOrEmpty(value))
		{
			return string.Empty;
		}
		string text = value.Replace("\r", " ").Replace("\n", " ").Trim();
		if (text.Contains(',') || text.Contains('"'))
		{
			text = "\"" + text.Replace("\"", "\"\"") + "\"";
		}
		return text;
	}

	private ActionResult AccessDenied(string message)
	{
		_response.StatusCode = HttpStatusCode.Forbidden;
		_response.IsSuccess = false;
		_response.ErrorMessages = new List<string> { message };
		return StatusCode(403, _response);
	}

	private ActionResult NotFoundError(string message)
	{
		_response.StatusCode = HttpStatusCode.NotFound;
		_response.IsSuccess = false;
		_response.ErrorMessages = new List<string> { message };
		return NotFound(_response);
	}

	private ActionResult ServerError(Exception exception, string operation)
	{
		_logger.LogError(exception, "فشل {Operation} في سجل التدقيق", operation);
		_response.StatusCode = HttpStatusCode.InternalServerError;
		_response.IsSuccess = false;
		_response.ErrorMessages = new List<string> { "تعذ\u0651ر تنفيذ الطلب في سجل التدقيق. راجع مسؤول النظام." };
		return StatusCode(500, _response);
	}
}
