using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using cashbook.Data;
using cashbook.Dto;
using cashbook.Dto.book;
using cashbook.Dto.business;
using cashbook.Interfaces;
using cashbook.Models;

namespace cashbook.Repositories;

public class BusinessRepository : Repository<Business>, IBusinessRepository, IRepository<Business>
{
	private readonly ApplicationDbContext _context;

	public BusinessRepository(ApplicationDbContext context, IConfiguration configuration)
		: base(context)
	{
		_context = context;
	}

	public async Task<bool> CreateBussinessAsync(CreateBusinessDto createBusinessDto, Guid userId)
	{
		try
		{
			Business business = new Business
			{
				Name = createBusinessDto.Name
			};
			await _context.Businesses.AddAsync(business);
			await _context.SaveChangesAsync();
			BusinessUser businessUser = new BusinessUser
			{
				UserId = userId,
				BusinessId = business.Id,
				Role = "owner"
			};
			await _context.BusinessUsers.AddAsync(businessUser);
			List<PaymentMethod> paymentMethods = new List<PaymentMethod>
			{
				new PaymentMethod
				{
					Name = "Cash",
					BusinessId = business.Id
				},
				new PaymentMethod
				{
					Name = "Online",
					BusinessId = business.Id
				}
			};
			await _context.PaymentMethods.AddRangeAsync(paymentMethods);
			await _context.SaveChangesAsync();
			return true;
		}
		catch (Exception)
		{
			return false;
		}
	}

	public async Task<PaginatedResponse<BusinessDto>> GetPaginatedBusinessesAsync(ClaimsPrincipal userClaims, int? skip = 1, int? take = 25, string search = null)
	{
		int currentPage = skip ?? 1;
		int currentSize = take ?? 25;
		string userIdStr = userClaims.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")?.Value;
		if (!Guid.TryParse(userIdStr, out var userId))
		{
			return new PaginatedResponse<BusinessDto>
			{
				TotalRecords = 0,
				Skip = (skip ?? 1),
				Take = (take ?? 25),
				Data = new List<BusinessDto>()
			};
		}
		IQueryable<BusinessUser> query = (from bu in _context.BusinessUsers.Include((BusinessUser bu) => bu.Business)
			where bu.UserId == userId
			select bu).AsQueryable();
		if (!string.IsNullOrEmpty(search))
		{
			query = query.Where((BusinessUser bu) => bu.Business.Name.Contains(search));
		}
		int totalRecords = await query.CountAsync();
		List<BusinessDto> businesses = await (from bu in (from bu in query
				orderby bu.Business.CreatedAt descending, bu.Business.Id
				select bu).Skip((currentPage - 1) * currentSize).Take(currentSize)
			select new BusinessDto
			{
				Id = bu.Business.Id,
				Name = bu.Business.Name,
				CreatedAt = bu.Business.CreatedAt,
				UpdatedAt = bu.Business.UpdatedAt,
				Role = bu.Role,
				BooksCount = _context.Books.Count((Book b) => b.BusinessId == bu.Business.Id)
			}).ToListAsync();
		return new PaginatedResponse<BusinessDto>
		{
			TotalRecords = totalRecords,
			Skip = currentPage,
			Take = currentSize,
			Data = businesses
		};
	}

	public async Task<BusinessWithBooksDto?> GetBusinessWithBooksDtoByIdAsync(Guid Id)
	{
		Business business = await _context.Businesses.Include((Business b) => b.Books).FirstOrDefaultAsync((Business b) => b.Id == Id);
		if (business == null)
		{
			return null;
		}
		return new BusinessWithBooksDto
		{
			Id = business.Id,
			Name = business.Name,
			CreatedAt = business.CreatedAt,
			UpdatedAt = business.UpdatedAt,
			Books = business.Books.Select((Book book) => new BookDto
			{
				Id = book.Id,
				Name = book.Name,
				CreatedAt = book.CreatedAt,
				UpdatedAt = book.UpdatedAt
			}).ToList()
		};
	}

	public async Task<bool> DeleteBusinessAsync(Guid businessId)
	{
		Business business = await _context.Businesses.Include((Business b) => b.Books).ThenInclude((Book book) => book.Transactions).ThenInclude((Transaction t) => t.Attachements)
			.Include((Business b) => b.BusinessUsers)
			.Include((Business b) => b.Categories)
			.Include((Business b) => b.Contacts)
			.Include((Business b) => b.PaymentMethods)
			.Include((Business b) => b.Books)
			.ThenInclude((Book book) => book.CustomFields)
			.FirstOrDefaultAsync((Business b) => b.Id == businessId);
		if (business == null)
		{
			return false;
		}
		string uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
		List<Attachement> attachments = business.Books.SelectMany(delegate(Book book)
		{
			IEnumerable<Transaction> transactions2 = book.Transactions;
			return transactions2 ?? Enumerable.Empty<Transaction>();
		}).SelectMany(delegate(Transaction t)
		{
			IEnumerable<Attachement> attachements = t.Attachements;
			return attachements ?? Enumerable.Empty<Attachement>();
		}).ToList();
		foreach (Attachement attachment in attachments)
		{
			if (string.IsNullOrEmpty(attachment.Files))
			{
				continue;
			}
			string filePath = Path.Combine(uploadsFolder, attachment.Files);
			if (File.Exists(filePath))
			{
				try
				{
					File.Delete(filePath);
				}
				catch (Exception ex)
				{
					Exception ex2 = ex;
					Console.WriteLine("Failed to delete file " + attachment.Files + ": " + ex2.Message);
				}
			}
		}
		List<Guid> transactionIds = (from t in business.Books.SelectMany(delegate(Book book)
			{
				IEnumerable<Transaction> transactions2 = book.Transactions;
				return transactions2 ?? Enumerable.Empty<Transaction>();
			})
			select t.Id).ToList();
		List<CustomFieldValue> customFieldValuesToDelete = await _context.CustomFieldValues.Where((CustomFieldValue cfVal) => transactionIds.Contains(cfVal.TransactionId)).ToListAsync();
		if (customFieldValuesToDelete.Any())
		{
			_context.CustomFieldValues.RemoveRange(customFieldValuesToDelete);
		}
		if (attachments.Any())
		{
			_context.Attachements.RemoveRange(attachments);
		}
		List<Transaction> transactions = business.Books.SelectMany(delegate(Book book)
		{
			IEnumerable<Transaction> transactions2 = book.Transactions;
			return transactions2 ?? Enumerable.Empty<Transaction>();
		}).ToList();
		if (transactions.Any())
		{
			_context.Transactions.RemoveRange(transactions);
		}
		List<CustomField> customFields = business.Books.SelectMany(delegate(Book book)
		{
			IEnumerable<CustomField> customFields2 = book.CustomFields;
			return customFields2 ?? Enumerable.Empty<CustomField>();
		}).ToList();
		if (customFields.Any())
		{
			_context.CustomFields.RemoveRange(customFields);
		}
		if (business.Books != null && business.Books.Any())
		{
			_context.Books.RemoveRange(business.Books);
		}
		if (business.BusinessUsers != null && business.BusinessUsers.Any())
		{
			_context.BusinessUsers.RemoveRange(business.BusinessUsers);
		}
		if (business.Categories != null && business.Categories.Any())
		{
			_context.Categories.RemoveRange(business.Categories);
		}
		if (business.PaymentMethods != null && business.PaymentMethods.Any())
		{
			_context.PaymentMethods.RemoveRange(business.PaymentMethods);
		}
		if (business.Contacts != null && business.Contacts.Any())
		{
			_context.Contacts.RemoveRange(business.Contacts);
		}
		_context.Businesses.Remove(business);
		await _context.SaveChangesAsync();
		return true;
	}
}
