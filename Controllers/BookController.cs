using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using cashbook.Data;
using cashbook.Dto;
using cashbook.Dto.Book;
using cashbook.Dto.book;
using cashbook.Dto.setting;
using cashbook.Helper;
using cashbook.Interfaces;
using cashbook.Models;
using cashbook.Services;

namespace cashbook.Controllers;

/// <summary>الخزائن داخل المنشأة، مع أرصدتها بالدينار والدولار. كل خزنة لها صلاحيات مستقلة لكل مستخدم.</summary>
[Route("API/[Controller]")]
[Authorize]
public class BookController : ApiControllerBase
{
	private readonly IBookRepository _bookRepository;

	private readonly IPermissionService _permissions;

	private readonly ApplicationDbContext _context;

	public BookController(IBookRepository bookRepository, IPermissionService permissions, ApplicationDbContext context)
	{
		_bookRepository = bookRepository;
		_permissions = permissions;
		_context = context;
	}

	/// <summary>خزائن منشأة مع أرصدتها</summary>
	/// <remarks>
	/// **الصلاحية:** عضو في المنشأة. تُعاد الخزائن التي تملك book.view عليها فقط.
	///
	/// الرصيد = الإيداعات − السحوبات، بالدينار والدولار كلٌّ على حدة. القيود المؤرشفة لا تدخل في الرصيد.
	/// </remarks>
	/// <param name="businessId">معرّف المنشأة.</param>
	/// <param name="skip">رقم الصفحة، يبدأ من 1.</param>
	/// <param name="take">عدد السجلات في الصفحة.</param>
	/// <param name="search">بحث في اسم الخزنة.</param>
	/// <param name="sortBy">الترتيب: Name أو CreatedAt أو Balance (الدينار ثم الدولار).</param>
	/// <param name="sortDirection">asc أو desc.</param>
	/// <response code="200">تمت العملية. النتيجة في الحقل result.</response>
	/// <response code="401">لم تسجّل الدخول، أو رمز الدخول منتهٍ أو غير صالح.</response>
	/// <response code="403">لا تملك الصلاحية المطلوبة.</response>
	[HttpGet]
	public async Task<ActionResult<APIResponse>> GetPaginatedBooks([FromQuery] Guid businessId, [FromQuery] int? skip = 1, [FromQuery] int? take = 25, [FromQuery] string? search = null, [FromQuery] SortField? sortBy = null, [FromQuery] SortDirection? sortDirection = SortDirection.desc)
	{
		if (CallerId is not Guid callerId)
		{
			return NoCaller();
		}
		if (!await _permissions.IsBusinessMemberAsync(callerId, businessId))
		{
			return Fail(HttpStatusCode.Forbidden, "لا تملك صلاحية على هذه المنشأة.");
		}
		List<Guid>? allowed = await _permissions.IsSuperAdminAsync(callerId)
			? null
			: await _permissions.GetBookIdsAsync(callerId, businessId, Permissions.BookView);
		return Success(await _bookRepository.GetBooksAsync(businessId, allowed, skip, take, search, sortBy, sortDirection));
	}

	/// <summary>تفاصيل خزنة</summary>
	/// <remarks>
	/// **الصلاحية:** book.view على الخزنة. يشمل الأرصدة وإعدادات الخزنة.
	/// </remarks>
	/// <param name="id">معرّف الخزنة.</param>
	/// <response code="200">تمت العملية. النتيجة في الحقل result.</response>
	/// <response code="401">لم تسجّل الدخول، أو رمز الدخول منتهٍ أو غير صالح.</response>
	/// <response code="403">لا تملك الصلاحية المطلوبة.</response>
	/// <response code="404">السجل غير موجود، أو مؤرشف.</response>
	[HttpGet("{id:Guid}", Name = "GetBook")]
	public async Task<ActionResult<APIResponse>> GetBook(Guid id)
	{
		if (CallerId is not Guid callerId)
		{
			return NoCaller();
		}
		BookAccess access = await _permissions.CheckBookAsync(callerId, id, Permissions.BookView);
		if (access.Status != AccessStatus.Granted)
		{
			// من لا يملك الخزنة لا يعرف حتى أنها موجودة.
			return Fail(HttpStatusCode.NotFound, "الخزنة غير موجودة.");
		}
		BookDetailsDto? book = await _bookRepository.GetBookDetailsByIdAsync(id);
		return book == null ? Fail(HttpStatusCode.NotFound, "الخزنة غير موجودة.") : Success(book);
	}

	/// <summary>إنشاء خزنة</summary>
	/// <remarks>
	/// **الصلاحية:** السوبر أدمن فقط.
	///
	/// بعد الإنشاء امنح الأدمن صلاحياته عليها من /API/UserManagement/{id}/permissions.
	/// </remarks>
	/// <param name="createBookDto">اسم الخزنة والمنشأة التي تتبعها.</param>
	/// <response code="200">تمت العملية. النتيجة في الحقل result.</response>
	/// <response code="401">لم تسجّل الدخول، أو رمز الدخول منتهٍ أو غير صالح.</response>
	/// <response code="403">لا تملك الصلاحية المطلوبة.</response>
	/// <response code="400">بيانات الطلب ناقصة أو غير صالحة. السبب في errorMessages.</response>
	/// <response code="404">المنشأة غير موجودة.</response>
	[HttpPost]
	public async Task<ActionResult<APIResponse>> CreateBook([FromBody] CreateBookDto createBookDto)
	{
		if (CallerId is not Guid callerId)
		{
			return NoCaller();
		}
		if (createBookDto == null || string.IsNullOrWhiteSpace(createBookDto.Name))
		{
			return Fail(HttpStatusCode.BadRequest, "اسم الخزنة مطلوب.");
		}
		if (!await _permissions.IsSuperAdminAsync(callerId))
		{
			return Fail(HttpStatusCode.Forbidden, "إنشاء الخزائن للسوبر أدمن فقط.");
		}
		if (!await _context.Businesses.AnyAsync(b => b.Id == createBookDto.BusinessId))
		{
			return Fail(HttpStatusCode.NotFound, "المنشأة غير موجودة.");
		}
		if (!await _bookRepository.CreateBookAsync(createBookDto))
		{
			return Fail(HttpStatusCode.InternalServerError, "تعذّر إنشاء الخزنة.");
		}
		return Success(null, HttpStatusCode.Created);
	}

	/// <summary>أرشفة خزنة (لا حذف)</summary>
	/// <remarks>
	/// **الصلاحية:** book.archive على الخزنة.
	///
	/// تختفي الخزنة وقيودها من القوائم والأرصدة دون رجعة، وتبقى في الأرشيف. مثل POST /{id}/archive.
	/// </remarks>
	/// <param name="id">معرّف الخزنة.</param>
	/// <param name="reason">سبب الأرشفة (اختياري).</param>
	/// <response code="200">تمت العملية. النتيجة في الحقل result.</response>
	/// <response code="401">لم تسجّل الدخول، أو رمز الدخول منتهٍ أو غير صالح.</response>
	/// <response code="403">لا تملك الصلاحية المطلوبة.</response>
	/// <response code="404">السجل غير موجود، أو مؤرشف.</response>
	[HttpDelete("{id:Guid}", Name = "DeleteBook")]
	public Task<ActionResult<APIResponse>> DeleteBook(Guid id, [FromQuery] string? reason = null) => ArchiveCore(id, reason);

	/// <summary>أرشفة خزنة</summary>
	/// <remarks>
	/// **الصلاحية:** book.archive على الخزنة. دون رجعة.
	/// </remarks>
	/// <param name="id">معرّف الخزنة.</param>
	/// <param name="reason">سبب الأرشفة (اختياري).</param>
	/// <response code="200">تمت العملية. النتيجة في الحقل result.</response>
	/// <response code="401">لم تسجّل الدخول، أو رمز الدخول منتهٍ أو غير صالح.</response>
	/// <response code="403">لا تملك الصلاحية المطلوبة.</response>
	/// <response code="404">السجل غير موجود، أو مؤرشف.</response>
	[HttpPost("{id:Guid}/archive")]
	public Task<ActionResult<APIResponse>> ArchiveBook(Guid id, [FromQuery] string? reason = null) => ArchiveCore(id, reason);

	private async Task<ActionResult<APIResponse>> ArchiveCore(Guid id, string? reason)
	{
		if (CallerId is not Guid callerId)
		{
			return NoCaller();
		}
		BookAccess access = await _permissions.CheckBookAsync(callerId, id, Permissions.BookArchive);
		if (access.Status != AccessStatus.Granted)
		{
			return Denied(access.Status, "لا تملك صلاحية أرشفة هذه الخزنة.");
		}
		await _bookRepository.ArchiveBookAsync(id, callerId, reason);
		return Success(new { archived = true });
	}

	/// <summary>تعديل خزنة</summary>
	/// <remarks>
	/// **الصلاحية:** book.edit على الخزنة. نقلها إلى منشأة أخرى (businessId) للسوبر أدمن فقط.
	///
	/// أرسل الحقل الذي تريد تغييره فقط.
	/// </remarks>
	/// <param name="id">معرّف الخزنة.</param>
	/// <param name="updateBookDto">الاسم الجديد، والمنشأة اختيارياً.</param>
	/// <response code="200">تمت العملية. النتيجة في الحقل result.</response>
	/// <response code="401">لم تسجّل الدخول، أو رمز الدخول منتهٍ أو غير صالح.</response>
	/// <response code="403">لا تملك الصلاحية المطلوبة.</response>
	/// <response code="400">بيانات الطلب ناقصة أو غير صالحة. السبب في errorMessages.</response>
	/// <response code="404">السجل غير موجود، أو مؤرشف.</response>
	[HttpPut("{id:Guid}", Name = "UpdateBook")]
	public async Task<ActionResult<APIResponse>> UpdateBook([FromRoute] Guid id, [FromBody] UpdateBookDto updateBookDto)
	{
		if (CallerId is not Guid callerId)
		{
			return NoCaller();
		}
		if (updateBookDto == null)
		{
			return Fail(HttpStatusCode.BadRequest, "بيانات غير صالحة.");
		}
		BookAccess access = await _permissions.CheckBookAsync(callerId, id, Permissions.BookEdit);
		if (access.Status != AccessStatus.Granted)
		{
			return Denied(access.Status, "لا تملك صلاحية تعديل هذه الخزنة.");
		}
		Book book = await _context.Books.FirstAsync(b => b.Id == id);
		if (updateBookDto.BusinessId != Guid.Empty && updateBookDto.BusinessId != book.BusinessId)
		{
			// نقل الخزنة إلى منشأة أخرى يغيّر من يراها، فهو للسوبر أدمن وحده.
			if (!await _permissions.IsSuperAdminAsync(callerId))
			{
				return Fail(HttpStatusCode.Forbidden, "نقل الخزنة إلى منشأة أخرى للسوبر أدمن فقط.");
			}
			if (!await _context.Businesses.AnyAsync(b => b.Id == updateBookDto.BusinessId))
			{
				return Fail(HttpStatusCode.NotFound, "المنشأة المطلوبة غير موجودة.");
			}
			book.BusinessId = updateBookDto.BusinessId;
		}
		if (!string.IsNullOrWhiteSpace(updateBookDto.Name))
		{
			book.Name = updateBookDto.Name.Trim();
		}
		book.UpdatedAt = DateTime.UtcNow;
		await _context.SaveChangesAsync();
		return Success(new BookDto
		{
			Id = book.Id,
			Name = book.Name,
			BusinessId = book.BusinessId,
			CreatedAt = book.CreatedAt,
			UpdatedAt = book.UpdatedAt
		});
	}

	/// <summary>إعدادات الخزنة</summary>
	/// <remarks>
	/// **الصلاحية:** book.edit على الخزنة.
	///
	/// تحدد الحقول الإلزامية عند إضافة قيد: categoryStatus (التصنيف)، paymentMethodStatus (طريقة الدفع)، contactStatus (جهة الاتصال).
	/// أرسل الحقل الذي تريد تغييره فقط.
	/// </remarks>
	/// <param name="bookId">معرّف الخزنة.</param>
	/// <param name="dto">الإعدادات المراد تغييرها.</param>
	/// <response code="200">تمت العملية. النتيجة في الحقل result.</response>
	/// <response code="401">لم تسجّل الدخول، أو رمز الدخول منتهٍ أو غير صالح.</response>
	/// <response code="403">لا تملك الصلاحية المطلوبة.</response>
	/// <response code="404">السجل غير موجود، أو مؤرشف.</response>
	[HttpPatch("{bookId:Guid}/setting")]
	public async Task<ActionResult<APIResponse>> PatchBookSetting(Guid bookId, [FromBody] UpdateSettingDto dto)
	{
		if (CallerId is not Guid callerId)
		{
			return NoCaller();
		}
		BookAccess access = await _permissions.CheckBookAsync(callerId, bookId, Permissions.BookEdit);
		if (access.Status != AccessStatus.Granted)
		{
			return Denied(access.Status, "لا تملك صلاحية تعديل إعدادات هذه الخزنة.");
		}
		if (!await _bookRepository.UpdateSettingAsync(bookId, dto))
		{
			return Fail(HttpStatusCode.NotFound, "إعدادات الخزنة غير موجودة.");
		}
		return Success("Setting updated successfully.");
	}
}
