using cashbook.Dto;
using cashbook.Dto.user;
using cashbook.Models;
using System.Security.Claims;


namespace cashbook.Interfaces
{
    public interface IUserRepository : IRepository<User>
    {
        Task<UserDto> Register(RegisterationRequestDto registerationRequestDto);
        Task<LoginResponseDto>? Login(LoginRequestDto loginRequestDto);
        Task<bool> IsPasswordCorrect(RegisterationRequestDto registerationRequestDto);
        Task<bool> IsUniqueUser(string Email);

        Task<bool> UserExistsAsync(Guid Id);

        Task<bool> UpdateUserAsync(Guid id, UserUpdateDto userUpdateDto);
        Task<LoginResponseDto> RefreshTokenAsync(string RefreshToken);
        Task<PaginatedResponse<UserDto>> GetAllUsersAsync(
       Guid businessId,
       int? skip = 1,
       int? take = 25,
       string search = null);
        Task<bool> RemoveUserAsync(Guid id);
        Task<bool> LogoutAsync(ClaimsPrincipal userClaims);
        Task<UserDto?> GetUserByIdAsync(Guid userId, Guid businessId);

    }

}
