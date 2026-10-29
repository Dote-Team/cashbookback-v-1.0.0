using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using cashbook.Data;
using cashbook.Dto;
using cashbook.Dto.book;
using cashbook.Dto.business;
using cashbook.Helper;
using cashbook.Interfaces;
using cashbook.Models;

namespace cashbook.Repositories;

public class BusinessRepository : Repository<Business>, IBusinessRepository, IRepository<Business>
{
	private readonly ApplicationDbContext _context;

	public BusinessRepository(ApplicationDbContext context)
		: base(context)
	{
		_context = context;
	}

	/// <summary>المنشأة ينشئها السوبر أدمن، ولا تحتاج صف عضوية له: صلاحياته على كل شيء.</summary>
	public async Task<Guid> CreateBussinessAsync(CreateBusinessDto createBusinessDto)
	{
		Business business = new Business
		{
			Name = createBusinessDto.Name.Trim()
		};
		_context.Businesses.Add(business);
		_context.PaymentMethods.AddRange(
			new PaymentMethod { Name = "Cash", BusinessId = business.Id },
			new PaymentMethod { Name = "Online", BusinessId = business.Id });
		await _context.SaveChangesAsync();
		return business.Id;
	}

	public async Task<PaginatedResponse<BusinessDto>> GetPaginatedBusinessesAsync(List<Guid> businessIds, List<Guid>? accessibleBookIds, string role, int? skip = 1, int? take = 25, string? search = null)
	{
		int currentPage = Math.Max(1, skip ?? 1);
		int currentSize = Math.Clamp(take ?? 25, 1, 200);
		IQueryable<Business> query = _context.Businesses.AsNoTracking().Where(b => businessIds.Contains(b.Id));
		if (!string.IsNullOrEmpty(search))
		{
			query = query.Where(b => b.Name.Contains(search));
		}
		int totalRecords = await query.CountAsync();
		List<BusinessDto> businesses = await query
			.OrderByDescending(b => b.CreatedAt).ThenBy(b => b.Id)
			.Skip((currentPage - 1) * currentSize).Take(currentSize)
			.Select(b => new BusinessDto
			{
				Id = b.Id,
				Name = b.Name,
				CreatedAt = b.CreatedAt,
				UpdatedAt = b.UpdatedAt,
				Role = role,
				BooksCount = _context.Books.Count(book => book.BusinessId == b.Id && (accessibleBookIds == null || accessibleBookIds.Contains(book.Id)))
			}).ToListAsync();
		return new PaginatedResponse<BusinessDto>
		{
			TotalRecords = totalRecords,
			Skip = currentPage,
			Take = currentSize,
			Data = businesses
		};
	}

	public async Task<BusinessWithBooksDto?> GetBusinessWithBooksDtoByIdAsync(Guid id, List<Guid>? accessibleBookIds)
	{
		Business? business = await _context.Businesses.AsNoTracking().FirstOrDefaultAsync(b => b.Id == id);
		if (business == null)
		{
			return null;
		}
		List<BookDto> books = await _context.Books.AsNoTracking()
			.Where(book => book.BusinessId == id && (accessibleBookIds == null || accessibleBookIds.Contains(book.Id)))
			.Select(book => new BookDto
			{
				Id = book.Id,
				Name = book.Name,
				BusinessId = book.BusinessId,
				CreatedAt = book.CreatedAt,
				UpdatedAt = book.UpdatedAt
			}).ToListAsync();
		return new BusinessWithBooksDto
		{
			Id = business.Id,
			Name = business.Name,
			CreatedAt = business.CreatedAt,
			UpdatedAt = business.UpdatedAt,
			Books = books
		};
	}

	/// <summary>أرشفة المنشأة دون رجعة، ومعها خزائنها كلها في العملية نفسها.</summary>
	public async Task<bool> ArchiveBusinessAsync(Guid businessId, Guid archivedByUserId, string? reason = null)
	{
		Business? business = await _context.Businesses.FirstOrDefaultAsync(b => b.Id == businessId);
		if (business == null)
		{
			return false;
		}
		DateTime now = DateTime.UtcNow;
		string? cleanReason = ArchiveText.Reason(reason);
		foreach (IArchivable item in (await _context.Books.Where(b => b.BusinessId == businessId).ToListAsync()).Cast<IArchivable>().Append(business))
		{
			item.IsArchived = true;
			item.ArchivedAt = now;
			item.ArchivedByUserId = archivedByUserId;
			item.ArchiveReason = cleanReason ?? (item is Book ? "أُرشفت مع منشأتها" : null);
		}
		await _context.SaveChangesAsync();
		return true;
	}
}
