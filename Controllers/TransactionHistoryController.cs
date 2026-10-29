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
