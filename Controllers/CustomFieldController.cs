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
using cashbook.Data;
using cashbook.Dto;
using cashbook.Dto.customfield;
using cashbook.Helper;
using cashbook.Interfaces;
using cashbook.Models;

namespace cashbook.Controllers;

[Route("API/[Controller]")]
[ApiController]
public class CustomFieldController : ControllerBase
{
	protected APIResponse _response;

	private readonly IMapper _mapper;

	private readonly ICustomFieldRepository _customFieldRepository;

	private readonly ApplicationDbContext _context;

	public CustomFieldController(ICustomFieldRepository customFieldRepository, IMapper mapper, ApplicationDbContext context)
	{
		_customFieldRepository = customFieldRepository;
		_response = new APIResponse();
		_mapper = mapper;
		_context = context;
	}

	/// <summary>نتيجة فحص صلاحية مستخدم على خزنة محددة.</summary>
	private enum FieldAccess
	{
		Granted,
		WalletNotFound,
		Forbidden
	}

	/// <summary>
	/// يفحص صلاحية المستدعي على خزنة بعينها، ويستخرج المنشأة من الخزنة نفسها.
	///
	/// كانت العبارة السابقة تقرأ دور المستخدم بلا تحديد منشأة إطلاقاً:
	/// <c>where bu.UserId == userId</c> ثم <c>FirstOrDefaultAsync</c>، فتأخذ دوراً
	/// غير محدَّد من بين كل عضوياته. والنتيجة أن شريكاً في منشأة وقارئاً في أخرى
	/// يُرفض بـ403 في منشأته هو، حسب ترتيب الصفوف لا حسب صلاحيته. وهذا هو سبب
	/// رسالة «ليس لديك الصلاحية» التي كانت تظهر لمستخدم يملك الصلاحية فعلاً.
	///
	/// ويُفحص نطاق الخزنة (BookIds) للأدوار المقيَّدة، كما يُفحص في القراءة.
	/// </summary>
	private async Task<FieldAccess> CheckWalletAccessAsync(Guid userId, Guid walletId, string[] allowedRoles)
	{
		if (walletId == Guid.Empty)
		{
			return FieldAccess.WalletNotFound;
		}
		Book wallet = await _context.Books.AsNoTracking().FirstOrDefaultAsync((Book b) => b.Id == walletId);
		if (wallet == null)
		{
			return FieldAccess.WalletNotFound;
		}
		string role = await (from bu in _context.BusinessUsers
							 where bu.UserId == userId && bu.BusinessId == wallet.BusinessId
							 select bu.Role.ToLower()).FirstOrDefaultAsync();
		if (role == null || !Enumerable.Contains(allowedRoles, role))
		{
			return FieldAccess.Forbidden;
		}
		if (Roles.IsBookScoped(role))
		{
			List<Guid> allowedBooks = await (from bu in _context.BusinessUsers
											 where bu.UserId == userId && bu.BusinessId == wallet.BusinessId
											 select bu.BookIds).FirstOrDefaultAsync();
			if (allowedBooks == null || !allowedBooks.Contains(walletId))
			{
				return FieldAccess.Forbidden;
			}
		}
		return FieldAccess.Granted;
	}

	private ActionResult<APIResponse> FieldDenied(FieldAccess access, string message)
	{
		_response.IsSuccess = false;
		_response.StatusCode = ((access == FieldAccess.WalletNotFound) ? HttpStatusCode.NotFound : HttpStatusCode.Forbidden);
		_response.ErrorMessages = new List<string> { message };
		return StatusCode((int)_response.StatusCode, _response);
	}

	[HttpGet]
	[Authorize]
	[ProducesResponseType(403)]
	[ProducesResponseType(401)]
	[ProducesResponseType(200)]
	[SwaggerOperation(null, null, Summary = "all,except viewer")]
	public async Task<ActionResult<APIResponse>> GetPaginatedCustomField([FromQuery] Guid bookId, [FromQuery] int? skip = 1, [FromQuery] int? take = 25, [FromQuery] string? search = null)
	{
		try
		{
			Guid? callerId = base.User.GetUserId();
			if (!callerId.HasValue)
			{
				_response.StatusCode = HttpStatusCode.Unauthorized;
				_response.IsSuccess = false;
				_response.ErrorMessages = new List<string> { "Invalid token." };
				return Unauthorized(_response);
			}
			// من يقرأ حركات الخزنة يقرأ حقولها: قيم الحقول تُعاد داخل الحركة أصلاً،
			// فيتعذّر إرجاع القيمة ومنع تعريفها.
			FieldAccess access = await CheckWalletAccessAsync(callerId.Value, bookId, Roles.TransactionRead);
			if (access != FieldAccess.Granted)
			{
				return FieldDenied(access, "لا تملك صلاحية الوصول إلى حقول هذه الخزنة.");
			}
			PaginatedResponse<CustomFieldDto> paginatedCustomField = await _customFieldRepository.GetcustomFieldAsync(bookId, skip, take, search);
			PaginatedResponse<CustomFieldDto> paginatedResponse = new PaginatedResponse<CustomFieldDto>
			{
				Data = paginatedCustomField.Data,
				TotalRecords = paginatedCustomField.TotalRecords,
				Skip = paginatedCustomField.Skip,
				Take = paginatedCustomField.Take
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

	[HttpGet("{id:Guid}", Name = "GetCustomField")]
	[Authorize]
	[ProducesResponseType(403)]
	[ProducesResponseType(401)]
	[ProducesResponseType(200)]
	[ProducesResponseType(404)]
	[ProducesResponseType(400)]
	public async Task<ActionResult<APIResponse>> GetCustomField(Guid id)
	{
		try
		{
			Guid? callerId = base.User.GetUserId();
			if (!callerId.HasValue)
			{
				_response.StatusCode = HttpStatusCode.Unauthorized;
				_response.IsSuccess = false;
				_response.ErrorMessages = new List<string> { "Invalid token." };
				return Unauthorized(_response);
			}
			CustomField customField = await _customFieldRepository.GetAsync((CustomField u) => u.Id == id);
			if (customField == null)
			{
				_response.StatusCode = HttpStatusCode.NotFound;
				return NotFound(_response);
			}
			FieldAccess access = await CheckWalletAccessAsync(callerId.Value, customField.BookId, Roles.TransactionRead);
			if (access != FieldAccess.Granted)
			{
				return FieldDenied(access, "لا تملك صلاحية الوصول إلى هذا الحقل.");
			}
			_response.Result = customField;
			_response.StatusCode = HttpStatusCode.OK;
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

	[HttpPost]
	[Authorize]
	[ProducesResponseType(403)]
	[ProducesResponseType(401)]
	[ProducesResponseType(201)]
	[ProducesResponseType(500)]
	[ProducesResponseType(400)]
	public async Task<ActionResult<APIResponse>> CreateCustomField([FromBody] CreateCustomFieldDto createCustomFieldDto)
	{
		try
		{
			if (createCustomFieldDto == null)
			{
				return BadRequest(createCustomFieldDto);
			}
			Guid? callerId = base.User.GetUserId();
			if (!callerId.HasValue)
			{
				_response.StatusCode = HttpStatusCode.Unauthorized;
				_response.IsSuccess = false;
				_response.ErrorMessages = new List<string> { "Invalid token." };
				return Unauthorized(_response);
			}
			FieldAccess access = await CheckWalletAccessAsync(callerId.Value, createCustomFieldDto.BookId, Roles.Writers);
			if (access != FieldAccess.Granted)
			{
				return FieldDenied(access, "لا تملك صلاحية إضافة حقل في هذه الخزنة.");
			}
			CustomField customField = _mapper.Map<CustomField>(createCustomFieldDto);
			await _customFieldRepository.CreateAsync(customField);
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

	[HttpDelete("{id:Guid}", Name = "DeleteCustomField")]
	[Authorize]
	[ProducesResponseType(204)]
	[ProducesResponseType(404)]
	[ProducesResponseType(403)]
	[ProducesResponseType(401)]
	[ProducesResponseType(400)]
	public async Task<ActionResult<APIResponse>> DeleteCustomField(Guid id)
	{
		try
		{
			if (id == Guid.Empty)
			{
				return BadRequest("Invalid customField ID.");
			}
			Guid? callerId = base.User.GetUserId();
			if (!callerId.HasValue)
			{
				_response.StatusCode = HttpStatusCode.Unauthorized;
				_response.IsSuccess = false;
				_response.ErrorMessages = new List<string> { "Invalid token." };
				return Unauthorized(_response);
			}
			CustomField customField = await _customFieldRepository.GetAsync((CustomField u) => u.Id == id);
			if (customField == null)
			{
				return NotFound("CustomField not found.");
			}
			FieldAccess access = await CheckWalletAccessAsync(callerId.Value, customField.BookId, Roles.Writers);
			if (access != FieldAccess.Granted)
			{
				return FieldDenied(access, "لا تملك صلاحية حذف حقل من هذه الخزنة.");
			}
			await _customFieldRepository.RemoveAsync(customField);
			_response.StatusCode = HttpStatusCode.NoContent;
			_response.IsSuccess = true;
			return Ok(_response);
		}
		catch (Exception ex)
		{
			Exception ex2 = ex;
			_response.IsSuccess = false;
			_response.ErrorMessages = new List<string> { ex2.Message };
			return StatusCode(500, _response);
		}
	}

	[HttpPut("{id:Guid}", Name = "UpdateCustomField")]
	[Authorize]
	[ProducesResponseType(204)]
	[ProducesResponseType(200)]
	[ProducesResponseType(400)]
	public async Task<ActionResult<APIResponse>> UpdateCustomField([FromRoute] Guid id, [FromBody] UpdateCustomFieldDto updateCustomFieldDto)
	{
		try
		{
			Guid? callerId = base.User.GetUserId();
			if (!callerId.HasValue)
			{
				_response.StatusCode = HttpStatusCode.Unauthorized;
				_response.IsSuccess = false;
				_response.ErrorMessages = new List<string> { "Invalid token." };
				return Unauthorized(_response);
			}
			if (updateCustomFieldDto == null)
			{
				return BadRequest();
			}
			CustomField existingCustomField = await _customFieldRepository.GetAsync((CustomField u) => u.Id == id);
			if (existingCustomField == null)
			{
				return NotFound("CustomField not found.");
			}
			FieldAccess access = await CheckWalletAccessAsync(callerId.Value, existingCustomField.BookId, Roles.Writers);
			if (access != FieldAccess.Granted)
			{
				return FieldDenied(access, "لا تملك صلاحية تعديل حقل في هذه الخزنة.");
			}
			_mapper.Map(updateCustomFieldDto, existingCustomField);
			await _customFieldRepository.UpdateAsync(existingCustomField);
			_response.Result = existingCustomField;
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
}
