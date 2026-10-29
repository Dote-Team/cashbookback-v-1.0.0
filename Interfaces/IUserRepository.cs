using System;
using System.Security.Claims;
using System.Threading.Tasks;
using cashbook.Dto;
using cashbook.Dto.user;
using cashbook.Models;

namespace cashbook.Interfaces;

public interface IUserRepository : IRepository<User>
{
	/// <summary>الدخول بكلمة المرور. يعيد null إن كانت البيانات خاطئة أو الحساب معطّلاً.</summary>
	Task<LoginResponseDto?> Login(LoginRequestDto loginRequestDto);

	Task<bool> IsUniqueUser(string email);

	Task<bool> IsUsernameAvailableAsync(string username);

	Task<bool> UserExistsAsync(Guid id);

	Task<bool> UpdateUserAsync(Guid id, UserUpdateDto userUpdateDto);

	Task<LoginResponseDto?> RefreshTokenAsync(string refreshToken);

	Task<PaginatedResponse<UserDto>> GetAllUsersAsync(Guid businessId, int? skip = 1, int? take = 25, string search = null);

	Task<bool> LogoutAsync(ClaimsPrincipal userClaims);

	Task<UserDto?> GetUserByIdAsync(Guid userId, Guid businessId);
}
