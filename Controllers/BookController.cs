using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Swashbuckle.AspNetCore.Annotations;
using cashbook.Dto;
using cashbook.Dto.Book;
using cashbook.Dto.book;
using cashbook.Dto.setting;
using cashbook.Helper;
using cashbook.Interfaces;
using cashbook.Models;

namespace cashbook.Controllers;

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
        _response = new APIResponse();
        _mapper = mapper;
    }

    [HttpGet]
    [Authorize]
    [ProducesResponseType(403)]
    [ProducesResponseType(401)]
    [ProducesResponseType(200)]
    [SwaggerOperation(null, null, Summary = "Get paginated books with sorting")]
    public async Task<ActionResult<APIResponse>> GetPaginatedBooks([FromQuery] Guid businessId, [FromQuery] int? skip = 1, [FromQuery] int? take = 25, [FromQuery] string? search = null, [FromQuery] SortField? sortBy = null, [FromQuery] SortDirection? sortDirection = SortDirection.desc)
    {
        try
        {
            string[] allowedRoles = Roles.AnyMember;
            string userIdStr = base.User.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")?.Value;
            Guid userId = Guid.Parse(userIdStr);
            string userRole = await _bookRepository.GetUserRoleAsync(userId, businessId);
            if (userRole == null || !Enumerable.Contains(allowedRoles, userRole.ToLower()))
            {
                _response.StatusCode = HttpStatusCode.Forbidden;
                _response.IsSuccess = false;
                _response.ErrorMessages = new List<string> { "You do not have permission to view books for this business." };
                return Forbid();
            }
            PaginatedResponse<BookDto> paginatedBooks = await _bookRepository.GetBooksAsync(businessId, userId, skip, take, search, sortBy, sortDirection);
            PaginatedResponse<BookDto> paginatedResponse = new PaginatedResponse<BookDto>
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
        catch (UnauthorizedAccessException ex)
        {
            UnauthorizedAccessException uaex = ex;
            _response.IsSuccess = false;
            _response.StatusCode = HttpStatusCode.Forbidden;
            _response.ErrorMessages = new List<string> { uaex.Message };
            return StatusCode(403, _response);
        }
        catch (Exception ex2)
        {
            Exception ex3 = ex2;
            _response.IsSuccess = false;
            _response.ErrorMessages = new List<string> { ex3.ToString() };
            return StatusCode(500, _response);
        }
    }

    [HttpGet("{id:Guid}", Name = "GetBook")]
    [Authorize]
    [ProducesResponseType(403)]
    [ProducesResponseType(401)]
    [ProducesResponseType(200)]
    [ProducesResponseType(404)]
    [ProducesResponseType(400)]
    [SwaggerOperation(null, null, Summary = "all")]
    public async Task<ActionResult<APIResponse>> GetBook(Guid id)
    {
        try
        {
            string userIdStr = base.User.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")?.Value;
            Guid userId = Guid.Parse(userIdStr);
            if (id == Guid.Empty)
            {
                _response.StatusCode = HttpStatusCode.BadRequest;
                _response.IsSuccess = false;
                _response.ErrorMessages = new List<string> { "Invalid book ID." };
                return BadRequest(_response);
            }
            BookDetailsDto bookDto = await _bookRepository.GetBookDetailsByIdAsync(id, userId);
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
            Exception ex2 = ex;
            _response.IsSuccess = false;
            _response.ErrorMessages = new List<string> { ex2.Message };
            _response.StatusCode = HttpStatusCode.InternalServerError;
            return StatusCode(500, _response);
        }
    }

    [HttpPost]
    [Authorize]
    [ProducesResponseType(403)]
    [ProducesResponseType(401)]
    [ProducesResponseType(201)]
    [ProducesResponseType(500)]
    [ProducesResponseType(400)]
    [SwaggerOperation(null, null, Summary = "owner , partner")]
    public async Task<ActionResult<APIResponse>> CreateBook([FromBody] CreateBookDto createBookDto)
    {
        try
        {
            string[] allowedRoles = Roles.Management;
            string userIdStr = base.User.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")?.Value;
            Guid userId = Guid.Parse(userIdStr);
            string userRole = await _bookRepository.GetUserRoleAsync(userId, createBookDto.BusinessId);
            if (userRole == null || !Enumerable.Contains(allowedRoles, userRole.ToLower()))
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
            Exception ex2 = ex;
            _response.IsSuccess = false;
            _response.ErrorMessages = new List<string> { ex2.ToString() };
        }
        return _response;
    }

    [HttpDelete("{id:Guid}", Name = "DeleteBook")]
    [Authorize]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    [ProducesResponseType(403)]
    [ProducesResponseType(401)]
    [ProducesResponseType(400)]
    [SwaggerOperation(null, null, Summary = "owner , partner")]
    public async Task<ActionResult<APIResponse>> DeleteBook(Guid id, Guid businessId)
    {
        try
        {
            string[] allowedRoles = Roles.Management;
            string userIdStr = base.User.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")?.Value;
            Guid userId = Guid.Parse(userIdStr);
            string userRole = await _bookRepository.GetUserRoleAsync(userId, businessId);
            if (userRole == null || !Enumerable.Contains(allowedRoles, userRole.ToLower()))
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
            if (await _bookRepository.GetAsync((Book u) => u.Id == id) == null)
            {
                return NotFound("Book not found.");
            }
            await _bookRepository.DeleteBookAsync(id);
            _response.StatusCode = HttpStatusCode.NoContent;
            _response.IsSuccess = true;
            return Ok(_response);
        }
        catch (DbUpdateException)
        {
            // حذف خزنة ترتبط بها حركات أو أسعار صرف يخالف قيود المفاتيح الأجنبية.
            // كان الخطأ يخرج برمز 500 مع رسالة Entity Framework الخام بالإنجليزية،
            // وهذا يستوجب تعارضاً (409) لا عطلاً، ورسالة يفهما المستخدم.
            _response.IsSuccess = false;
            _response.StatusCode = HttpStatusCode.Conflict;
            _response.ErrorMessages = new List<string>
            {
                "لا يمكن حذف الخزنة لارتباطها ببيانات أخرى: انقل أو احذف حركاتها وأسعار صرفها أولاً."
            };
            return StatusCode(StatusCodes.Status409Conflict, _response);
        }
        catch (Exception ex)
        {
            Exception ex2 = ex;
            _response.IsSuccess = false;
            _response.ErrorMessages = new List<string> { ex2.Message };
            return StatusCode(500, _response);
        }
    }

    [HttpPut("{id:Guid}", Name = "UpdateBook")]
    [Authorize]
    [ProducesResponseType(204)]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [SwaggerOperation(null, null, Summary = "owner , partner")]
    public async Task<ActionResult<APIResponse>> UpdateBook([FromRoute] Guid id, [FromBody] UpdateBookDto updateBookDto)
    {
        try
        {
            string[] allowedRoles = Roles.Management;
            string userIdStr = base.User.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")?.Value;
            Guid userId = Guid.Parse(userIdStr);
            string userRole = await _bookRepository.GetUserRoleAsync(userId, updateBookDto.BusinessId);
            if (userRole == null || !Enumerable.Contains(allowedRoles, userRole.ToLower()))
            {
                _response.StatusCode = HttpStatusCode.Forbidden;
                _response.IsSuccess = false;
                _response.ErrorMessages = new List<string> { "You do not have permission to view books for this business." };
                return Forbid();
            }
            Book existingBook = await _bookRepository.GetAsync((Book u) => u.Id == id);
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
            Exception ex2 = ex;
            _response.IsSuccess = false;
            _response.ErrorMessages = new List<string> { ex2.ToString() };
        }
        return _response;
    }

    [HttpPatch("{bookId:Guid}/setting")]
    [Authorize]
    [ProducesResponseType(200)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    [ProducesResponseType(400)]
    [SwaggerOperation(null, null, Summary = "Patch book setting statuses (owner, partner)")]
    public async Task<ActionResult<APIResponse>> PatchBookSetting(Guid bookId, [FromBody] UpdateSettingDto dto)
    {
        try
        {
            string[] allowedRoles = Roles.Management;
            string userIdStr = base.User.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")?.Value;
            Guid userId = Guid.Parse(userIdStr);
            Book book = await _bookRepository.GetAsync((Book b) => b.Id == bookId);
            if (book == null)
            {
                _response.StatusCode = HttpStatusCode.NotFound;
                _response.IsSuccess = false;
                _response.ErrorMessages = new List<string> { "Book not found." };
                return NotFound(_response);
            }
            string userRole = await _bookRepository.GetUserRoleAsync(userId, book.BusinessId);
            if (userRole == null || !Enumerable.Contains(allowedRoles, userRole.ToLower()))
            {
                _response.StatusCode = HttpStatusCode.Forbidden;
                _response.IsSuccess = false;
                _response.ErrorMessages = new List<string> { "You do not have permission to update settings for this book." };
                return Forbid();
            }
            if (!(await _bookRepository.UpdateSettingAsync(bookId, dto)))
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
            Exception ex2 = ex;
            _response.StatusCode = HttpStatusCode.InternalServerError;
            _response.IsSuccess = false;
            _response.ErrorMessages = new List<string> { ex2.Message };
            return StatusCode(500, _response);
        }
    }
}
