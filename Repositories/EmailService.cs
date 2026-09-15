using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
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

    public async Task<EmailDeliveryResult> SendMembershipNoticeAsync(InviteDto inviteDto, string businessName)
    {
        if (!_settings.Enabled)
        {
            return EmailDeliveryResult.Skipped("إرسال البريد معطَّل في إعدادات الخادم.");
        }
        if (string.IsNullOrWhiteSpace(_settings.SmtpServer) || string.IsNullOrWhiteSpace(_settings.FromEmail))
        {
            return EmailDeliveryResult.Skipped("إعدادات خادم البريد غير مكتملة: اسم الخادم أو بريد المُرسل فارغ.");
        }
        MailboxAddress recipient;
        try
        {
            recipient = MailboxAddress.Parse(inviteDto.Email);
        }
        catch (ParseException)
        {
            return EmailDeliveryResult.Failed("بريد العضو غير صالح، فلم يُرسل الإشعار.", "MailboxAddress.Parse rejected the recipient address.");
        }
        try
        {
            MimeMessage message = new MimeMessage();
            message.From.Add(new MailboxAddress(_settings.FromName, _settings.FromEmail));
            message.To.Add(recipient);
            message.Subject = "تمت إضافتك إلى " + businessName;
            BodyBuilder builder = new BodyBuilder
            {
                HtmlBody = BuildMembershipNoticeHtml(businessName, inviteDto.Role)
            };
            message.Body = builder.ToMessageBody();
            using SmtpClient smtp = new SmtpClient
            {
                Timeout = Math.Max(5, _settings.TimeoutSeconds) * 1000
            };
            await smtp.ConnectAsync(_settings.SmtpServer, _settings.SmtpPort, ResolveSecureSocketOptions());
            if (!string.IsNullOrWhiteSpace(_settings.Email))
            {
                await smtp.AuthenticateAsync(_settings.Email, _settings.Password);
            }
            await smtp.SendAsync(message);
            await smtp.DisconnectAsync(quit: true);
            return EmailDeliveryResult.Success();
        }
        catch (Exception ex)
        {
            // التفصيل للسجل فقط؛ رسالة المستخدم لا تكشف اسم خادم ولا بيانات مصادقة.
            Console.WriteLine("Mail delivery failed: " + ex.GetType().Name + " - " + ex.Message);
            return EmailDeliveryResult.Failed("أُضيف العضو، لكن تعذّر إرسال إشعار البريد.", ex.Message);
        }
    }

    /// <summary>
    /// يحوّل إعداد <c>UseSsl</c> إلى وضع الاتصال الصحيح.
    ///
    /// <para>كان الوضع مُثبَّتاً على <c>None</c> سابقاً فتتعذّر مخاطبة أي خادم حقيقي،
    /// لأن خوادم البريد العامة ترفض الاتصال بلا تشفير.</para>
    /// </summary>
    private SecureSocketOptions ResolveSecureSocketOptions()
    {
        if (!_settings.UseSsl)
        {
            return SecureSocketOptions.None;
        }
        return (_settings.SmtpPort == 465) ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls;
    }

    private static string BuildMembershipNoticeHtml(string businessName, string role)
    {
        StringBuilder html = new StringBuilder();
        html.Append("<div dir=\"rtl\" style=\"font-family:Segoe UI,Tahoma,Arial,sans-serif;text-align:right;color:#1f2937;line-height:1.8\">");
        html.Append("<p>مرحباً،</p>");
        html.Append("<p>تمت إضافتك إلى منشأة <strong>").Append(WebUtility.HtmlEncode(businessName)).Append("</strong> في نظام الحسابات بدور <strong>").Append(WebUtility.HtmlEncode(RoleLabel(role))).Append("</strong>.</p>");
        html.Append("<p>يمكنك الدخول إلى النظام باسم المستخدم أو البريد الإلكتروني المسجَّل لديك.</p>");
        html.Append("<p style=\"color:#6b7280;font-size:13px\">إذا لم تكن تتوقّع هذه الرسالة، يمكنك تجاهلها.</p>");
        html.Append("</div>");
        return html.ToString();
    }

    private static string RoleLabel(string role)
    {
        switch ((role ?? string.Empty).Trim().ToLowerInvariant())
        {
            case "owner":
                return "مالك";
            case "partner":
                return "شريك";
            case "viewer":
                return "مطّلع";
            case "admin":
                return "مسؤول";
            case "staff":
                return "موظف";
            case "dataoperator":
                return "مدخل بيانات";
            case "privateviewer":
                return "مطّلع خاص";
            case "portfolio_manager":
                return "مسؤول الخزنة";
            default:
                return role ?? string.Empty;
        }
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
