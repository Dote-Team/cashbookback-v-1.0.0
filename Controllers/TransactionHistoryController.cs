using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using cashbook.Data;
using cashbook.Dto;
using cashbook.Helper;
using cashbook.Interfaces;
using cashbook.Services;

namespace cashbook.Controllers;

/// <summary>سجل تغييرات القيود: من أضاف، ومن عدّل المبلغ من كم إلى كم، ومن أرشف.</summary>
[Route("API/[Controller]")]
[Authorize]
public class TransactionHistoryController : ApiControllerBase
{
	private readonly ITransactionHistoryRepository _transactionHistoryRepository;

	private readonly IPermissionService _permissions;

	private readonly ApplicationDbContext _context;

	public TransactionHistoryController(ITransactionHistoryRepository transactionHistoryRepository, IPermissionService permissions, ApplicationDbContext context)
	{
		_transactionHistoryRepository = transactionHistoryRepository;
		_permissions = permissions;
		_context = context;
	}

	/// <summary>سجل تغييرات قيود خزنة</summary>
	/// <remarks>
	/// **الصلاحية:** history.view على الخزنة.
	///
	/// كل إضافة (POST) وتعديل (PUT) وأرشفة (ARCHIVE) واستيراد (IMPORT)، مع المبلغ قبل وبعد واسم من نفّذ.
	/// </remarks>
	/// <param name="bookId">معرّف الخزنة.</param>
	/// <param name="businessId">للتوافق مع الواجهة فقط.</param>
	/// <response code="200">تمت العملية. النتيجة في الحقل result.</response>
	/// <response code="401">لم تسجّل الدخول، أو رمز الدخول منتهٍ أو غير صالح.</response>
	/// <response code="403">لا تملك الصلاحية المطلوبة.</response>
	/// <response code="404">الخزنة غير موجودة.</response>
	[HttpGet("ByBook")]
	public async Task<ActionResult<APIResponse>> GetAllByBookId([FromQuery] Guid bookId, [FromQuery] Guid businessId)
	{
		if (CallerId is not Guid callerId)
		{
			return NoCaller();
		}
		BookAccess access = await _permissions.CheckBookAsync(callerId, bookId, Permissions.HistoryView);
		if (access.Status != AccessStatus.Granted)
		{
			return Denied(access.Status, "لا تملك صلاحية عرض سجل هذه الخزنة.");
		}
		return Success(await _transactionHistoryRepository.GetAllByBookIdAsync(bookId));
	}

	/// <summary>سجل تغييرات قيد واحد</summary>
	/// <remarks>
	/// **الصلاحية:** history.view على خزنة القيد.
	/// </remarks>
	/// <param name="transactionId">معرّف القيد.</param>
	/// <param name="businessId">للتوافق مع الواجهة فقط.</param>
	/// <response code="200">تمت العملية. النتيجة في الحقل result.</response>
	/// <response code="401">لم تسجّل الدخول، أو رمز الدخول منتهٍ أو غير صالح.</response>
	/// <response code="403">لا تملك الصلاحية المطلوبة.</response>
	/// <response code="404">السجل غير موجود، أو مؤرشف.</response>
	[HttpGet("ByTransaction")]
	public async Task<ActionResult<APIResponse>> GetAllByTransactionId([FromQuery] Guid transactionId, [FromQuery] Guid businessId)
	{
		if (CallerId is not Guid callerId)
		{
			return NoCaller();
		}
		// القيد قد يكون مؤرشفاً وسجله ما زال يُعرض، فيُبحث عنه متجاوزاً فلتر الأرشفة.
		Guid? bookId = await _context.Transactions.IgnoreQueryFilters()
			.Where(t => t.Id == transactionId)
			.Select(t => (Guid?)t.BookId)
			.FirstOrDefaultAsync();
		if (!bookId.HasValue)
		{
			return Fail(HttpStatusCode.NotFound, "القيد غير موجود.");
		}
		BookAccess access = await _permissions.CheckBookAsync(callerId, bookId.Value, Permissions.HistoryView);
		if (access.Status != AccessStatus.Granted)
		{
			return Denied(access.Status, "لا تملك صلاحية عرض سجل هذا القيد.");
		}
		return Success(await _transactionHistoryRepository.GetAllByTransactionIdAsync(transactionId));
	}
}
