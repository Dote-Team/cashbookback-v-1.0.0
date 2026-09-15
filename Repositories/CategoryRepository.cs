using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using cashbook.Data;
using cashbook.Dto;
using cashbook.Dto.category;
using cashbook.Interfaces;
using cashbook.Models;

namespace cashbook.Repositories;

public class CategoryRepository : Repository<Category>, ICategoryRepository, IRepository<Category>
{
	private readonly ApplicationDbContext _context;

	public CategoryRepository(ApplicationDbContext context, IConfiguration configuration)
		: base(context)
	{
		_context = context;
	}

	public async Task<PaginatedResponse<CategoryDto>> GetCategoriesAsync(Guid businessId, int? skip = 1, int? take = 25, string search = null)
	{
		int currentPage = skip ?? 1;
		int currentPageSize = take ?? 25;
		IQueryable<Category> query = _context.Categories.Where((Category b) => b.BusinessId == businessId);
		if (!string.IsNullOrWhiteSpace(search))
		{
			query = query.Where((Category b) => b.Name.Contains(search));
		}
		int totalRecords = await query.CountAsync();
		List<CategoryDto> books = await (from c in query.OrderByDescending((Category c) => c.CreatedAt).Skip((currentPage - 1) * currentPageSize).Take(currentPageSize)
			select new CategoryDto
			{
				Id = c.Id,
				Name = c.Name,
				BusinessId = c.BusinessId,
				CreatedAt = c.CreatedAt,
				UpdatedAt = c.UpdatedAt
			}).ToListAsync();
		return new PaginatedResponse<CategoryDto>
		{
			TotalRecords = totalRecords,
			Skip = currentPage,
			Take = currentPageSize,
			Data = books
		};
	}
}
