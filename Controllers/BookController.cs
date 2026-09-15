using AutoMapper;
using cashbook.Dto;
using cashbook.Models;
using cashbook.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Linq.Expressions;
using System.Net;
using cashbook.Dto.book;
using Microsoft.EntityFrameworkCore;
using cashbook.Data;
using System.Security.Claims;
using Org.BouncyCastle.Asn1.X509;
using Swashbuckle.AspNetCore.Annotations;
using cashbook.Dto.setting;
using cashbook.Dto.Book;

namespace cashbook.Controllers
{
    [Route("API/[Controller]")]
    [ApiController]
    public class BookController : ControllerBase
    {
        protected APIResponse _response;
        private readonly IMapper _mapper;
        private readonly IBookRepository _bookRepository;


        public BookController(IBookRepository bookRepository, IMapper mapper)
        {
            _bookRepository = bookRepository;
            _response = new();
            _mapper = mapper;
        }

        // GET: API/Book
        [HttpGet]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [SwaggerOperation(Summary = "Get paginated books with sorting")]
        public async Task<ActionResult<APIResponse>> GetPaginatedBooks(
      [FromQuery] Guid businessId,
      [FromQuery] int? skip = 1,
      [FromQuery] int? take = 25,
      [FromQuery] string? search = null,
      [FromQuery] SortField? sortBy = null,
      [FromQuery] SortDirection? sortDirection = SortDirection.desc)
        {
            try
            {
                var allowedRoles = new List<string> { "owner", "partner", "viewer", "staff", "admin", "dataoperator", "privateviewer" };

                var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var userId = Guid.Parse(userIdStr);

                var userRole = await _bookRepository.GetUserRoleAsync(userId, businessId);

                if (userRole == null || !allowedRoles.Contains(userRole.ToLower()))
                {
                    _response.StatusCode = HttpStatusCode.Forbidden;
                    _response.IsSuccess = false;
                    _response.ErrorMessages = new List<string> { "You do not have permission to view books for this business." };
                    return Forbid();
                }

                var paginatedBooks = await _bookRepository.GetBooksAsync(
                    businessId,
                    userId,
                    skip,
                    take,
                    search,
                    sortBy,
                    sortDirection);

                var paginatedResponse = new PaginatedResponse<BookDto>
                {
                    Data = paginatedBooks.Data,
                    TotalRecords = paginatedBooks.TotalRecords,
                    Skip = paginatedBooks.Skip,
                    Take = paginatedBooks.Take
                };

                _response.Result = paginatedResponse;
                _response.StatusCode = HttpStatusCode.OK;
                _response.IsSuccess = true;

                return Ok(_response);
            }
            catch (UnauthorizedAccessException uaex)
            {
                _response.IsSuccess = false;
                _response.StatusCode = HttpStatusCode.Forbidden;
                _response.ErrorMessages = new List<string> { uaex.Message };
                return StatusCode(StatusCodes.Status403Forbidden, _response);
            }
            catch (Exception ex)
            {
                _response.IsSuccess = false;
                _response.ErrorMessages = new List<string> { ex.ToString() };
                return StatusCode(StatusCodes.Status500InternalServerError, _response);
            }
        }



        // GET: API/Book/{id}
        [HttpGet("{id:Guid}", Name = "GetBook")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [SwaggerOperation(Summary = "all")]

        public async Task<ActionResult<APIResponse>> GetBook(Guid id)

        {
            try
            {

                var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var userId = Guid.Parse(userIdStr);

                if (id == Guid.Empty)
                {
                    _response.StatusCode = HttpStatusCode.BadRequest;
                    _response.IsSuccess = false;
                    _response.ErrorMessages = new List<string> { "Invalid book ID." };
                    return BadRequest(_response);
                }

                var bookDto = await _bookRepository.GetBookDetailsByIdAsync(id, userId);

                if (bookDto == null)
                {
                    _response.StatusCode = HttpStatusCode.NotFound;
                    _response.IsSuccess = false;
                    _response.ErrorMessages = new List<string> { "Book not found." };
                    return NotFound(_response);
                }

                _response.Result = bookDto;
                _response.StatusCode = HttpStatusCode.OK;
                _response.IsSuccess = true;
                return Ok(_response);
            }
            catch (Exception ex)
            {
                _response.IsSuccess = false;
                _response.ErrorMessages = new List<string> { ex.Message };
                _response.StatusCode = HttpStatusCode.InternalServerError;
                return StatusCode(StatusCodes.Status500InternalServerError, _response);
            }
        }


        // POST: API/Book
        [HttpPost]
        [Authorize]

        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [SwaggerOperation(Summary = "owner , partner")]

        public async Task<ActionResult<APIResponse>> CreateBook([FromBody] CreateBookDto createBookDto)
        {
            try
            {

                var allowedRoles = new List<string> { "owner", "partner" };


                var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var userId = Guid.Parse(userIdStr);

                var userRole = await _bookRepository.GetUserRoleAsync(userId, createBookDto.BusinessId);


                if (userRole == null || !allowedRoles.Contains(userRole.ToLower()))
                {
                    _response.StatusCode = HttpStatusCode.Forbidden;
                    _response.IsSuccess = false;
                    _response.ErrorMessages = new List<string> { "You do not have permission to add books for this business." };
                    return Forbid();
                }

                if (createBookDto == null)
                {
                    return BadRequest(createBookDto);
                }

                await _bookRepository.CreateBookAsync(createBookDto);
                _response.StatusCode = HttpStatusCode.Created;
                _response.IsSuccess = true;

                return _response;
            }
            catch (Exception ex)
            {
                _response.IsSuccess = false;
                _response.ErrorMessages = new List<string>() { ex.ToString() };
            }
            return _response;
        }

        [HttpDelete("{id:Guid}", Name = "DeleteBook")]
        [Authorize]

        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [SwaggerOperation(Summary = "owner , partner")]

        public async Task<ActionResult<APIResponse>> DeleteBook(Guid id, Guid businessId)
        {
            try
            {

                var allowedRoles = new List<string> { "owner", "partner" };


                var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var userId = Guid.Parse(userIdStr);

                var userRole = await _bookRepository.GetUserRoleAsync(userId, businessId);


                if (userRole == null || !allowedRoles.Contains(userRole.ToLower()))
                {
                    _response.StatusCode = HttpStatusCode.Forbidden;
                    _response.IsSuccess = false;
                    _response.ErrorMessages = new List<string> { "You do not have permission to view books for this business." };
                    return Forbid();
                }
                if (id == Guid.Empty)
                {
                    return BadRequest("Invalid book ID.");
                }

                var book = await _bookRepository.GetAsync(u => u.Id == id);
                if (book == null)
                {
                    return NotFound("Book not found.");
                }


                await _bookRepository.DeleteBookAsync(id);

                _response.StatusCode = HttpStatusCode.NoContent;
                _response.IsSuccess = true;
                return Ok(_response);
            }
            catch (Exception ex)
            {
                _response.IsSuccess = false;
                _response.ErrorMessages = new List<string> { ex.Message };
                return StatusCode(500, _response);
            }
        }


        [HttpPut("{id:Guid}", Name = "UpdateBook")]
        [Authorize]

        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [SwaggerOperation(Summary = "owner , partner")]

        public async Task<ActionResult<APIResponse>> UpdateBook([FromRoute] Guid id, [FromBody] UpdateBookDto updateBookDto)
        {
            try
            {

                var allowedRoles = new List<string> { "owner", "partner" };


                var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var userId = Guid.Parse(userIdStr);

                var userRole = await _bookRepository.GetUserRoleAsync(userId, updateBookDto.BusinessId);


                if (userRole == null || !allowedRoles.Contains(userRole.ToLower()))
                {
                    _response.StatusCode = HttpStatusCode.Forbidden;
                    _response.IsSuccess = false;
                    _response.ErrorMessages = new List<string> { "You do not have permission to view books for this business." };
                    return Forbid();
                }
                var existingBook = await _bookRepository.GetAsync(u => u.Id == id);
                _mapper.Map(updateBookDto, existingBook);

                if (updateBookDto == null || existingBook == null)
                {
                    return BadRequest();
                }

                await _bookRepository.UpdateAsync(existingBook);
                _response.Result = existingBook;
                _response.StatusCode = HttpStatusCode.NoContent;
                _response.IsSuccess = true;
                return Ok(_response);
            }
            catch (Exception ex)
            {
                _response.IsSuccess = false;
                _response.ErrorMessages = new List<string>() { ex.ToString() };
            }
            return _response;
        }


        [HttpPatch("{bookId:Guid}/setting")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [SwaggerOperation(Summary = "Patch book setting statuses (owner, partner)")]

        public async Task<ActionResult<APIResponse>> PatchBookSetting(Guid bookId, [FromBody] UpdateSettingDto dto)
        {
            try
            {
                var allowedRoles = new List<string> { "owner", "partner" };

                var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var userId = Guid.Parse(userIdStr);

                var book = await _bookRepository.GetAsync(b => b.Id == bookId);
                if (book == null)
                {
                    _response.StatusCode = HttpStatusCode.NotFound;
                    _response.IsSuccess = false;
                    _response.ErrorMessages = new List<string> { "Book not found." };
                    return NotFound(_response);
                }

                var userRole = await _bookRepository.GetUserRoleAsync(userId, book.BusinessId);
                if (userRole == null || !allowedRoles.Contains(userRole.ToLower()))
                {
                    _response.StatusCode = HttpStatusCode.Forbidden;
                    _response.IsSuccess = false;
                    _response.ErrorMessages = new List<string> { "You do not have permission to update settings for this book." };
                    return Forbid();
                }

                var result = await _bookRepository.UpdateSettingAsync(bookId, dto);
                if (!result)
                {
                    _response.StatusCode = HttpStatusCode.NotFound;
                    _response.IsSuccess = false;
                    _response.ErrorMessages = new List<string> { "Setting not found for this book." };
                    return NotFound(_response);
                }

                _response.StatusCode = HttpStatusCode.OK;
                _response.IsSuccess = true;
                _response.Result = "Setting updated successfully.";
                return Ok(_response);
            }
            catch (Exception ex)
            {
                _response.StatusCode = HttpStatusCode.InternalServerError;
                _response.IsSuccess = false;
                _response.ErrorMessages = new List<string> { ex.Message };
                return StatusCode(StatusCodes.Status500InternalServerError, _response);
            }
        }

    }
}
