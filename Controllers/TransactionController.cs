using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using cashbook.Data;
using cashbook.Dto;
using cashbook.Dto.Book;
using cashbook.Dto.transaction;
using cashbook.Helper;
using cashbook.Interfaces;
using cashbook.Models;
using cashbook.Services;
using cashbook.Validators;

namespace cashbook.Controllers;

/// <summary>
/// القيود. كل فحص يقع على خزنة القيد نفسها، ومعامل businessId القادم من الطلب يُقبل
/// للتوافق مع الواجهة لكنه لا يُستخدم في أي قرار صلاحية.
/// </summary>
[Route("API/[Controller]")]
[Authorize]
public class TransactionController : ApiControllerBase
{
	private readonly ITransactionRepository _transactionRepository;

	private readonly IPermissionService _permissions;

	private readonly ApplicationDbContext _context;

	public TransactionController(ITransactionRepository transactionRepository, IPermissionService permissions, ApplicationDbContext context)
	{
		_transactionRepository = transactionRepository;
		_permissions = permissions;
		_context = context;
	}

	[HttpGet]
	public async Task<ActionResult<APIResponse>> GetPaginatedTransactions([FromQuery] Guid bookId, [FromQuery] Guid businessId, [FromQuery] int? skip = 1, [FromQuery] int? take = 25, [FromQuery] decimal? amount = null, [FromQuery] string? searchCategory = null, [FromQuery] string? searchContact = null, [FromQuery] string? searchPaymentMethod = null, [FromQuery] string? searchType = null, [FromQuery] string? searchUser = null, [FromQuery] DateTime? startDate = null, [FromQuery] DateTime? endDate = null, [FromQuery] SortField? sortBy = null, [FromQuery] SortDirection? sortDirection = SortDirection.desc)
	{
		if (CallerId is not Guid callerId)
		{
			return NoCaller();
		}
		BookAccess access = await _permissions.CheckBookAsync(callerId, bookId, Permissions.TransactionView);
		if (access.Status != AccessStatus.Granted)
		{
			return Denied(access.Status, "لا تملك صلاحية عرض قيود هذه الخزنة.");
		}
		PaginatedResponse<TransactionDto> page = await _transactionRepository.GetAllTransactionsByBookIdAsync(bookId, Page(skip), PageSize(take, 500), amount, searchCategory, searchContact, searchPaymentMethod, searchType, searchUser, startDate, endDate, sortBy, sortDirection);
		return Success(page);
	}

	[HttpGet("RawByBookId")]
	public async Task<ActionResult<APIResponse>> GetAllTransactionsRawByBookId([FromQuery] Guid bookId, [FromQuery] Guid businessId)
	{
		if (CallerId is not Guid callerId)
		{
			return NoCaller();
		}
		BookAccess access = await _permissions.CheckBookAsync(callerId, bookId, Permissions.TransactionExport);
		if (access.Status != AccessStatus.Granted)
		{
			return Denied(access.Status, "لا تملك صلاحية تصدير قيود هذه الخزنة.");
		}
		return Success(await _transactionRepository.GetAllTransactionsRawByBookIdAsync(bookId));
	}

	[HttpGet("{id:Guid}", Name = "GetTransaction")]
	public async Task<ActionResult<APIResponse>> GetTransaction(Guid id)
	{
		if (CallerId is not Guid callerId)
		{
			return NoCaller();
		}
		Transaction? transaction = await _transactionRepository.GetAsync(t => t.Id == id, tracked: false);
		if (transaction == null)
		{
			return Fail(HttpStatusCode.NotFound, "القيد غير موجود.");
		}
		BookAccess access = await _permissions.CheckBookAsync(callerId, transaction.BookId, Permissions.TransactionView);
		if (access.Status != AccessStatus.Granted)
		{
			return Denied(access.Status, "لا تملك صلاحية عرض هذا القيد.");
		}
		return Success(transaction);
	}

	[HttpPost]
	public async Task<ActionResult<APIResponse>> CreateTransaction([FromForm] CreateTransactionDto dto, [FromQuery] Guid businessId)
	{
		if (CallerId is not Guid callerId)
		{
			return NoCaller();
		}
		if (dto == null)
		{
			return Fail(HttpStatusCode.BadRequest, "بيانات غير صالحة.");
		}
		if (dto.BookId == Guid.Empty)
		{
			return Fail(HttpStatusCode.BadRequest, "الخزنة غير محددة.");
		}
		BookAccess access = await _permissions.CheckBookAsync(callerId, dto.BookId, Permissions.TransactionCreate);
		if (access.Status != AccessStatus.Granted)
		{
			return Denied(access.Status, "لا تملك صلاحية إضافة قيد في هذه الخزنة.");
		}
		List<string> validationErrors = TransactionValidator.ValidateCreate(dto);
		string? referenceError = await ValidateReferencesAsync(access.BusinessId, dto.CategoryId, dto.ContactId, dto.PaymentMethodId);
		if (referenceError != null)
		{
			validationErrors.Add(referenceError);
		}
		if (validationErrors.Any())
		{
			return StatusCode(400, new APIResponse
			{
				StatusCode = HttpStatusCode.BadRequest,
				IsSuccess = false,
				ErrorMessages = validationErrors
			});
		}
		await _transactionRepository.CreateTransactionAsync(dto, callerId);
		return Success(new { message = "Transaction created successfully." });
	}

	/// <summary>لا حذف في النظام: هذا المسار يؤرشف القيد دون رجعة. السبب اختياري.</summary>
	[HttpDelete("{id:Guid}", Name = "DeleteTransaction")]
	public Task<ActionResult<APIResponse>> DeleteTransaction(Guid id, [FromQuery] string? reason = null) => ArchiveCore(id, reason);

	[HttpPost("{id:Guid}/archive")]
	public Task<ActionResult<APIResponse>> ArchiveTransaction(Guid id, [FromQuery] string? reason = null) => ArchiveCore(id, reason);

	private async Task<ActionResult<APIResponse>> ArchiveCore(Guid id, string? reason)
	{
		if (CallerId is not Guid callerId)
		{
			return NoCaller();
		}
		Transaction? target = await _transactionRepository.GetAsync(t => t.Id == id, tracked: false);
		if (target == null)
		{
			return Fail(HttpStatusCode.NotFound, "القيد غير موجود.");
		}
		BookAccess access = await _permissions.CheckBookAsync(callerId, target.BookId, Permissions.TransactionArchive);
		if (access.Status != AccessStatus.Granted)
		{
			return Denied(access.Status, "لا تملك صلاحية أرشفة قيد في هذه الخزنة.");
		}
		await _transactionRepository.ArchiveTransactionAsync(id, callerId, reason);
		return Success(new { archived = true });
	}

	[HttpPut("{id:Guid}", Name = "UpdateTransaction")]
	public async Task<ActionResult<APIResponse>> UpdateTransaction([FromRoute] Guid id, [FromQuery] Guid businessId, [FromForm] UpdateTransactionDto updateTransactionDto)
	{
		if (CallerId is not Guid callerId)
		{
			return NoCaller();
		}
		if (updateTransactionDto == null)
		{
			return Fail(HttpStatusCode.BadRequest, "بيانات غير صالحة.");
		}
		Transaction? existing = await _transactionRepository.GetAsync(t => t.Id == id, tracked: false);
		if (existing == null)
		{
			return Fail(HttpStatusCode.NotFound, "القيد غير موجود.");
		}
		BookAccess access = await _permissions.CheckBookAsync(callerId, existing.BookId, Permissions.TransactionEdit);
		if (access.Status != AccessStatus.Granted)
		{
			return Denied(access.Status, "لا تملك صلاحية تعديل قيد في هذه الخزنة.");
		}
		List<string> validationErrors = TransactionValidator.ValidateUpdate(updateTransactionDto, existing, out ResolvedTransactionValues _);
		// القيمة التي لم تتغير لا تُفحص: قيد قديم يبقى قابلاً للتعديل ولو أُرشف تصنيفه لاحقاً.
		string? referenceError = await ValidateReferencesAsync(access.BusinessId,
			updateTransactionDto.CategoryId == existing.CategoryId ? null : updateTransactionDto.CategoryId,
			updateTransactionDto.ContactId == existing.ContactId ? null : updateTransactionDto.ContactId,
			updateTransactionDto.PaymentMethodId == existing.PaymentMethodId ? null : updateTransactionDto.PaymentMethodId);
		if (referenceError != null)
		{
			validationErrors.Add(referenceError);
		}
		if (validationErrors.Any())
		{
			return StatusCode(400, new APIResponse
			{
				StatusCode = HttpStatusCode.BadRequest,
				IsSuccess = false,
				ErrorMessages = validationErrors
			});
		}
		if (!await _transactionRepository.UpdateTransactionAsync(updateTransactionDto, id, callerId))
		{
			return Fail(HttpStatusCode.NotFound, "القيد غير موجود.");
		}
		return Success();
	}

	[HttpPost("{id:Guid}/DuplicateToBook")]
	public async Task<ActionResult<APIResponse>> DuplicateTransactionToAnotherBook([FromRoute] Guid id, [FromQuery] Guid targetBookId, [FromQuery] Guid businessId)
	{
		if (CallerId is not Guid callerId)
		{
			return NoCaller();
		}
		if (id == Guid.Empty || targetBookId == Guid.Empty)
		{
			return Fail(HttpStatusCode.BadRequest, "معرّف القيد أو الخزنة الهدف غير صالح.");
		}
		Transaction? source = await _transactionRepository.GetAsync(t => t.Id == id, tracked: false);
		if (source == null)
		{
			return Fail(HttpStatusCode.NotFound, "القيد غير موجود.");
		}
		BookAccess sourceAccess = await _permissions.CheckBookAsync(callerId, source.BookId, Permissions.TransactionDuplicate);
		if (sourceAccess.Status != AccessStatus.Granted)
		{
			return Denied(sourceAccess.Status, "لا تملك صلاحية نسخ قيد من هذه الخزنة.");
		}
		BookAccess targetAccess = await _permissions.CheckBookAsync(callerId, targetBookId, Permissions.TransactionCreate);
		if (targetAccess.Status == AccessStatus.NotFound)
		{
			return Fail(HttpStatusCode.NotFound, "الخزنة الهدف غير موجودة.");
		}
		// النسخ بين منشأتين مختلفتين يُرفض: قيد منشأة لا يُدخل في دفاتر منشأة أخرى.
		if (targetAccess.BusinessId != sourceAccess.BusinessId)
		{
			return Fail(HttpStatusCode.Forbidden, "الخزنة الهدف لا تنتمي إلى المنشأة نفسها.");
		}
		if (targetAccess.Status != AccessStatus.Granted)
		{
			return Fail(HttpStatusCode.Forbidden, "لا تملك صلاحية إضافة قيد في الخزنة الهدف.");
		}
		Guid newTransactionId = await _transactionRepository.DuplicateTransactionToAnotherBookAsync(id, targetBookId, callerId);
		return Success(new
		{
			NewTransactionId = newTransactionId,
			Message = "Transaction duplicated successfully."
		});
	}

	/// <summary>التصنيف وجهة الاتصال وطريقة الدفع يجب أن تكون من منشأة الخزنة نفسها وغير مؤرشفة.</summary>
	private async Task<string?> ValidateReferencesAsync(Guid businessId, Guid? categoryId, Guid? contactId, Guid? paymentMethodId)
	{
		if (categoryId.HasValue && !await _context.Categories.AnyAsync(c => c.Id == categoryId.Value && c.BusinessId == businessId && !c.IsArchived))
		{
			return "التصنيف المحدد غير موجود في هذه المنشأة.";
		}
		if (contactId.HasValue && !await _context.Contacts.AnyAsync(c => c.Id == contactId.Value && c.BusinessId == businessId && !c.IsArchived))
		{
			return "جهة الاتصال المحددة غير موجودة في هذه المنشأة.";
		}
		if (paymentMethodId.HasValue && !await _context.PaymentMethods.AnyAsync(p => p.Id == paymentMethodId.Value && p.BusinessId == businessId && !p.IsArchived))
		{
			return "طريقة الدفع المحددة غير موجودة في هذه المنشأة.";
		}
		return null;
	}
}
