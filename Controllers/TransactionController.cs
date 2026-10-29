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

/// <summary>القيود (إيداع cash in وسحب cash out). لا حذف: الحذف يؤرشف القيد فيخرج من الأرصدة ويبقى في الأرشيف.</summary>
[Route("API/[Controller]")]
[Authorize]
public class TransactionController : ApiControllerBase
{
	private readonly ITransactionRepository _transactionRepository;

	private readonly IPermissionService _permissions;

	private readonly ApplicationDbContext _context;

	private readonly IFileStorage _files;

	public TransactionController(ITransactionRepository transactionRepository, IPermissionService permissions, ApplicationDbContext context, IFileStorage files)
	{
		_files = files;
		_transactionRepository = transactionRepository;
		_permissions = permissions;
		_context = context;
	}

	/// <summary>قيود خزنة (صفحات)</summary>
	/// <remarks>
	/// **الصلاحية:** transaction.view على الخزنة.
	///
	/// كل قيد معه الرصيد التراكمي بعده (newBalance)، ومرفقاته. حقل files في المرفق رابط موقّع صالح 12 ساعة:
	/// اعرضه هكذا: /API/Files/{files}.
	///
	/// الفلاتر كلها اختيارية وتُجمع معاً.
	/// </remarks>
	/// <param name="bookId">معرّف الخزنة.</param>
	/// <param name="businessId">للتوافق مع الواجهة فقط، لا يُستخدم في الصلاحية.</param>
	/// <param name="skip">رقم الصفحة، يبدأ من 1.</param>
	/// <param name="take">عدد القيود في الصفحة (حد أقصى 500).</param>
	/// <param name="amount">مبلغ مطابق تماماً.</param>
	/// <param name="searchCategory">جزء من اسم التصنيف.</param>
	/// <param name="searchContact">جزء من اسم جهة الاتصال.</param>
	/// <param name="searchPaymentMethod">جزء من اسم طريقة الدفع.</param>
	/// <param name="searchType">cash in أو cash out.</param>
	/// <param name="searchUser">جزء من اسم من أضاف القيد.</param>
	/// <param name="startDate">من تاريخ.</param>
	/// <param name="endDate">إلى تاريخ.</param>
	/// <param name="sortBy">Date أو Amount أو Description أو Category أو PaymentMethod أو NewBalance.</param>
	/// <param name="sortDirection">asc أو desc (الافتراضي desc).</param>
	/// <response code="200">تمت العملية. النتيجة في الحقل result.</response>
	/// <response code="401">لم تسجّل الدخول، أو رمز الدخول منتهٍ أو غير صالح.</response>
	/// <response code="403">لا تملك الصلاحية المطلوبة.</response>
	/// <response code="404">الخزنة غير موجودة.</response>
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

	/// <summary>تصدير كل قيود الخزنة</summary>
	/// <remarks>
	/// **الصلاحية:** transaction.export على الخزنة.
	///
	/// كل القيود غير المؤرشفة دون صفحات، للتصدير إلى Excel أو PDF.
	/// </remarks>
	/// <param name="bookId">معرّف الخزنة.</param>
	/// <param name="businessId">للتوافق مع الواجهة فقط.</param>
	/// <response code="200">تمت العملية. النتيجة في الحقل result.</response>
	/// <response code="401">لم تسجّل الدخول، أو رمز الدخول منتهٍ أو غير صالح.</response>
	/// <response code="403">لا تملك الصلاحية المطلوبة.</response>
	/// <response code="404">الخزنة غير موجودة.</response>
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

	/// <summary>قيد واحد</summary>
	/// <remarks>
	/// **الصلاحية:** transaction.view على خزنة القيد.
	/// </remarks>
	/// <param name="id">معرّف القيد.</param>
	/// <response code="200">تمت العملية. النتيجة في الحقل result.</response>
	/// <response code="401">لم تسجّل الدخول، أو رمز الدخول منتهٍ أو غير صالح.</response>
	/// <response code="403">لا تملك الصلاحية المطلوبة.</response>
	/// <response code="404">السجل غير موجود، أو مؤرشف.</response>
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

	/// <summary>إضافة قيد</summary>
	/// <remarks>
	/// **الصلاحية:** transaction.create على الخزنة.
	///
	/// يُرسل كـ **multipart/form-data** (لأجل المرفقات):
	/// - Type: cash in (إيداع) أو cash out (سحب).
	/// - Amount و Date و BookId: إلزامية.
	/// - Currency: IQD أو USD. للدولار: ExchangeRate إلزامي، و ExchangeDate للإيداع فقط.
	/// - CategoryId و ContactId و PaymentMethodId: إلزامية إن فرضتها إعدادات الخزنة.
	/// - CustomFieldValues: نص JSON، مثل [{"customFieldId":"...","value":"123"}].
	/// - Files: حتى 5 ملفات، كلٌّ حتى 10MB، صور أو PDF فقط (يُفحص المحتوى لا الامتداد).
	/// </remarks>
	/// <param name="dto">بيانات القيد.</param>
	/// <param name="businessId">للتوافق مع الواجهة فقط.</param>
	/// <response code="200">تمت العملية. النتيجة في الحقل result.</response>
	/// <response code="401">لم تسجّل الدخول، أو رمز الدخول منتهٍ أو غير صالح.</response>
	/// <response code="403">لا تملك الصلاحية المطلوبة.</response>
	/// <response code="400">بيانات الطلب ناقصة أو غير صالحة. السبب في errorMessages.</response>
	/// <response code="404">الخزنة غير موجودة.</response>
	/// <response code="413">حجم الطلب أكبر من المسموح.</response>
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
		if (_files.Validate(dto.Files, UploadKind.Attachment) is { } fileError)
		{
			validationErrors.Add(fileError);
		}
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

	/// <summary>أرشفة قيد (لا حذف)</summary>
	/// <remarks>
	/// **الصلاحية:** transaction.archive على خزنة القيد.
	///
	/// القيد يخرج من القوائم والأرصدة دون رجعة، ويبقى في الأرشيف وسجل الحركات. مثل POST /{id}/archive.
	/// </remarks>
	/// <param name="id">معرّف القيد.</param>
	/// <param name="reason">سبب الأرشفة (اختياري، يظهر في الأرشيف).</param>
	/// <response code="200">تمت العملية. النتيجة في الحقل result.</response>
	/// <response code="401">لم تسجّل الدخول، أو رمز الدخول منتهٍ أو غير صالح.</response>
	/// <response code="403">لا تملك الصلاحية المطلوبة.</response>
	/// <response code="404">السجل غير موجود، أو مؤرشف.</response>
	[HttpDelete("{id:Guid}", Name = "DeleteTransaction")]
	public Task<ActionResult<APIResponse>> DeleteTransaction(Guid id, [FromQuery] string? reason = null) => ArchiveCore(id, reason);

	/// <summary>أرشفة قيد</summary>
	/// <remarks>
	/// **الصلاحية:** transaction.archive على خزنة القيد. دون رجعة.
	/// </remarks>
	/// <param name="id">معرّف القيد.</param>
	/// <param name="reason">سبب الأرشفة (اختياري).</param>
	/// <response code="200">تمت العملية. النتيجة في الحقل result.</response>
	/// <response code="401">لم تسجّل الدخول، أو رمز الدخول منتهٍ أو غير صالح.</response>
	/// <response code="403">لا تملك الصلاحية المطلوبة.</response>
	/// <response code="404">السجل غير موجود، أو مؤرشف.</response>
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

	/// <summary>تعديل قيد</summary>
	/// <remarks>
	/// **الصلاحية:** transaction.edit على خزنة القيد.
	///
	/// multipart/form-data. أرسل الحقول التي تريد تغييرها فقط. تغيير المبلغ أو النوع أو العملة يُسجَّل في سجل الحركات (من كم إلى كم، ومن عدّل).
	/// إرسال ملفات جديدة في Files يؤرشف المرفقات القديمة ويضع الجديدة مكانها.
	/// </remarks>
	/// <param name="id">معرّف القيد.</param>
	/// <param name="businessId">للتوافق مع الواجهة فقط.</param>
	/// <param name="updateTransactionDto">الحقول المراد تغييرها.</param>
	/// <response code="200">تمت العملية. النتيجة في الحقل result.</response>
	/// <response code="401">لم تسجّل الدخول، أو رمز الدخول منتهٍ أو غير صالح.</response>
	/// <response code="403">لا تملك الصلاحية المطلوبة.</response>
	/// <response code="400">بيانات الطلب ناقصة أو غير صالحة. السبب في errorMessages.</response>
	/// <response code="404">السجل غير موجود، أو مؤرشف.</response>
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
		if (_files.Validate(updateTransactionDto.Files, UploadKind.Attachment) is { } fileError)
		{
			validationErrors.Add(fileError);
		}
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

	/// <summary>نسخ قيد إلى خزنة أخرى</summary>
	/// <remarks>
	/// **الصلاحية:** transaction.duplicate على الخزنة الأصلية، و transaction.create على الخزنة الهدف.
	///
	/// يُنسخ القيد بمبلغه وسعر صرفه وحقوله ومرفقاته. الخزنتان في المنشأة نفسها.
	/// </remarks>
	/// <param name="id">معرّف القيد الأصلي.</param>
	/// <param name="targetBookId">الخزنة التي يُنسخ إليها.</param>
	/// <param name="businessId">للتوافق مع الواجهة فقط.</param>
	/// <response code="200">تمت العملية. النتيجة في الحقل result.</response>
	/// <response code="401">لم تسجّل الدخول، أو رمز الدخول منتهٍ أو غير صالح.</response>
	/// <response code="403">لا تملك الصلاحية المطلوبة.</response>
	/// <response code="400">بيانات الطلب ناقصة أو غير صالحة. السبب في errorMessages.</response>
	/// <response code="404">السجل غير موجود، أو مؤرشف.</response>
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
