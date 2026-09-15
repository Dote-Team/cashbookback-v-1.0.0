using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using cashbook.Data;
using cashbook.Dto;
using cashbook.Dto.contact;
using cashbook.Interfaces;
using cashbook.Models;

namespace cashbook.Repositories;

public class ContactRepository : Repository<Contact>, IContactRepository, IRepository<Contact>
{
	private readonly ApplicationDbContext _context;

	public ContactRepository(ApplicationDbContext context, IConfiguration configuration)
		: base(context)
	{
		_context = context;
	}

	public async Task<PaginatedResponse<ContactDto>> GetContactsAsync(Guid businessId, int? skip = 1, int? take = 25, string search = null)
	{
		int currentPage = skip ?? 1;
		int currentPageSize = take ?? 25;
		IQueryable<Contact> query = _context.Contacts.Where((Contact b) => b.BusinessId == businessId);
		if (!string.IsNullOrWhiteSpace(search))
		{
			query = query.Where((Contact b) => b.Name.Contains(search));
		}
		int totalRecords = await query.CountAsync();
		List<ContactDto> books = await (from c in query.OrderByDescending((Contact c) => c.CreatedAt).Skip((currentPage - 1) * currentPageSize).Take(currentPageSize)
			select new ContactDto
			{
				Id = c.Id,
				Name = c.Name,
				Phone = c.Phone,
				BusinessId = c.BusinessId,
				CreatedAt = c.CreatedAt,
				UpdatedAt = c.UpdatedAt
			}).ToListAsync();
		return new PaginatedResponse<ContactDto>
		{
			TotalRecords = totalRecords,
			Skip = currentPage,
			Take = currentPageSize,
			Data = books
		};
	}
}
