using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using AutoMapper;
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

/// <summary>
/// الخزائن. إنشاء الخزنة ونقلها بين المنشآت للسوبر أدمن وحده، وما سوى ذلك بصلاحيات الخزنة نفسها.
/// </summary>
[Route("API/[Controller]")]
[Authorize]
public class BookController : ApiControllerBase
{
	private readonly IBookRepository _bookRepository;

	private readonly IPermissionService _permissions;

	private readonly ApplicationDbContext _context;

	private readonly IMapper _mapper;

	public BookController(IBookRepository bookRepository, IPermissionService permissions, ApplicationDbContext context, IMapper mapper)
	{
		_bookRepository = bookRepository;
		_permissions = permissions;
		_context = context;
		_mapper = mapper;
	}

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

	/// <summary>لا حذف في النظام: هذا المسار يؤرشف الخزنة وقيودها دون رجعة. السبب اختياري.</summary>
	[HttpDelete("{id:Guid}", Name = "DeleteBook")]
	public Task<ActionResult<APIResponse>> DeleteBook(Guid id, [FromQuery] string? reason = null) => ArchiveCore(id, reason);

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
		return Success(_mapper.Map<BookDto>(book));
	}

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
