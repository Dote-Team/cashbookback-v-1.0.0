using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using cashbook.Data;
using cashbook.Dto;
using cashbook.Dto.paymentMethod;
using cashbook.Interfaces;
using cashbook.Models;

namespace cashbook.Repositories;

public class PaymentMethodRepository : Repository<PaymentMethod>, IPaymentMethodRepository, IRepository<PaymentMethod>
{
	private readonly ApplicationDbContext _context;

	public PaymentMethodRepository(ApplicationDbContext context, IConfiguration configuration)
		: base(context)
	{
		_context = context;
	}

	public async Task<PaginatedResponse<PaymentMethodDto>> GetPaymentMethodsAsync(Guid businessId, int? skip = 1, int? take = 25, string search = null)
	{
		int currentPage = skip ?? 1;
		int currentPageSize = take ?? 25;
		IQueryable<PaymentMethod> query = _context.PaymentMethods.Where((PaymentMethod b) => b.BusinessId == businessId);
		if (!string.IsNullOrWhiteSpace(search))
		{
			query = query.Where((PaymentMethod b) => b.Name.Contains(search));
		}
		int totalRecords = await query.CountAsync();
		List<PaymentMethodDto> books = await (from c in query.OrderByDescending((PaymentMethod c) => c.CreatedAt).Skip((currentPage - 1) * currentPageSize).Take(currentPageSize)
			select new PaymentMethodDto
			{
				Id = c.Id,
				Name = c.Name,
				BusinessId = c.BusinessId,
				CreatedAt = c.CreatedAt,
				UpdatedAt = c.UpdatedAt
			}).ToListAsync();
		return new PaginatedResponse<PaymentMethodDto>
		{
			TotalRecords = totalRecords,
			Skip = currentPage,
			Take = currentPageSize,
			Data = books
		};
	}
}
