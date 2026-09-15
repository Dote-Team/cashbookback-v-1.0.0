using cashbook.Data;
using cashbook.Dto;
using cashbook.Dto.book;
using cashbook.Dto.business;
using cashbook.Dto.businessUser;
using cashbook.Dto.user;
using cashbook.Interfaces;
using cashbook.Models;
using Microsoft.EntityFrameworkCore;
using System.Net;

namespace cashbook.Repositories
{
    public class BusinessUserRepository : Repository<BusinessUser>, IBusinessUserRepository
    {

        private readonly ApplicationDbContext _context;
        public BusinessUserRepository(ApplicationDbContext context, IConfiguration configuration) : base(context)
        {
            _context = context;

        }


        public async Task<PaginatedResponse<BusinessUserDto>> GetBusinessUsersAsync(
            Guid businessId,
            int? skip = 1,
            int? take = 25,
            string search = null)
        {
            int currentPage = skip.GetValueOrDefault(1);
            int currentPageSize = take.GetValueOrDefault(25);

            var query = _context.BusinessUsers
                .Where(bu => bu.BusinessId == businessId)
                .Include(bu => bu.User) // assuming navigation property 'User'
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(bu => bu.User.Name.Contains(search)); // filtering by User's name
            }

            int totalRecords = await query.CountAsync();

            var businessUsers = await query
                .Skip((currentPage - 1) * currentPageSize)
                .Take(currentPageSize)
                .ToListAsync();

            var result = new List<BusinessUserDto>();

            foreach (var bu in businessUsers)
            {
                var dto = new BusinessUserDto
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

                if (bu.Role == "admin" || bu.Role == "dataoperator" || bu.Role == "staff" || bu.Role == "privateviewer")
                {
                    if (bu.BookIds != null && bu.BookIds.Any())
                    {
                        var books = await _context.Books
                            .Where(b => bu.BookIds.Contains(b.Id))
                            .Select(b => new BookDto
                            {
                                Id = b.Id,
                                Name = b.Name
                            }).ToListAsync();

                        dto.Books = books;
                    }
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
            var bu = await _context.BusinessUsers
                .Include(bu => bu.User)
                .Include(bu => bu.Business)
                .FirstOrDefaultAsync(bu => bu.Id == businessUserId);

            if (bu == null)
            {
                return null; // or throw exception if you prefer
            }

            var dto = new BusinessUserDto
            {
                Id = bu.Id,
                UserId = bu.UserId,
                BusinessId = bu.BusinessId,
                Role = bu.Role,
                User = bu.User == null ? null : new UserDto
                {
                    Id = bu.User.Id,
                    Name = bu.User.Name,
                    Email = bu.User.Email
                },
                Business = bu.Business == null ? null : new BusinessDto
                {
                    Id = bu.Business.Id,
                    Name = bu.Business.Name
                },
                Books = new List<BookDto>()
            };

            if (bu.BookIds != null && bu.BookIds.Any())
            {
                var books = await _context.Books
                    .Where(b => bu.BookIds.Contains(b.Id))
                    .Select(b => new BookDto
                    {
                        Id = b.Id,
                        Name = b.Name
                    })
                    .ToListAsync();

                dto.Books = books;
            }

            return dto;
        }


        public async Task<APIResponse> UpdateBusinessUserAsync(Guid businessUserId, UpdateBusinessUserDto dto)
        {
            var businessUser = await _context.BusinessUsers
                .FirstOrDefaultAsync(bu => bu.Id == businessUserId);

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


            var roleWithBooks = new[] { "staff", "dataoperator", "admin", "privateviewer" };
            var isBookRole = !string.IsNullOrEmpty(dto.Role) && roleWithBooks.Contains(dto.Role.ToLower());

            if (isBookRole && dto.BookIds != null && dto.BookIds.Any())
            {
                var conflictingBusinessUsers = await _context.BusinessUsers
                    .Where(bu => bu.UserId == businessUser.UserId
                                 && bu.BusinessId == (dto.BusinessId ?? businessUser.BusinessId)
                                 && bu.Id != businessUserId
                                 && roleWithBooks.Contains(bu.Role.ToLower())
                                 && bu.BookIds.Any(bookId => dto.BookIds.Contains(bookId)))
                    .ToListAsync();

                if (conflictingBusinessUsers.Any())
                {


                    foreach (var conflictUser in conflictingBusinessUsers)
                    {
                        var intersectingBooks = conflictUser.BookIds.Intersect(dto.BookIds).ToList();
                        if (intersectingBooks.Any())
                        {
                            // Remove the conflicting books from the conflicting role
                            conflictUser.BookIds = conflictUser.BookIds.Except(intersectingBooks).ToList();
                            _context.BusinessUsers.Update(conflictUser);

                            // Merge the books into the target businessUser
                            if (businessUser.BookIds == null)
                            {
                                businessUser.BookIds = new List<Guid>();
                            }

                            foreach (var bookId in intersectingBooks)
                            {
                                if (!businessUser.BookIds.Contains(bookId))
                                {
                                    businessUser.BookIds.Add(bookId);
                                }
                            }
                        }
                    }

                }
            }

            if (dto.BusinessId.HasValue)
                businessUser.BusinessId = dto.BusinessId.Value;

            if (!string.IsNullOrEmpty(dto.Role))
                businessUser.Role = dto.Role;

            if (dto.BookIds != null)
            {
                if (businessUser.BookIds == null)
                    businessUser.BookIds = new List<Guid>();

                foreach (var bookId in dto.BookIds)
                {
                    if (!businessUser.BookIds.Contains(bookId))
                    {
                        businessUser.BookIds.Add(bookId);
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
            var businessUsers = await _context.BusinessUsers
                .Where(bu => bu.BusinessId == businessId && bu.UserId == userId)
                .ToListAsync();

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
            var businessUser = await _context.BusinessUsers
                .FirstOrDefaultAsync(bu => bu.Id == businessUserId);

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
            var currentOwner = await _context.BusinessUsers
                .FirstOrDefaultAsync(bu => bu.Id == currentOwnerId && bu.BusinessId == businessId);

            if (currentOwner == null || currentOwner.Role.ToLower() != "owner")
            {
                return false; // Only the current owner can perform this action
            }

            var targetUser = await _context.BusinessUsers
                .FirstOrDefaultAsync(bu => bu.Id == targetUserId && bu.BusinessId == businessId);

            if (targetUser == null)
            {
                return false; // Target user not found in this business
            }

            // Swap roles
            currentOwner.Role = "partner";
            targetUser.Role = "owner";

            _context.BusinessUsers.Update(currentOwner);
            _context.BusinessUsers.Update(targetUser);

            await _context.SaveChangesAsync();

            return true;
        }

    }
}

