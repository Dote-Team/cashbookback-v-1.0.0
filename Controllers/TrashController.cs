using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using cashbook.Dto;
using cashbook.Dto.Trash;
using cashbook.Helper;
using cashbook.Interfaces;
using cashbook.Models;

namespace cashbook.Controllers;

[Route("API/[Controller]")]
[ApiController]
public class TrashController : ControllerBase
{
	protected APIResponse _response;

	private readonly ITrashRepository _trashRepository;

	public TrashController(ITrashRepository trashRepository)
	{
		_trashRepository = trashRepository;
		_response = new APIResponse();
	}

	[HttpGet]
	[Authorize]
	[ProducesResponseType(200)]
	[ProducesResponseType(401)]
	[ProducesResponseType(500)]
	public async Task<ActionResult<APIResponse>> GetTrash([FromQuery] Guid? businessId = null, [FromQuery] int? skip = 1, [FromQuery] int? take = 25, [FromQuery] string? search = null)
	{
		try
		{
			Guid? callerId = User.GetUserId();
			if (!callerId.HasValue)
			{
				_response.IsSuccess = false;
				_response.StatusCode = HttpStatusCode.Unauthorized;
				_response.ErrorMessages = new List<string> { "Invalid token." };
				return Unauthorized(_response);
			}
			PaginatedResponse<TrashItemDto> result = await _trashRepository.GetTrashAsync(businessId, callerId.Value, skip, take, search);
			_response.Result = result;
			_response.StatusCode = HttpStatusCode.OK;
			_response.IsSuccess = true;
			return Ok(_response);
		}
		catch (Exception ex)
		{
			_response.IsSuccess = false;
			_response.StatusCode = HttpStatusCode.InternalServerError;
			_response.ErrorMessages = new List<string> { ex.Message };
			return StatusCode(500, _response);
		}
	}

	[HttpPost("{id:Guid}/restore")]
	[Authorize]
	[ProducesResponseType(200)]
	[ProducesResponseType(401)]
	[ProducesResponseType(403)]
	[ProducesResponseType(404)]
	[ProducesResponseType(409)]
	[ProducesResponseType(500)]
	public async Task<ActionResult<APIResponse>> Restore(Guid id)
	{
		try
		{
			Guid? callerId = User.GetUserId();
			if (!callerId.HasValue)
			{
				_response.IsSuccess = false;
				_response.StatusCode = HttpStatusCode.Unauthorized;
				_response.ErrorMessages = new List<string> { "Invalid token." };
				return Unauthorized(_response);
			}
			TrashItem? item = await _trashRepository.GetTrashItemAsync(id);
			if (item == null)
			{
				_response.IsSuccess = false;
				_response.StatusCode = HttpStatusCode.NotFound;
				_response.ErrorMessages = new List<string> { "العنصر غير موجود في سلة المهملات." };
				return NotFound(_response);
			}
			if (item.DeletedByUserId != callerId.Value)
			{
				_response.IsSuccess = false;
				_response.StatusCode = HttpStatusCode.Forbidden;
				_response.ErrorMessages = new List<string> { "لا تملك صلاحية استعادة هذا العنصر." };
				return StatusCode(403, _response);
			}
			if (!(await _trashRepository.RestoreAsync(id)))
			{
				_response.IsSuccess = false;
				_response.StatusCode = HttpStatusCode.Conflict;
				_response.ErrorMessages = new List<string> { "تعذّرت الاستعادة لتعارض في البيانات (مثل ارتباط العنصر ببيانات حُذفت نهائياً)." };
				return StatusCode(StatusCodes.Status409Conflict, _response);
			}
			_response.StatusCode = HttpStatusCode.NoContent;
			_response.IsSuccess = true;
			return Ok(_response);
		}
		catch (Exception ex)
		{
			_response.IsSuccess = false;
			_response.StatusCode = HttpStatusCode.InternalServerError;
			_response.ErrorMessages = new List<string> { ex.Message };
			return StatusCode(500, _response);
		}
	}

	[HttpDelete("{id:Guid}")]
	[Authorize]
	[ProducesResponseType(200)]
	[ProducesResponseType(401)]
	[ProducesResponseType(403)]
	[ProducesResponseType(404)]
	[ProducesResponseType(500)]
	public async Task<ActionResult<APIResponse>> HardDelete(Guid id)
	{
		try
		{
			Guid? callerId = User.GetUserId();
			if (!callerId.HasValue)
			{
				_response.IsSuccess = false;
				_response.StatusCode = HttpStatusCode.Unauthorized;
				_response.ErrorMessages = new List<string> { "Invalid token." };
				return Unauthorized(_response);
			}
			TrashItem? item = await _trashRepository.GetTrashItemAsync(id);
			if (item == null)
			{
				_response.IsSuccess = false;
				_response.StatusCode = HttpStatusCode.NotFound;
				_response.ErrorMessages = new List<string> { "العنصر غير موجود في سلة المهملات." };
				return NotFound(_response);
			}
			if (item.DeletedByUserId != callerId.Value)
			{
				_response.IsSuccess = false;
				_response.StatusCode = HttpStatusCode.Forbidden;
				_response.ErrorMessages = new List<string> { "لا تملك صلاحية حذف هذا العنصر نهائياً." };
				return StatusCode(403, _response);
			}
			if (!(await _trashRepository.HardDeleteAsync(id)))
			{
				_response.IsSuccess = false;
				_response.StatusCode = HttpStatusCode.InternalServerError;
				_response.ErrorMessages = new List<string> { "تعذّر الحذف النهائي للعنصر." };
				return StatusCode(500, _response);
			}
			_response.StatusCode = HttpStatusCode.NoContent;
			_response.IsSuccess = true;
			return Ok(_response);
		}
		catch (Exception ex)
		{
			_response.IsSuccess = false;
			_response.StatusCode = HttpStatusCode.InternalServerError;
			_response.ErrorMessages = new List<string> { ex.Message };
			return StatusCode(500, _response);
		}
	}
}
