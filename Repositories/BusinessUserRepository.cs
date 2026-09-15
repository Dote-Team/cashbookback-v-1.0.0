using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using cashbook.Data;
using cashbook.Dto;
using cashbook.Dto.book;
using cashbook.Dto.business;
using cashbook.Dto.businessUser;
using cashbook.Dto.user;
using cashbook.Interfaces;
using cashbook.Models;

namespace cashbook.Repositories;

public class BusinessUserRepository : Repository<BusinessUser>, IBusinessUserRepository, IRepository<BusinessUser>
{
	private readonly ApplicationDbContext _context;

	public BusinessUserRepository(ApplicationDbContext context, IConfiguration configuration)
		: base(context)
	{
		_context = context;
	}

	public async Task<PaginatedResponse<BusinessUserDto>> GetBusinessUsersAsync(Guid businessId, int? skip = 1, int? take = 25, string search = null)
	{
		int currentPage = skip ?? 1;
		int currentPageSize = take ?? 25;
		IQueryable<BusinessUser> query = _context.BusinessUsers.Where((BusinessUser businessUser) => businessUser.BusinessId == businessId).Include((BusinessUser businessUser) => businessUser.User).AsQueryable();
		if (!string.IsNullOrWhiteSpace(search))
		{
			query = query.Where((BusinessUser businessUser) => businessUser.User.Name.Contains(search));
		}
		int totalRecords = await query.CountAsync();
		List<BusinessUser> businessUsers = await (from businessUser in query
			orderby businessUser.User.Name, businessUser.Id
			select businessUser).Skip((currentPage - 1) * currentPageSize).Take(currentPageSize).ToListAsync();
		List<BusinessUserDto> result = new List<BusinessUserDto>();
		foreach (BusinessUser bu in businessUsers)
		{
			BusinessUserDto dto = new BusinessUserDto
			{
				Id = bu.Id,
				UserId = bu.UserId,
				BusinessId = bu.BusinessId,
				Role = bu.Role,
				User = new UserDto
				{
					Id = bu.User.Id,
					Name = bu.User.Name,
					Email = bu.User.Email
				},
				Books = new List<BookDto>()
			};
			if ((bu.Role == "admin" || bu.Role == "dataoperator" || bu.Role == "staff" || bu.Role == "privateviewer" || bu.Role == "portfolio_manager") && bu.BookIds != null && bu.BookIds.Any())
			{
				dto.Books = await (from b in _context.Books
					where bu.BookIds.Contains(b.Id)
					select new BookDto
					{
						Id = b.Id,
						Name = b.Name
					}).ToListAsync();
			}
			result.Add(dto);
		}
		return new PaginatedResponse<BusinessUserDto>
		{
			TotalRecords = totalRecords,
			Skip = currentPage,
			Take = currentPageSize,
			Data = result
		};
	}

	public async Task<BusinessUserDto> GetBusinessUserByIdAsync(Guid businessUserId)
	{
		BusinessUser bu = await _context.BusinessUsers.Include((BusinessUser businessUser) => businessUser.User).Include((BusinessUser businessUser) => businessUser.Business).FirstOrDefaultAsync((BusinessUser businessUser) => businessUser.Id == businessUserId);
		if (bu == null)
		{
			return null;
		}
		BusinessUserDto dto = new BusinessUserDto
		{
			Id = bu.Id,
			UserId = bu.UserId,
			BusinessId = bu.BusinessId,
			Role = bu.Role,
			User = ((bu.User == null) ? null : new UserDto
			{
				Id = bu.User.Id,
				Name = bu.User.Name,
				Email = bu.User.Email
			}),
			Business = ((bu.Business == null) ? null : new BusinessDto
			{
				Id = bu.Business.Id,
				Name = bu.Business.Name
			}),
			Books = new List<BookDto>()
		};
		if (bu.BookIds != null && bu.BookIds.Any())
		{
			dto.Books = await (from b in _context.Books
				where bu.BookIds.Contains(b.Id)
				select new BookDto
				{
					Id = b.Id,
					Name = b.Name
				}).ToListAsync();
		}
		return dto;
	}

	public async Task<APIResponse> UpdateBusinessUserAsync(Guid businessUserId, UpdateBusinessUserDto dto)
	{
		BusinessUser businessUser = await _context.BusinessUsers.FirstOrDefaultAsync((BusinessUser bu) => bu.Id == businessUserId);
		if (businessUser == null)
		{
			return new APIResponse
			{
				StatusCode = HttpStatusCode.NotFound,
				IsSuccess = false,
				ErrorMessages = new List<string> { "BusinessUser not found." }
			};
		}
		if (businessUser.Role.ToLower() == "owner" && !string.IsNullOrEmpty(dto.Role) && dto.Role.ToLower() != "owner")
		{
			return new APIResponse
			{
				StatusCode = HttpStatusCode.Forbidden,
				IsSuccess = false,
				ErrorMessages = new List<string> { "You are not allowed to change the role of the owner." }
			};
		}
		string[] roleWithBooks = new string[5] { "staff", "dataoperator", "admin", "privateviewer", "portfolio_manager" };
		if (!string.IsNullOrEmpty(dto.Role) && Enumerable.Contains(roleWithBooks, dto.Role.ToLower()) && dto.BookIds != null && dto.BookIds.Any())
		{
			List<BusinessUser> conflictingBusinessUsers = await _context.BusinessUsers.Where((BusinessUser bu) => bu.UserId == businessUser.UserId && bu.BusinessId == (dto.BusinessId ?? businessUser.BusinessId) && bu.Id != businessUserId && Enumerable.Contains(roleWithBooks, bu.Role.ToLower()) && bu.BookIds.Any((Guid item) => dto.BookIds.Contains(item))).ToListAsync();
			if (conflictingBusinessUsers.Any())
			{
				foreach (BusinessUser conflictUser in conflictingBusinessUsers)
				{
					List<Guid> intersectingBooks = conflictUser.BookIds.Intersect(dto.BookIds).ToList();
					if (!intersectingBooks.Any())
					{
						continue;
					}
					conflictUser.BookIds = conflictUser.BookIds.Except(intersectingBooks).ToList();
					_context.BusinessUsers.Update(conflictUser);
					if (businessUser.BookIds == null)
					{
						businessUser.BookIds = new List<Guid>();
					}
					foreach (Guid bookId in intersectingBooks)
					{
						if (!businessUser.BookIds.Contains(bookId))
						{
							businessUser.BookIds.Add(bookId);
						}
					}
				}
			}
		}
		if (dto.BusinessId.HasValue)
		{
			businessUser.BusinessId = dto.BusinessId.Value;
		}
		if (!string.IsNullOrEmpty(dto.Role))
		{
			businessUser.Role = dto.Role;
		}
		if (dto.BookIds != null)
		{
			if (businessUser.BookIds == null)
			{
				businessUser.BookIds = new List<Guid>();
			}
			foreach (Guid bookId2 in dto.BookIds)
			{
				if (!businessUser.BookIds.Contains(bookId2))
				{
					businessUser.BookIds.Add(bookId2);
				}
			}
		}
		_context.BusinessUsers.Update(businessUser);
		await _context.SaveChangesAsync();
		return new APIResponse
		{
			StatusCode = HttpStatusCode.OK,
			IsSuccess = true,
			Result = new { businessUser.Id }
		};
	}

	public async Task<bool> DeleteBusinessUsersRange(Guid businessId, Guid userId)
	{
		List<BusinessUser> businessUsers = await _context.BusinessUsers.Where((BusinessUser bu) => bu.BusinessId == businessId && bu.UserId == userId).ToListAsync();
		if (!businessUsers.Any())
		{
			return false;
		}
		_context.BusinessUsers.RemoveRange(businessUsers);
		await _context.SaveChangesAsync();
		return true;
	}

	public async Task<bool> RemoveBookFromBusinessUserAsync(Guid businessUserId, Guid bookId)
	{
		BusinessUser businessUser = await _context.BusinessUsers.FirstOrDefaultAsync((BusinessUser bu) => bu.Id == businessUserId);
		if (businessUser == null || businessUser.BookIds == null || !businessUser.BookIds.Contains(bookId))
		{
			return false;
		}
		businessUser.BookIds.Remove(bookId);
		_context.BusinessUsers.Update(businessUser);
		await _context.SaveChangesAsync();
		return true;
	}

	public async Task<bool> ExchangeOwnerAsync(Guid currentOwnerId, Guid targetUserId, Guid businessId)
	{
		BusinessUser currentOwner = await _context.BusinessUsers.FirstOrDefaultAsync((BusinessUser bu) => bu.Id == currentOwnerId && bu.BusinessId == businessId);
		if (currentOwner == null || currentOwner.Role.ToLower() != "owner")
		{
			return false;
		}
		BusinessUser targetUser = await _context.BusinessUsers.FirstOrDefaultAsync((BusinessUser bu) => bu.Id == targetUserId && bu.BusinessId == businessId);
		if (targetUser == null)
		{
			return false;
		}
		currentOwner.Role = "partner";
		targetUser.Role = "owner";
		_context.BusinessUsers.Update(currentOwner);
		_context.BusinessUsers.Update(targetUser);
		await _context.SaveChangesAsync();
		return true;
	}
}
