using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using cashbook.Data;
using cashbook.Dto;
using cashbook.Dto.user;
using cashbook.Interfaces;
using cashbook.Models;
using cashbook.Services;

namespace cashbook.Repositories;

public class UserRepository : Repository<User>, IUserRepository, IRepository<User>
{
	private readonly ApplicationDbContext _context;

	private readonly ITokenService _tokens;

	public UserRepository(ApplicationDbContext context, ITokenService tokens)
		: base(context)
	{
		_context = context;
		_tokens = tokens;
	}

	// ملاحظة مهمّة: هذه الدوال لا تبتلع الاستثناءات عمداً.
	// «false» هنا تعني «الاسم أو البريد محجوز»، فلو ابتُلع عطل قاعدة البيانات لظهر للمستخدم
	// برسالة كاذبة. الاستثناء ينتشر إلى معالج الأخطاء العام فيُسجَّل العطل الحقيقي.
	public async Task<bool> IsUniqueUser(string email)
	{
		return !await _context.Users.AnyAsync(u => u.Email == email);
	}

	public async Task<bool> IsUsernameAvailableAsync(string username)
	{
		if (string.IsNullOrWhiteSpace(username))
		{
			return false;
		}
		// ترتيب الحروف في قاعدة البيانات لا يميّز الكبيرة من الصغيرة، فالمقارنة المباشرة
		// تستخدم الفهرس، بخلاف ToLower الذي يمسح الجدول كله.
		string normalized = username.Trim();
		return !await _context.Users.AnyAsync(u => u.Username == normalized);
	}

	public Task<bool> UserExistsAsync(Guid id)
	{
		return _context.Users.AnyAsync(u => u.Id == id);
	}

	public async Task<bool> UpdateUserAsync(Guid id, UserUpdateDto userUpdateDto)
	{
		User existingUser = await _context.Users.FindAsync(id);
		if (existingUser == null)
		{
			return false;
		}
		if (!string.IsNullOrWhiteSpace(userUpdateDto.Username))
		{
			string newUsername = userUpdateDto.Username.Trim();
			if (await _context.Users.AnyAsync(u => u.Id != id && u.Username == newUsername))
			{
				return false;
			}
			existingUser.Username = newUsername;
		}
		if (!string.IsNullOrWhiteSpace(userUpdateDto.Email))
		{
			existingUser.Email = userUpdateDto.Email.Trim();
		}
		if (!string.IsNullOrEmpty(userUpdateDto.Password))
		{
			existingUser.Password = BCrypt.Net.BCrypt.HashPassword(userUpdateDto.Password);
		}
		if (!string.IsNullOrWhiteSpace(userUpdateDto.Name))
		{
			existingUser.Name = userUpdateDto.Name.Trim();
		}
		if (userUpdateDto.ProfileImage != null && userUpdateDto.ProfileImage.Length > 0)
		{
			List<string> uploaded = await UploadFilesAsync(new List<IFormFile> { userUpdateDto.ProfileImage }, Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads"));
			if (uploaded.Any())
			{
				existingUser.ProfileImage = uploaded.First();
			}
		}
		existingUser.UpdatedAt = DateTime.UtcNow;
		await _context.SaveChangesAsync();
		return true;
	}

	public async Task<LoginResponseDto?> Login(LoginRequestDto loginRequestDto)
	{
		string identifier = !string.IsNullOrWhiteSpace(loginRequestDto.Username) ? loginRequestDto.Username.Trim() : loginRequestDto.Email?.Trim();
		if (string.IsNullOrWhiteSpace(identifier))
		{
			return null;
		}
		User user = await _context.Users.FirstOrDefaultAsync(u => u.Username == identifier || u.Email == identifier);
		if (user == null || !user.IsActive || !BCrypt.Net.BCrypt.Verify(loginRequestDto.Password ?? "", user.Password))
		{
			return null;
		}
		return await _tokens.IssueAsync(user, loginRequestDto.DeviceToken, "pwd");
	}

	public Task<LoginResponseDto?> RefreshTokenAsync(string refreshToken)
	{
		return _tokens.RefreshAsync(refreshToken);
	}

	public async Task<PaginatedResponse<UserDto>> GetAllUsersAsync(Guid businessId, int? skip = 1, int? take = 25, string search = null)
	{
		int currentPage = Math.Max(1, skip ?? 1);
		int currentPageSize = Math.Clamp(take ?? 25, 1, 200);
		IQueryable<User> query = _context.Users.Where(u => !u.IsHidden && u.BusinessUsers.Any(bu => bu.BusinessId == businessId && bu.BookIds != null && bu.BookIds.Count > 0));
		if (!string.IsNullOrWhiteSpace(search))
		{
			query = query.Where(u => u.Email.Contains(search) || u.Username.Contains(search) || u.Name.Contains(search));
		}
		int totalRecords = await query.CountAsync();
		List<UserDto> users = await query.OrderByDescending(u => u.CreatedAt).Skip((currentPage - 1) * currentPageSize).Take(currentPageSize)
			.Select(u => new UserDto
			{
				Id = u.Id,
				Username = u.Username,
				Email = u.Email,
				Name = u.Name,
				Role = u.BusinessUsers.Where(bu => bu.BusinessId == businessId).Select(bu => bu.Role).FirstOrDefault() ?? ""
			}).ToListAsync();
		return new PaginatedResponse<UserDto>
		{
			TotalRecords = totalRecords,
			Skip = currentPage,
			Take = currentPageSize,
			Data = users
		};
	}

	public async Task<UserDto?> GetUserByIdAsync(Guid userId, Guid businessId)
	{
		return await _context.Users
			.Where(u => u.Id == userId && u.BusinessUsers.Any(bu => bu.BusinessId == businessId))
			.Select(u => new UserDto
			{
				Id = u.Id,
				Username = u.Username,
				Email = u.Email,
				Name = u.Name,
				CreatedAt = u.CreatedAt,
				UpdatedAt = u.UpdatedAt,
				Role = u.BusinessUsers.Where(bu => bu.BusinessId == businessId).Select(bu => bu.Role).FirstOrDefault() ?? "",
				BusinessName = u.BusinessUsers.Where(bu => bu.BusinessId == businessId).Select(bu => bu.Business.Name).FirstOrDefault() ?? ""
			}).FirstOrDefaultAsync();
	}

	public async Task<bool> LogoutAsync(ClaimsPrincipal userClaims)
	{
		if (!Guid.TryParse(userClaims.FindFirst("sessionId")?.Value, out Guid sessionId) || !Guid.TryParse(userClaims.FindFirst(ClaimTypes.NameIdentifier)?.Value, out Guid userId))
		{
			return false;
		}
		Session session = await _context.Sessions.FirstOrDefaultAsync(s => s.Id == sessionId && s.UserId == userId);
		if (session == null)
		{
			return false;
		}
		_context.Sessions.Remove(session);
		await _context.SaveChangesAsync();
		return true;
	}
}
