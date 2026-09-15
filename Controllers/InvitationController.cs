using AutoMapper;
using cashbook.Models;
using cashbook.Dto;
using cashbook.Interfaces;
using cashbook.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Linq.Expressions;
using System.Net;
using System.Security.Claims;
using cashbook.Dto.user;
using Azure;
using cashbook.Dto.book;
using cashbook.Repositories;
using Swashbuckle.AspNetCore.Annotations;

namespace cashbook.Controllers
{
    [ApiController]
    [Route("API/invitations")]
    public class InvitationController : ControllerBase
    {
        private readonly IEmailService _emailService;
        private readonly IConfiguration _config;
        protected APIResponse _response;


        public InvitationController(IEmailService emailService, IConfiguration config)
        {
            _emailService = emailService;
            _response = new();
            _config = config;
        }

        //[HttpPost("send")]
        //public async Task<IActionResult> SendInvitation([FromBody] SendInvitationDto dto)
        //{
        //    if (string.IsNullOrWhiteSpace(dto.RecipientEmail))
        //        return BadRequest("Recipient email is required.");

        //    // Example: generate a unique token and link
        //    var token = Guid.NewGuid().ToString();
        //    var invitationLink = $"{_config["App:BaseUrl"]}/accept-invitation?email={dto.RecipientEmail}&token={token}";

        //    // TODO: Save the token in your database with expiration, linked to the email

        //    await _emailService.SendInvitationEmailAsync(dto.RecipientEmail, invitationLink);
        //    return Ok(new { message = "Invitation sent." });
        //}


        [HttpPost]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]

        public async Task<ActionResult<APIResponse>> InviteMember([FromBody] InviteDto inviteDto)
        {
            try
            {



                if (inviteDto == null)
                {
                    return BadRequest(inviteDto);
                }

                var success = await _emailService.SendInvaiteByEmail(inviteDto);
                if (!success)
                {
                    _response.StatusCode = HttpStatusCode.InternalServerError;
                    _response.IsSuccess = false;
                    _response.ErrorMessages = new List<string> { "An error occurred while adding the member." };
                    return StatusCode(StatusCodes.Status500InternalServerError, _response);
                }
                _response.StatusCode = HttpStatusCode.OK;
                _response.IsSuccess = true;
                _response.Result = new { message = "member added successfully." };
            }
            catch (Exception ex)
            {
                _response.IsSuccess = false;
                _response.StatusCode = HttpStatusCode.InternalServerError;
                _response.ErrorMessages = new List<string> { ex.Message };
                return StatusCode(StatusCodes.Status500InternalServerError, _response);
            }
            return _response;
        }



    }

}
