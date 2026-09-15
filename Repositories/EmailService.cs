namespace cashbook.Repositories;

using cashbook.Data;
using cashbook.Dto.user;
using cashbook.Interfaces;

using cashbook.Models;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MimeKit;

public class EmailService : IEmailService
{
    private readonly MailSettings _settings;
    private readonly ApplicationDbContext _context;


    public EmailService(IOptions<MailSettings> settings, ApplicationDbContext context)
    {
        _settings = settings.Value;
        _context = context;

    }

    public async Task SendInvitationEmailAsync(string recipientEmail, string invitationLink)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_settings.FromName, _settings.FromEmail));
        message.To.Add(MailboxAddress.Parse(recipientEmail));
        message.Subject = "You're Invited to Join Our Business Platform";

        var builder = new BodyBuilder
        {
            HtmlBody = $@"
                <p>Hello,</p>
                <p>You’ve been invited to join our platform. Click the link below to accept the invitation:</p>
                <p><a href=""{invitationLink}"">{invitationLink}</a></p>
                <p>If you were not expecting this, you can ignore this email.</p>"
        };

        message.Body = builder.ToMessageBody();

        using var smtp = new SmtpClient();
        //await smtp.ConnectAsync(_settings.SmtpServer, _settings.SmtpPort, _settings.UseSsl);
        await smtp.ConnectAsync(_settings.SmtpServer, _settings.SmtpPort, SecureSocketOptions.None);




        //await smtp.AuthenticateAsync(_settings.Email, _settings.Password);
        await smtp.SendAsync(message);
        await smtp.DisconnectAsync(true);
    }

    public async Task<bool> SendInvaiteByEmail(InviteDto inviteDto)
    {
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Email.ToLower() == inviteDto.Email.ToLower());

        if (user == null)
        {
            throw new InvalidOperationException($"المستخدم ذو البريد الإلكتروني '{inviteDto.Email}' غير موجود.");
        }

        // Check if user is already associated with this business
        var alreadyExists = await _context.BusinessUsers
            .AnyAsync(bu => bu.BusinessId == inviteDto.BusinessId && bu.UserId == user.Id && bu.Role == inviteDto.Role);

        if (alreadyExists)
        {
            throw new InvalidOperationException("User is already associated with this business.");
        }

        // Add the user to the business
        var businessUser = new BusinessUser
        {
            UserId = user.Id,
            BusinessId = inviteDto.BusinessId,
            Role = inviteDto.Role,

        };

        var allowedRoles = new[] { "staff", "admin", "dataoperator", "privateviewer" };
        if (inviteDto.BookIds != null && allowedRoles.Contains(inviteDto.Role.Trim().ToLower()))
        {
            var validBooks = await _context.Books
                .Where(b => inviteDto.BookIds.Contains(b.Id) && b.BusinessId == inviteDto.BusinessId)
                .Select(b => b.Id)
                .ToListAsync();

            if (validBooks.Count != inviteDto.BookIds.Count)
            {
                throw new InvalidOperationException("One or more BookIds are invalid or do not belong to the business.");
            }

            businessUser.BookIds = validBooks;
        }

        _context.BusinessUsers.Add(businessUser);
        await _context.SaveChangesAsync();
        return true;

    }


}
