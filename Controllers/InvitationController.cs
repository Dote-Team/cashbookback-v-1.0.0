using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using cashbook.Data;
using cashbook.Dto;
using cashbook.Dto.user;
using cashbook.Helper;
using cashbook.Interfaces;
using cashbook.Models;

namespace cashbook.Controllers;

[ApiController]
[Route("API/invitations")]
public class InvitationController : ControllerBase
{
    private readonly IEmailService _emailService;

    private readonly IConfiguration _config;

    private readonly ApplicationDbContext _context;

    protected APIResponse _response;

    public InvitationController(IEmailService emailService, IConfiguration config, ApplicationDbContext context)
    {
        _emailService = emailService;
        _response = new APIResponse();
        _config = config;
        _context = context;
    }

    [HttpPost]
    [Authorize]
    [ProducesResponseType(403)]
    [ProducesResponseType(401)]
    [ProducesResponseType(201)]
    [ProducesResponseType(500)]
    [ProducesResponseType(400)]
    public async Task<ActionResult<APIResponse>> InviteMember([FromBody] InviteDto inviteDto)
    {
        try
        {
            if (inviteDto == null)
            {
                return BadRequest(inviteDto);
            }
            Guid? callerId = base.User.GetUserId();
            if (!callerId.HasValue)
            {
                _response.StatusCode = HttpStatusCode.Unauthorized;
                _response.IsSuccess = false;
                _response.ErrorMessages = new List<string> { "User identity could not be determined." };
                return Unauthorized(_response);
            }
            if (string.IsNullOrWhiteSpace(inviteDto.Role) || !Enumerable.Contains(Roles.Assignable, inviteDto.Role.Trim().ToLowerInvariant()))
            {
                _response.StatusCode = HttpStatusCode.BadRequest;
                _response.IsSuccess = false;
                _response.ErrorMessages = new List<string> { "Invalid role." };
                return BadRequest(_response);
            }
            if (!(await _context.Businesses.AnyAsync((Business b) => b.Id == inviteDto.BusinessId)))
            {
                _response.StatusCode = HttpStatusCode.NotFound;
                _response.IsSuccess = false;
                _response.ErrorMessages = new List<string> { "Business not found." };
                return NotFound(_response);
            }
            string callerRole = await (from bu in _context.BusinessUsers
                                       where bu.UserId == ((Guid?)callerId).Value && bu.BusinessId == inviteDto.BusinessId
                                       select bu.Role).FirstOrDefaultAsync();
            if (callerRole == null || !Enumerable.Contains(Roles.Management, callerRole.ToLowerInvariant()))
            {
                _response.StatusCode = HttpStatusCode.Forbidden;
                _response.IsSuccess = false;
                _response.ErrorMessages = new List<string> { "You do not have permission to add members to this business." };
                return StatusCode(403, _response);
            }
            if (string.Equals(inviteDto.Role.Trim(), "owner", StringComparison.OrdinalIgnoreCase) && !string.Equals(callerRole, "owner", StringComparison.OrdinalIgnoreCase))
            {
                _response.StatusCode = HttpStatusCode.Forbidden;
                _response.IsSuccess = false;
                _response.ErrorMessages = new List<string> { "Only the business owner can assign the owner role." };
                return StatusCode(403, _response);
            }
            inviteDto.Role = inviteDto.Role.Trim().ToLowerInvariant();

            // الدعوة تُرسل إلى بريد مسجَّل؛ فabsence الحساب حالة عميل (404) لا خلل خادم (500).
            // كان الخطأ يُرمى من خدمة البريد ويُلتقط فيُعاد برمز 500، وهذا يضلل الواجهة والمراقبة.
            var emailToInvite = inviteDto.Email?.Trim();
            if (string.IsNullOrWhiteSpace(emailToInvite))
            {
                _response.StatusCode = HttpStatusCode.BadRequest;
                _response.IsSuccess = false;
                _response.ErrorMessages = new List<string> { "البريد الإلكتروني مطلوب." };
                return BadRequest(_response);
            }
            bool targetUserExists = await _context.Users.AnyAsync((User u) => u.Email.ToLower() == emailToInvite.ToLower());
            if (!targetUserExists)
            {
                _response.StatusCode = HttpStatusCode.NotFound;
                _response.IsSuccess = false;
                _response.ErrorMessages = new List<string>
                {
                    $"المستخدم ذو البريد الإلكتروني '{emailToInvite}' غير موجود."
                };
                return NotFound(_response);
            }

            if (!(await _emailService.SendInvaiteByEmail(inviteDto)))
            {
                _response.StatusCode = HttpStatusCode.InternalServerError;
                _response.IsSuccess = false;
                _response.ErrorMessages = new List<string> { "An error occurred while adding the member." };
                return StatusCode(500, _response);
            }
            _response.StatusCode = HttpStatusCode.OK;
            _response.IsSuccess = true;
            _response.Result = new
            {
                message = "member added successfully."
            };
        }
        catch (Exception ex)
        {
            Exception ex2 = ex;
            _response.IsSuccess = false;
            _response.StatusCode = HttpStatusCode.InternalServerError;
            _response.ErrorMessages = new List<string> { ex2.Message };
            return StatusCode(500, _response);
        }
        return _response;
    }
}
