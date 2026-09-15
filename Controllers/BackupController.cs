using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Swashbuckle.AspNetCore.Annotations;
using cashbook.Data;
using cashbook.Dto;
using cashbook.Dto.backup;
using cashbook.Helper;
using cashbook.Interfaces;
using cashbook.Models;

namespace cashbook.Controllers;

[Route("API/[Controller]")]
[ApiController]
[Authorize]
public class BackupController : ControllerBase
{
	private const long MaxBackupFileBytes = 209715200L;

	protected APIResponse _response;

	private readonly IBackupService _backupService;

	private readonly ApplicationDbContext _context;

	public BackupController(IBackupService backupService, ApplicationDbContext context)
	{
		_backupService = backupService;
		_context = context;
		_response = new APIResponse();
	}

	private bool TryGetUserId(out Guid userId)
	{
		return Guid.TryParse(base.User.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")?.Value, out userId);
	}

	private ActionResult<APIResponse> Fail(HttpStatusCode status, string message)
	{
		_response.StatusCode = status;
		_response.IsSuccess = false;
		_response.ErrorMessages = new List<string> { message };
		return StatusCode((int)status, _response);
	}

	private IActionResult FailRaw(HttpStatusCode status, string message)
	{
		_response.StatusCode = status;
		_response.IsSuccess = false;
		_response.ErrorMessages = new List<string> { message };
		return StatusCode((int)status, _response);
	}

	private async Task<bool> IsManagementAsync(Guid userId, Guid businessId)
	{
		string role = await (from bu in _context.BusinessUsers
			where bu.UserId == userId && bu.BusinessId == businessId
			select bu.Role.ToLower()).FirstOrDefaultAsync();
		return role != null && Enumerable.Contains(Roles.Management, role);
	}

	private static string BuildFileName(string bookName)
	{
		string value = new string((bookName ?? "book").Where((char c) => !Enumerable.Contains(Path.GetInvalidFileNameChars(), c)).ToArray()).Trim();
		if (string.IsNullOrWhiteSpace(value))
		{
			value = "book";
		}
		return $"backup-{value}-{DateTime.Now:yyyyMMdd-HHmmss}.cbk";
	}

	[HttpPost("export")]
	[SwaggerOperation(null, null, Summary = "owner , partner")]
	[ProducesResponseType(200)]
	[ProducesResponseType(400)]
	[ProducesResponseType(401)]
	[ProducesResponseType(403)]
	[ProducesResponseType(404)]
	public async Task<IActionResult> Export([FromBody] BackupExportRequestDto request)
	{
		try
		{
			if (!TryGetUserId(out var userId))
			{
				return FailRaw(HttpStatusCode.Unauthorized, "Invalid user ID.");
			}
			if (request == null || request.BookId == Guid.Empty)
			{
				return FailRaw(HttpStatusCode.BadRequest, "الخزنة غير محددة.");
			}
			if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 6)
			{
				return FailRaw(HttpStatusCode.BadRequest, "كلمة مرور النسخة الاحتياطية إلزامية ويجب ألا تقل عن 6 أحرف.");
			}
			var book = await (from b in _context.Books
				where b.Id == request.BookId
				select new { b.Id, b.Name, b.BusinessId }).FirstOrDefaultAsync();
			if (book == null)
			{
				return FailRaw(HttpStatusCode.NotFound, "الخزنة غير موجودة.");
			}
			if (!(await IsManagementAsync(userId, book.BusinessId)))
			{
				return FailRaw(HttpStatusCode.Forbidden, "لا تملك صلاحية تصدير هذه الخزنة.");
			}
			return File(await _backupService.ExportBookAsync(book.Id, request.Password, userId), "application/octet-stream", BuildFileName(book.Name));
		}
		catch (InvalidOperationException ex)
		{
			InvalidOperationException ex2 = ex;
			return FailRaw(HttpStatusCode.BadRequest, ex2.Message);
		}
		catch (Exception ex3)
		{
			Exception ex4 = ex3;
			return FailRaw(HttpStatusCode.InternalServerError, ex4.Message);
		}
	}

	[HttpPost("inspect")]
	[RequestFormLimits(MultipartBodyLengthLimit = 209715200L)]
	[ProducesResponseType(200)]
	[ProducesResponseType(400)]
	[ProducesResponseType(401)]
	public async Task<ActionResult<APIResponse>> Inspect([FromForm] BackupInspectRequestDto request)
	{
		try
		{
			if (!TryGetUserId(out var _))
			{
				return Fail(HttpStatusCode.Unauthorized, "Invalid user ID.");
			}
			IFormFile file = request?.File;
			if (file == null || file.Length == 0)
			{
				return Fail(HttpStatusCode.BadRequest, "لم يتم إرسال ملف النسخة الاحتياطية.");
			}
			if (file.Length > 209715200)
			{
				return Fail(HttpStatusCode.BadRequest, "حجم الملف يتجاوز الحد المسموح.");
			}
			using Stream stream = file.OpenReadStream();
			BackupManifestDto manifest = await _backupService.InspectAsync(stream, request?.Password ?? string.Empty);
			_response.StatusCode = HttpStatusCode.OK;
			_response.IsSuccess = true;
			_response.Result = manifest;
			return Ok(_response);
		}
		catch (InvalidOperationException ex)
		{
			InvalidOperationException ex2 = ex;
			return Fail(HttpStatusCode.BadRequest, ex2.Message);
		}
		catch (Exception ex3)
		{
			Exception ex4 = ex3;
			return Fail(HttpStatusCode.InternalServerError, ex4.Message);
		}
	}

	[HttpPost("import")]
	[RequestFormLimits(MultipartBodyLengthLimit = 209715200L)]
	[SwaggerOperation(null, null, Summary = "owner , partner")]
	[ProducesResponseType(200)]
	[ProducesResponseType(400)]
	[ProducesResponseType(401)]
	[ProducesResponseType(403)]
	public async Task<ActionResult<APIResponse>> Import([FromForm] BackupImportRequestDto request)
	{
		try
		{
			if (!TryGetUserId(out var userId))
			{
				return Fail(HttpStatusCode.Unauthorized, "Invalid user ID.");
			}
			IFormFile file = request?.File;
			Guid businessId = request?.BusinessId ?? Guid.Empty;
			if (businessId == Guid.Empty)
			{
				return Fail(HttpStatusCode.BadRequest, "المنشأة غير محددة.");
			}
			if (file == null || file.Length == 0)
			{
				return Fail(HttpStatusCode.BadRequest, "لم يتم إرسال ملف النسخة الاحتياطية.");
			}
			if (file.Length > 209715200)
			{
				return Fail(HttpStatusCode.BadRequest, "حجم الملف يتجاوز الحد المسموح.");
			}
			if (!(await IsManagementAsync(userId, businessId)))
			{
				return Fail(HttpStatusCode.Forbidden, "لا تملك صلاحية الاستيراد في هذه المنشأة.");
			}
			using Stream stream = file.OpenReadStream();
			BackupImportResultDto result = await _backupService.ImportAsync(stream, request?.Password ?? string.Empty, businessId, request?.TargetBookId, request?.NewBookName, userId);
			_response.StatusCode = HttpStatusCode.OK;
			_response.IsSuccess = true;
			_response.Result = result;
			return Ok(_response);
		}
		catch (InvalidOperationException ex)
		{
			InvalidOperationException ex2 = ex;
			return Fail(HttpStatusCode.BadRequest, ex2.Message);
		}
		catch (Exception ex3)
		{
			Exception ex4 = ex3;
			return Fail(HttpStatusCode.InternalServerError, ex4.Message);
		}
	}
}
