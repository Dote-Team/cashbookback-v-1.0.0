using System.Threading.Tasks;
using cashbook.Dto.user;

namespace cashbook.Interfaces;

public interface IEmailService
{
	Task SendInvitationEmailAsync(string recipientEmail, string invitationLink);

	Task<bool> SendInvaiteByEmail(InviteDto inviteDto);
}
