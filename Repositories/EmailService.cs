using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MimeKit;
using cashbook.Data;
using cashbook.Dto.user;
using cashbook.Helper;
using cashbook.Interfaces;
using cashbook.Models;

namespace cashbook.Repositories;

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
		MimeMessage message = new MimeMessage
		{
			From = { (InternetAddress)new MailboxAddress(_settings.FromName, _settings.FromEmail) },
			To = { (InternetAddress)MailboxAddress.Parse(recipientEmail) },
			Subject = "You're Invited to Join Our Business Platform"
		};
		BodyBuilder builder = new BodyBuilder
		{
			HtmlBody = $"\n                <p>Hello,</p>\n                <p>You’ve been invited to join our platform. Click the link below to accept the invitation:</p>\n                <p><a href=\"{invitationLink}\">{invitationLink}</a></p>\n                <p>If you were not expecting this, you can ignore this email.</p>"
		};
		message.Body = builder.ToMessageBody();
		using SmtpClient smtp = new SmtpClient();
		await smtp.ConnectAsync(_settings.SmtpServer, _settings.SmtpPort, SecureSocketOptions.None);
		await smtp.SendAsync(message);
		await smtp.DisconnectAsync(quit: true);
	}

	public async Task<bool> SendInvaiteByEmail(InviteDto inviteDto)
	{
		User user = await _context.Users.FirstOrDefaultAsync((User u) => u.Email.ToLower() == inviteDto.Email.ToLower());
		if (user == null)
		{
			throw new InvalidOperationException("المستخدم ذو البريد الإلكتروني '" + inviteDto.Email + "' غير موجود.");
		}
		if (await _context.BusinessUsers.AnyAsync((BusinessUser bu) => bu.BusinessId == inviteDto.BusinessId && bu.UserId == user.Id && bu.Role == inviteDto.Role))
		{
			throw new InvalidOperationException("User is already associated with this business.");
		}
		BusinessUser businessUser = new BusinessUser
		{
			UserId = user.Id,
			BusinessId = inviteDto.BusinessId,
			Role = inviteDto.Role
		};
		if (inviteDto.BookIds != null && Roles.IsBookScoped(inviteDto.Role))
		{
			List<Guid> validBooks = await (from b in _context.Books
				where inviteDto.BookIds.Contains(b.Id) && b.BusinessId == inviteDto.BusinessId
				select b.Id).ToListAsync();
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
