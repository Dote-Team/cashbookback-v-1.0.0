using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using cashbook.Data;
using cashbook.Dto;
using cashbook.Dto.customfield;
using cashbook.Interfaces;
using cashbook.Models;

namespace cashbook.Repositories;

public class CustomFieldRepository : Repository<CustomField>, ICustomFieldRepository, IRepository<CustomField>
{
	private readonly ApplicationDbContext _context;

	public CustomFieldRepository(ApplicationDbContext context, IConfiguration configuration)
		: base(context)
	{
		_context = context;
	}

	public async Task<PaginatedResponse<CustomFieldDto>> GetcustomFieldAsync(Guid bookId, int? skip = 1, int? take = 25, string search = null)
	{
		int currentPage = skip ?? 1;
		int currentPageSize = take ?? 25;
		IQueryable<CustomField> query = _context.CustomFields.Where((CustomField b) => b.BookId == bookId);
		if (!string.IsNullOrWhiteSpace(search))
		{
			query = query.Where((CustomField b) => b.Key.Contains(search));
		}
		int totalRecords = await query.CountAsync();
		List<CustomFieldDto> books = await (from c in (from c in query
				orderby c.Key, c.Id
				select c).Skip((currentPage - 1) * currentPageSize).Take(currentPageSize)
			select new CustomFieldDto
			{
				Id = c.Id,
				Key = c.Key,
				IsRequired = c.IsRequired,
				BookId = c.BookId
			}).ToListAsync();
		return new PaginatedResponse<CustomFieldDto>
		{
			TotalRecords = totalRecords,
			Skip = currentPage,
			Take = currentPageSize,
			Data = books
		};
	}
}
