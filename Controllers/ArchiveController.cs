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
using cashbook.Helper;
using cashbook.Services;

namespace cashbook.Controllers;

public class ArchivedItemDto
{
	public Guid Id { get; set; }

	public string Type { get; set; }

	public string Name { get; set; }

	public Guid? BookId { get; set; }

	public string? BookName { get; set; }

	public Guid? BusinessId { get; set; }

	public decimal? Amount { get; set; }

	public string? Currency { get; set; }

	public string? TransactionType { get; set; }

	public DateTime? Date { get; set; }

	public DateTime? ArchivedAt { get; set; }

	public Guid? ArchivedByUserId { get; set; }

	public string? ArchivedByName { get; set; }

	public string? ArchiveReason { get; set; }
}

/// <summary>
/// الأرشيف: عرض فقط، بلا استرجاع. يرى كل مستخدم أرشيف الخزائن التي يملك عليها
/// صلاحية «عرض الأرشيف»، حتى بعد أرشفة الخزنة نفسها.
/// </summary>
[Route("API/[Controller]")]
[Authorize]
public class ArchiveController : ApiControllerBase
{
	private static readonly string[] Types = { "transaction", "book", "category", "contact", "payment_method", "custom_field", "business" };

	private readonly ApplicationDbContext _context;

	private readonly IPermissionService _permissions;

	public ArchiveController(ApplicationDbContext context, IPermissionService permissions)
	{
		_context = context;
		_permissions = permissions;
	}

	[HttpGet]
	public async Task<ActionResult<APIResponse>> GetArchive([FromQuery] Guid businessId, [FromQuery] string type = "transaction", [FromQuery] Guid? bookId = null, [FromQuery] int? skip = 1, [FromQuery] int? take = 25, [FromQuery] string? search = null)
	{
		if (CallerId is not Guid callerId)
		{
			return NoCaller();
		}
		type = (type ?? "transaction").Trim().ToLowerInvariant();
		if (!Types.Contains(type))
		{
			return Fail(HttpStatusCode.BadRequest, "نوع الأرشيف غير معروف.");
		}
		bool isSuperAdmin = await _permissions.IsSuperAdminAsync(callerId);
		UserAccessInfo? caller = await _permissions.GetUserAsync(callerId);
		if (caller == null || !caller.IsActive)
		{
			return NoCaller();
		}
		// الخزائن ومنشأتها تُقرأ متجاوزةً فلتر الأرشفة: صلاحية عرض الأرشيف تبقى بعد أرشفة الخزنة.
		List<Guid> allowedBooks = isSuperAdmin
			? await _context.Books.IgnoreQueryFilters().Where(b => b.BusinessId == businessId).Select(b => b.Id).ToListAsync()
			: await (from p in _context.UserWalletPermissions
				join b in _context.Books.IgnoreQueryFilters() on p.BookId equals b.Id
				where p.UserId == callerId && p.Permission == Permissions.ArchiveView && b.BusinessId == businessId
				select b.Id).Distinct().ToListAsync();
		if (bookId.HasValue)
		{
			allowedBooks = allowedBooks.Where(id => id == bookId.Value).ToList();
		}
		if (type == "business" ? !isSuperAdmin : allowedBooks.Count == 0)
		{
			return Fail(HttpStatusCode.Forbidden, "لا تملك صلاحية عرض الأرشيف.");
		}
		int page = Page(skip);
		int size = PageSize(take);
		string? term = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
		IQueryable<ArchivedItemDto> query = type switch
		{
			"transaction" => from t in _context.Transactions.IgnoreQueryFilters()
				join b in _context.Books.IgnoreQueryFilters() on t.BookId equals b.Id
				where t.IsArchived && allowedBooks.Contains(t.BookId) && (term == null || (t.Description != null && t.Description.Contains(term)))
				select new ArchivedItemDto
				{
					Id = t.Id, Type = type, Name = t.Description ?? "", BookId = t.BookId, BookName = b.Name, BusinessId = b.BusinessId,
					Amount = t.Amount, Currency = t.Currency.ToString(), TransactionType = t.Type, Date = t.Date,
					ArchivedAt = t.ArchivedAt, ArchivedByUserId = t.ArchivedByUserId, ArchiveReason = t.ArchiveReason
				},
			"book" => from b in _context.Books.IgnoreQueryFilters()
				where b.IsArchived && allowedBooks.Contains(b.Id) && (term == null || b.Name.Contains(term))
				select new ArchivedItemDto
				{
					Id = b.Id, Type = type, Name = b.Name, BookId = b.Id, BookName = b.Name, BusinessId = b.BusinessId,
					ArchivedAt = b.ArchivedAt, ArchivedByUserId = b.ArchivedByUserId, ArchiveReason = b.ArchiveReason
				},
			"custom_field" => from f in _context.CustomFields
				join b in _context.Books.IgnoreQueryFilters() on f.BookId equals b.Id
				where f.IsArchived && allowedBooks.Contains(f.BookId) && (term == null || f.Key.Contains(term))
				select new ArchivedItemDto
				{
					Id = f.Id, Type = type, Name = f.Key, BookId = f.BookId, BookName = b.Name, BusinessId = b.BusinessId,
					ArchivedAt = f.ArchivedAt, ArchivedByUserId = f.ArchivedByUserId, ArchiveReason = f.ArchiveReason
				},
			"category" => from c in _context.Categories
				where c.IsArchived && c.BusinessId == businessId && (term == null || c.Name.Contains(term))
				select new ArchivedItemDto
				{
					Id = c.Id, Type = type, Name = c.Name, BusinessId = c.BusinessId,
					ArchivedAt = c.ArchivedAt, ArchivedByUserId = c.ArchivedByUserId, ArchiveReason = c.ArchiveReason
				},
			"contact" => from c in _context.Contacts
				where c.IsArchived && c.BusinessId == businessId && (term == null || c.Name.Contains(term))
				select new ArchivedItemDto
				{
					Id = c.Id, Type = type, Name = c.Name, BusinessId = c.BusinessId,
					ArchivedAt = c.ArchivedAt, ArchivedByUserId = c.ArchivedByUserId, ArchiveReason = c.ArchiveReason
				},
			"payment_method" => from p in _context.PaymentMethods
				where p.IsArchived && p.BusinessId == businessId && (term == null || p.Name.Contains(term))
				select new ArchivedItemDto
				{
					Id = p.Id, Type = type, Name = p.Name, BusinessId = p.BusinessId,
					ArchivedAt = p.ArchivedAt, ArchivedByUserId = p.ArchivedByUserId, ArchiveReason = p.ArchiveReason
				},
			_ => from b in _context.Businesses.IgnoreQueryFilters()
				where b.IsArchived && (term == null || b.Name.Contains(term))
				select new ArchivedItemDto
				{
					Id = b.Id, Type = type, Name = b.Name, BusinessId = b.Id,
					ArchivedAt = b.ArchivedAt, ArchivedByUserId = b.ArchivedByUserId, ArchiveReason = b.ArchiveReason
				},
		};
		int total = await query.CountAsync();
		List<ArchivedItemDto> data = await query.OrderByDescending(x => x.ArchivedAt).ThenBy(x => x.Id)
			.Skip((page - 1) * size).Take(size).ToListAsync();
		List<Guid> userIds = data.Where(x => x.ArchivedByUserId.HasValue).Select(x => x.ArchivedByUserId!.Value).Distinct().ToList();
		Dictionary<Guid, string> names = await _context.Users.Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Name);
		foreach (ArchivedItemDto item in data)
		{
			if (item.ArchivedByUserId.HasValue && names.TryGetValue(item.ArchivedByUserId.Value, out string? name))
			{
				item.ArchivedByName = name;
			}
		}
		return Success(new PaginatedResponse<ArchivedItemDto>
		{
			TotalRecords = total,
			Skip = page,
			Take = size,
			Data = data
		});
	}
}
