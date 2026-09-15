using cashbook.Data;
using cashbook.Dto;
using cashbook.Dto.user;
using cashbook.Interfaces;
using cashbook.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace cashbook.Repositories
{
    public class UserRepository : Repository<User>, IUserRepository
    {
        private readonly ApplicationDbContext _context;
        private readonly string _secretKey;
        private readonly int _expireTokenHours;
        private readonly int _refreshTokenExpire;

        public UserRepository(ApplicationDbContext context, IConfiguration configuration) : base(context)
        {
            _context = context;
            _secretKey = configuration.GetValue<string>("ApiSettings:Secret");
            _expireTokenHours = configuration.GetValue<int>("Jwt:ExpireHours");
            _refreshTokenExpire = configuration.GetValue<int>("Jwt:RefreshExpireDays");
        }

        public async Task<bool> IsPasswordCorrect(RegisterationRequestDto registerationRequestDto)
        {
            try
            {
                var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == registerationRequestDto.Email);
                return user != null && BCrypt.Net.BCrypt.Verify(registerationRequestDto.Password, user.Password);
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        public async Task<bool> IsUniqueUser(string email)
        {
            try
            {
                bool user = await _context.Users.AnyAsync(u => u.Email == email);
                return !user;

            }
            catch (Exception ex)
            {
                return false;
            }
        }

        public async Task<UserDto> Register(RegisterationRequestDto registerationRequestDto)
        {
            try
            {
                var user = new User
                {
                    Email = registerationRequestDto.Email,
                    Password = BCrypt.Net.BCrypt.HashPassword(registerationRequestDto.Password),
                    Name = registerationRequestDto.Name,
                };

                _context.Users.Add(user);
                await _context.SaveChangesAsync();





                return new UserDto
                {
                    Id = user.Id,
                    Email = user.Email,
                    Name = user.Name,
                };
            }
            catch (Exception ex)
            {
                throw;
            }
        }

        public async Task<bool> UserExistsAsync(Guid Id)
        {
            try
            {
                return await _context.Users.AnyAsync(u => u.Id == Id);
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        public async Task<bool> UpdateUserAsync(Guid id, UserUpdateDto userUpdateDto)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var existingUser = await _context.Users.FindAsync(id);
                if (existingUser == null)
                {
                    return false;
                }

                existingUser.Email = userUpdateDto.Email;
                if (!string.IsNullOrEmpty(userUpdateDto.Password))
                {
                    existingUser.Password = BCrypt.Net.BCrypt.HashPassword(userUpdateDto.Password);
                }
                existingUser.Name = userUpdateDto.Name;

                // Handle profile image upload using your UploadFilesAsync method
                if (userUpdateDto.ProfileImage != null && userUpdateDto.ProfileImage.Length > 0)
                {
                    string uploadPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
                    var uploaded = await UploadFilesAsync(new List<IFormFile> { userUpdateDto.ProfileImage }, uploadPath);

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

        public async Task<LoginResponseDto> Login(LoginRequestDto loginRequestDto)
        {
            try
            {
                var user = await _context.Users
                    .Include(u => u.BusinessUsers)
                    .FirstOrDefaultAsync(u => u.Email.ToLower() == loginRequestDto.Email.ToLower());

                if (user == null || !BCrypt.Net.BCrypt.Verify(loginRequestDto.Password, user.Password))
                {
                    return null;
                }



                var key = Encoding.ASCII.GetBytes(_secretKey);

                var SessionId = Guid.NewGuid();


                // ✅ Create one ClaimsIdentity and use it for both tokens
                var claims = new[]
                {
                    new Claim(ClaimTypes.Name, user.Email),
                    new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                    new Claim("sessionId", SessionId.ToString()),
                };

                var identity = new ClaimsIdentity(claims);

                var tokenHandler = new JwtSecurityTokenHandler();

                var tokenDescriptor = new SecurityTokenDescriptor
                {
                    Subject = identity,
                    Expires = DateTime.UtcNow.AddHours(_expireTokenHours),
                    SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
                };

                var refreshTokenDescriptor = new SecurityTokenDescriptor
                {
                    Subject = identity,
                    Expires = DateTime.UtcNow.AddDays(_refreshTokenExpire),
                    SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
                };

                var accessToken = tokenHandler.CreateToken(tokenDescriptor);
                var accessTokenString = tokenHandler.WriteToken(accessToken);

                var refreshToken = tokenHandler.CreateToken(refreshTokenDescriptor);
                var refreshTokenString = tokenHandler.WriteToken(refreshToken);

                var refreshTokenHash = BCrypt.Net.BCrypt.HashPassword(refreshTokenString);
                var session = new Session
                {
                    Id = SessionId,
                    UserId = user.Id,
                    RefreshToken = refreshTokenHash,
                    DeviceToken = loginRequestDto.DeviceToken ?? "",
                    ExpiredAt = DateTime.UtcNow.AddDays(7)
                };
                await _context.Sessions.AddAsync(session);

                await _context.SaveChangesAsync();


                return new LoginResponseDto
                {
                    Email = user.Email,
                    Name = user.Name,
                    ProfileImage = user.ProfileImage,
                    AccessToken = accessTokenString,
                    RefreshToken = refreshTokenString,
                    SessionId = SessionId,
                    Id = user.Id
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during login: {ex.Message}");
                return null;
            }
        }
        public async Task<LoginResponseDto> RefreshTokenAsync(string RefreshToken)
        {
            try
            {
                var tokenHandler = new JwtSecurityTokenHandler();
                var key = Encoding.ASCII.GetBytes(_secretKey);

                var principal = tokenHandler.ValidateToken(RefreshToken, new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero
                }, out SecurityToken validatedToken);

                if (validatedToken is not JwtSecurityToken jwtToken ||
                    !jwtToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha256, StringComparison.InvariantCultureIgnoreCase))
                    return null;

                var userIdStr = principal.FindFirstValue(ClaimTypes.NameIdentifier);
                var sessionIdStr = principal.FindFirstValue("sessionId");
                var email = principal.FindFirstValue(ClaimTypes.Name);

                if (!Guid.TryParse(sessionIdStr, out var sessionId) || !Guid.TryParse(userIdStr, out var userId))
                    return null;



                var user = await _context.Users
                    .Include(u => u.BusinessUsers)
                    .FirstOrDefaultAsync(u => u.Id == userId && u.Email.ToLower() == email.ToLower());

                if (user == null)
                    return null;



                var session = await _context.Sessions
                    .FirstOrDefaultAsync(s => s.Id == sessionId && s.UserId == userId);

                if (session == null)
                    return null;

                if (!BCrypt.Net.BCrypt.Verify(RefreshToken, session.RefreshToken))
                    return null;

                // ✅ Regenerate new tokens
                var claims = new[]
                {
            new Claim(ClaimTypes.Name, user.Email),
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim("sessionId", sessionId.ToString()),
        };

                var identity = new ClaimsIdentity(claims);

                var newAccessToken = tokenHandler.CreateToken(new SecurityTokenDescriptor
                {
                    Subject = identity,
                    Expires = DateTime.UtcNow.AddHours(_expireTokenHours),
                    SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
                });

                var newRefreshToken = tokenHandler.CreateToken(new SecurityTokenDescriptor
                {
                    Subject = identity,
                    Expires = DateTime.UtcNow.AddDays(_refreshTokenExpire),
                    SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
                });

                var newRefreshTokenRaw = tokenHandler.WriteToken(newRefreshToken);
                session.RefreshToken = BCrypt.Net.BCrypt.HashPassword(newRefreshTokenRaw);
                session.ExpiredAt = DateTime.UtcNow.AddDays(_refreshTokenExpire);

                await _context.SaveChangesAsync();

                return new LoginResponseDto
                {
                    Email = user.Email,
                    AccessToken = tokenHandler.WriteToken(newAccessToken),
                    RefreshToken = newRefreshTokenRaw,
                    SessionId = session.Id,
                    Id = user.Id
                };
            }
            catch (SecurityTokenExpiredException)
            {
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error refreshing token: {ex.Message}");
                return null;
            }
        }


        public async Task<PaginatedResponse<UserDto>> GetAllUsersAsync(
     Guid businessId,
     int? skip = 1,
     int? take = 25,
     string search = null)
        {


            int currentPage = skip.GetValueOrDefault(1);
            int currentPageSize = take.GetValueOrDefault(25);

            var query = _context.Users
                .Where(u => u.BusinessUsers.Any(bu => bu.BusinessId == businessId));

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(u =>
                       u.Email.Contains(search) ||
                       u.Name.Contains(search));
            }

            int totalRecords = await query.CountAsync();

            var users = await query
                .OrderByDescending(u => u.CreatedAt)
                .Skip((currentPage - 1) * currentPageSize)
                .Take(currentPageSize)
                .Select(u => new UserDto
                {
                    Id = u.Id,
                    Email = u.Email,
                    Name = u.Name,
                    Role = u.BusinessUsers
                        .Where(bu => bu.BusinessId == businessId)
                        .Select(bu => bu.Role)
                        .FirstOrDefault() ?? ""
                })
                .ToListAsync();

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
            var user = await _context.Users
                .Where(u => u.Id == userId && u.BusinessUsers.Any(bu => bu.BusinessId == businessId))
                .Select(u => new UserDto
                {
                    Id = u.Id,
                    Email = u.Email,
                    Name = u.Name,
                    CreatedAt = u.CreatedAt,
                    UpdatedAt = u.UpdatedAt,
                    Role = u.BusinessUsers
                        .Where(bu => bu.BusinessId == businessId)
                        .Select(bu => bu.Role)
                        .FirstOrDefault() ?? "",
                    BusinessName = u.BusinessUsers
                .Where(bu => bu.BusinessId == businessId)
                .Select(bu => bu.Business.Name)
                .FirstOrDefault() ?? ""
                })

                .FirstOrDefaultAsync();

            return user;
        }



        public async Task<bool> RemoveUserAsync(Guid id)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var user = await _context.Users.FindAsync(id);
                if (user == null)
                {
                    return false;
                }



                _context.Users.Remove(user);

                // Save changes
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

        public async Task<bool> LogoutAsync(ClaimsPrincipal userClaims)
        {
            try
            {
                var sessionIdStr = userClaims.FindFirst("sessionId")?.Value;
                var userIdStr = userClaims.FindFirst(ClaimTypes.NameIdentifier)?.Value;

                if (!Guid.TryParse(sessionIdStr, out Guid sessionId) || !Guid.TryParse(userIdStr, out Guid userId))
                    return false;

                var session = await _context.Sessions
                    .FirstOrDefaultAsync(s => s.Id == sessionId && s.UserId == userId);

                if (session == null)
                    return false;

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
}
