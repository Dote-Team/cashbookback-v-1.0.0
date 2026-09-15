using cashbook.Dto.user;
using cashbook.Repositories;

namespace cashbook.Interfaces
{
    public interface IEmailService 
    {
        Task SendInvitationEmailAsync(string recipientEmail, string invitationLink);
        Task <bool>SendInvaiteByEmail(InviteDto inviteDto);
    }
}
