using System;
using System.Collections.Generic;
using System.IO;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using BCrypt.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
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

	private readonly IExchangeRateAuthorization _exchangeRateAuthorization;

	private readonly string _secretKey;

	private readonly int _expireTokenHours;

	private readonly int _refreshTokenExpire;

	public UserRepository(ApplicationDbContext context, IConfiguration configuration, IExchangeRateAuthorization exchangeRateAuthorization)
		: base(context)
	{
		_context = context;
		_exchangeRateAuthorization = exchangeRateAuthorization;
		_secretKey = configuration.GetValue<string>("ApiSettings:Secret");
		_expireTokenHours = configuration.GetValue<int>("Jwt:ExpireHours");
		_refreshTokenExpire = configuration.GetValue<int>("Jwt:RefreshExpireDays");
	}

	private async Task<(bool IsSuperAdmin, bool CanManage, List<Guid> BusinessIds)> GetExchangeRateCapabilitiesAsync(User user)
	{
		List<Guid> businessIds = await _exchangeRateAuthorization.GetManageableBusinessIdsAsync(user.Id);
		return (IsSuperAdmin: user.IsSuperAdmin, CanManage: user.IsSuperAdmin || businessIds.Any(), BusinessIds: businessIds);
	}

	public async Task<bool> IsPasswordCorrect(RegisterationRequestDto registerationRequestDto)
	{
		try
		{
			User user = await _context.Users.FirstOrDefaultAsync((User u) => u.Email == registerationRequestDto.Email);
			return user != null && BCrypt.Net.BCrypt.Verify(registerationRequestDto.Password, user.Password);
		}
		catch (Exception)
		{
			return false;
		}
	}

	public async Task<bool> IsUniqueUser(string email)
	{
		try
		{
			return !(await _context.Users.AnyAsync((User u) => u.Email == email));
		}
		catch (Exception)
		{
			return false;
		}
	}

	public async Task<bool> IsUsernameAvailableAsync(string username)
	{
		try
		{
			if (string.IsNullOrWhiteSpace(username))
			{
				return false;
			}
			string normalized = username.Trim().ToLower();
			return !(await _context.Users.AnyAsync((User u) => u.Username.ToLower() == normalized));
		}
		catch (Exception)
		{
			return false;
		}
	}

	private static string BuildInternalEmail(string username)
	{
		string text = new string((from c in (username ?? "user").Trim().ToLowerInvariant()
			where char.IsLetterOrDigit(c) || c == '.' || c == '_' || c == '-'
			select c).ToArray());
		if (string.IsNullOrWhiteSpace(text))
		{
			text = "user";
		}
		return text + "@local.invalid";
	}

	public async Task<UserDto> Register(RegisterationRequestDto registerationRequestDto)
	{
		try
		{
			string username = registerationRequestDto.Username?.Trim();
			User user = new User
			{
				Username = username,
				Email = (string.IsNullOrWhiteSpace(registerationRequestDto.Email) ? BuildInternalEmail(username) : registerationRequestDto.Email.Trim()),
				Password = BCrypt.Net.BCrypt.HashPassword(registerationRequestDto.Password),
				Name = registerationRequestDto.Name
			};
			_context.Users.Add(user);
			await _context.SaveChangesAsync();
			return new UserDto
			{
				Id = user.Id,
				Username = user.Username,
				Email = user.Email,
				Name = user.Name
			};
		}
		catch (Exception)
		{
			throw;
		}
	}

	public async Task<bool> UserExistsAsync(Guid Id)
	{
		try
		{
			return await _context.Users.AnyAsync((User u) => u.Id == Id);
		}
		catch (Exception)
		{
			return false;
		}
	}

	public async Task<bool> UpdateUserAsync(Guid id, UserUpdateDto userUpdateDto)
	{
		using (IDbContextTransaction transaction = await _context.Database.BeginTransactionAsync())
		{
			try
			{
				User existingUser = await _context.Users.FindAsync(id);
				if (existingUser == null)
				{
					return false;
				}
				if (!string.IsNullOrWhiteSpace(userUpdateDto.Username))
				{
					string newUsername = userUpdateDto.Username.Trim();
					if (await _context.Users.AnyAsync((User u) => u.Id != id && u.Username.ToLower() == newUsername.ToLower()))
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
					List<string> uploaded = await UploadFilesAsync(uploadPath: Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads"), files: new List<IFormFile> { userUpdateDto.ProfileImage });
					if (uploaded.Any())
					{
						existingUser.ProfileImage = uploaded.First();
					}
				}
				_context.Entry(existingUser).State = EntityState.Modified;
				await _context.SaveChangesAsync();
				await transaction.CommitAsync();
				return true;
			}
			catch (Exception)
			{
				await transaction.RollbackAsync();
				return false;
			}
		}
		IL_0777:
		throw null;
	}

	public async Task<LoginResponseDto> Login(LoginRequestDto loginRequestDto)
	{
		try
		{
			string identifier = ((!string.IsNullOrWhiteSpace(loginRequestDto.Username)) ? loginRequestDto.Username.Trim() : loginRequestDto.Email?.Trim());
			if (string.IsNullOrWhiteSpace(identifier))
			{
				return null;
			}
			string normalizedIdentifier = identifier.ToLower();
			User user = await _context.Users.Include((User u) => u.BusinessUsers).FirstOrDefaultAsync((User u) => u.Username.ToLower() == normalizedIdentifier || u.Email.ToLower() == normalizedIdentifier);
			if (user == null || !BCrypt.Net.BCrypt.Verify(loginRequestDto.Password, user.Password))
			{
				return null;
			}
			byte[] key = Encoding.ASCII.GetBytes(_secretKey);
			Guid SessionId = Guid.NewGuid();
			Claim[] claims = new Claim[3]
			{
				new Claim("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name", user.Username ?? user.Email),
				new Claim("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier", user.Id.ToString()),
				new Claim("sessionId", SessionId.ToString())
			};
			ClaimsIdentity identity = new ClaimsIdentity(claims);
			JwtSecurityTokenHandler tokenHandler = new JwtSecurityTokenHandler();
			SecurityTokenDescriptor tokenDescriptor = new SecurityTokenDescriptor
			{
				Subject = identity,
				Expires = DateTime.UtcNow.AddHours(_expireTokenHours),
				SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), "http://www.w3.org/2001/04/xmldsig-more#hmac-sha256")
			};
			SecurityTokenDescriptor refreshTokenDescriptor = new SecurityTokenDescriptor
			{
				Subject = identity,
				Expires = DateTime.UtcNow.AddDays(_refreshTokenExpire),
				SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), "http://www.w3.org/2001/04/xmldsig-more#hmac-sha256")
			};
			SecurityToken accessToken = tokenHandler.CreateToken(tokenDescriptor);
			string accessTokenString = tokenHandler.WriteToken(accessToken);
			SecurityToken refreshToken = tokenHandler.CreateToken(refreshTokenDescriptor);
			string refreshTokenString = tokenHandler.WriteToken(refreshToken);
			string refreshTokenHash = BCrypt.Net.BCrypt.HashPassword(refreshTokenString);
			Session session = new Session
			{
				Id = SessionId,
				UserId = user.Id,
				RefreshToken = refreshTokenHash,
				DeviceToken = (loginRequestDto.DeviceToken ?? ""),
				ExpiredAt = DateTime.UtcNow.AddDays(_refreshTokenExpire)
			};
			await _context.Sessions.AddAsync(session);
			await _context.SaveChangesAsync();
			(bool IsSuperAdmin, bool CanManage, List<Guid> BusinessIds) capabilities = await GetExchangeRateCapabilitiesAsync(user);
			LoginResponseDto obj = new LoginResponseDto
			{
				Email = user.Email,
				Username = user.Username,
				Name = user.Name,
				ProfileImage = user.ProfileImage,
				AccessToken = accessTokenString,
				RefreshToken = refreshTokenString,
				SessionId = SessionId,
				Id = user.Id
			};
			(obj.IsSuperAdmin, obj.CanManageExchangeRate, obj.ExchangeRateBusinessIds) = capabilities;
			return obj;
		}
		catch (Exception ex)
		{
			Exception ex2 = ex;
			Console.WriteLine("Error during login: " + ex2.Message);
			return null;
		}
	}

	public async Task<LoginResponseDto> RefreshTokenAsync(string RefreshToken)
	{
		try
		{
			JwtSecurityTokenHandler tokenHandler = new JwtSecurityTokenHandler();
			byte[] key = Encoding.ASCII.GetBytes(_secretKey);
			ClaimsPrincipal principal = tokenHandler.ValidateToken(RefreshToken, new TokenValidationParameters
			{
				ValidateIssuerSigningKey = true,
				IssuerSigningKey = new SymmetricSecurityKey(key),
				ValidateIssuer = false,
				ValidateAudience = false,
				ValidateLifetime = true,
				ClockSkew = TimeSpan.Zero
			}, out var validatedToken);
			if (!(validatedToken is JwtSecurityToken jwtToken) || !jwtToken.Header.Alg.Equals("HS256", StringComparison.InvariantCultureIgnoreCase))
			{
				return null;
			}
			string userIdStr = principal.FindFirstValue("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier");
			string sessionIdStr = principal.FindFirstValue("sessionId");
			if (!Guid.TryParse(sessionIdStr, out var sessionId) || !Guid.TryParse(userIdStr, out var userId))
			{
				return null;
			}
			User user = await _context.Users.Include((User u) => u.BusinessUsers).FirstOrDefaultAsync((User u) => u.Id == userId);
			if (user == null)
			{
				return null;
			}
			Session session = await _context.Sessions.FirstOrDefaultAsync((Session s) => s.Id == sessionId && s.UserId == userId);
			if (session == null)
			{
				return null;
			}
			_ = session.ExpiredAt;
			if (session.ExpiredAt < DateTime.UtcNow)
			{
				return null;
			}
			if (!BCrypt.Net.BCrypt.Verify(RefreshToken, session.RefreshToken))
			{
				return null;
			}
			Claim[] claims = new Claim[3]
			{
				new Claim("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name", user.Username ?? user.Email),
				new Claim("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier", user.Id.ToString()),
				new Claim("sessionId", sessionId.ToString())
			};
			ClaimsIdentity identity = new ClaimsIdentity(claims);
			SecurityToken newAccessToken = tokenHandler.CreateToken(new SecurityTokenDescriptor
			{
				Subject = identity,
				Expires = DateTime.UtcNow.AddHours(_expireTokenHours),
				SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), "http://www.w3.org/2001/04/xmldsig-more#hmac-sha256")
			});
			SecurityToken newRefreshToken = tokenHandler.CreateToken(new SecurityTokenDescriptor
			{
				Subject = identity,
				Expires = DateTime.UtcNow.AddDays(_refreshTokenExpire),
				SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), "http://www.w3.org/2001/04/xmldsig-more#hmac-sha256")
			});
			string newRefreshTokenRaw = tokenHandler.WriteToken(newRefreshToken);
			session.RefreshToken = BCrypt.Net.BCrypt.HashPassword(newRefreshTokenRaw);
			session.ExpiredAt = DateTime.UtcNow.AddDays(_refreshTokenExpire);
			await _context.SaveChangesAsync();
			(bool IsSuperAdmin, bool CanManage, List<Guid> BusinessIds) refreshCapabilities = await GetExchangeRateCapabilitiesAsync(user);
			LoginResponseDto obj = new LoginResponseDto
			{
				Email = user.Email,
				Username = user.Username,
				AccessToken = tokenHandler.WriteToken(newAccessToken),
				RefreshToken = newRefreshTokenRaw,
				SessionId = session.Id,
				Name = user.Name,
				ProfileImage = user.ProfileImage,
				Id = user.Id
			};
			(obj.IsSuperAdmin, obj.CanManageExchangeRate, obj.ExchangeRateBusinessIds) = refreshCapabilities;
			return obj;
		}
		catch (SecurityTokenExpiredException)
		{
			return null;
		}
		catch (Exception ex2)
		{
			Exception ex3 = ex2;
			Console.WriteLine("Error refreshing token: " + ex3.Message);
			return null;
		}
	}

	public async Task<PaginatedResponse<UserDto>> GetAllUsersAsync(Guid businessId, int? skip = 1, int? take = 25, string search = null)
	{
		int currentPage = skip ?? 1;
		int currentPageSize = take ?? 25;
		IQueryable<User> query = _context.Users.Where((User u) => u.BusinessUsers.Any((BusinessUser bu) => bu.BusinessId == businessId));
		if (!string.IsNullOrWhiteSpace(search))
		{
			query = query.Where((User u) => u.Email.Contains(search) || u.Username.Contains(search) || u.Name.Contains(search));
		}
		int totalRecords = await query.CountAsync();
		List<UserDto> users = await (from u in query.OrderByDescending((User u) => u.CreatedAt).Skip((currentPage - 1) * currentPageSize).Take(currentPageSize)
			select new UserDto
			{
				Id = u.Id,
				Username = u.Username,
				Email = u.Email,
				Name = u.Name,
				Role = ((from bu in u.BusinessUsers
					where bu.BusinessId == businessId
					select bu.Role).FirstOrDefault() ?? "")
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
		return await (from u in _context.Users
			where u.Id == userId && u.BusinessUsers.Any((BusinessUser bu) => bu.BusinessId == businessId)
			select new UserDto
			{
				Id = u.Id,
				Username = u.Username,
				Email = u.Email,
				Name = u.Name,
				CreatedAt = u.CreatedAt,
				UpdatedAt = u.UpdatedAt,
				Role = ((from bu in u.BusinessUsers
					where bu.BusinessId == businessId
					select bu.Role).FirstOrDefault() ?? ""),
				BusinessName = ((from bu in u.BusinessUsers
					where bu.BusinessId == businessId
					select bu.Business.Name).FirstOrDefault() ?? "")
			}).FirstOrDefaultAsync();
	}

	public async Task<bool> RemoveUserAsync(Guid id)
	{
		using (IDbContextTransaction transaction = await _context.Database.BeginTransactionAsync())
		{
			try
			{
				User user = await _context.Users.FindAsync(id);
				if (user == null)
				{
					return false;
				}
				_context.Users.Remove(user);
				await _context.SaveChangesAsync();
				await transaction.CommitAsync();
				return true;
			}
			catch (Exception)
			{
				await transaction.RollbackAsync();
				return false;
			}
		}
		IL_036f:
		throw null;
	}

	public async Task<bool> LogoutAsync(ClaimsPrincipal userClaims)
	{
		try
		{
			string sessionIdStr = userClaims.FindFirst("sessionId")?.Value;
			string userIdStr = userClaims.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")?.Value;
			if (!Guid.TryParse(sessionIdStr, out var sessionId) || !Guid.TryParse(userIdStr, out var userId))
			{
				return false;
			}
			Session session = await _context.Sessions.FirstOrDefaultAsync((Session s) => s.Id == sessionId && s.UserId == userId);
			if (session == null)
			{
				return false;
			}
			_context.Sessions.Remove(session);
			await _context.SaveChangesAsync();
			return true;
		}
		catch
		{
			return false;
		}
	}
}
