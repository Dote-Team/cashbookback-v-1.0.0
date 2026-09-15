using System.Threading.Tasks;
using cashbook.Dto.user;
using cashbook.Models;

namespace cashbook.Interfaces;

public interface IEmailService
{
	/// <summary>يضيف العضو إلى المنشأة بعد التحقّق من الصلاحيات ومدى صحة الخزائن.</summary>
	Task<bool> SendInvaiteByEmail(InviteDto inviteDto);

	/// <summary>
	/// يرسل بريداً يُعرّف العضو المضاف بعضويته ودوره.
	///
	/// <para>لا يرمي استثناءً: تُعاد النتيجة ككائن ليُبلّغ عنها في استجابة الطلب،
	/// لأن إضافة العضو تكون قد حُفظت فعلاً وفشل الإشعار لا يُلغيها.</para>
	/// </summary>
	Task<EmailDeliveryResult> SendMembershipNoticeAsync(InviteDto inviteDto, string businessName);
}
