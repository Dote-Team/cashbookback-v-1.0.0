namespace cashbook.Models;

/// <summary>
/// نتيجة محاولة إرسال بريد.
///
/// <para>خدمة البريد لا ترمي استثناءً أبداً: العملية الأصلية (إضافة العضو) تكون
/// قد حُفظت في قاعدة البيانات فعلاً، وفشل الإشعار لا يجوز أن يُلغيها. لذلك
/// تُعاد النتيجة ككائن ليُبلَّغ عنها للمستخدم بدل إخفائها أو إسقاط الطلب.</para>
/// </summary>
public sealed class EmailDeliveryResult
{
    /// <summary>هل أُرسل البريد فعلاً؟</summary>
    public bool Sent { get; private init; }

    /// <summary>هل حُوِلت المحاولة؟ <c>false</c> مع <c>Sent = false</c> تعني «لم يُحاول» لا «فشل».</summary>
    public bool Attempted { get; private init; }

    /// <summary>رسالة عربية قصيرة تُعرض للمستخدم — بلا أي تفاصيل داخلية عن الخادم.</summary>
    public string Message { get; private init; } = string.Empty;

    /// <summary>تفصيل تقني للسجل والتدقيق فقط — لا يُعاد في استجابة الـAPI.</summary>
    public string? Detail { get; private init; }

    public static EmailDeliveryResult Success()
    {
        return new EmailDeliveryResult
        {
            Sent = true,
            Attempted = true,
            Message = "أُرسل إشعار البريد إلى العضو."
        };
    }

    /// <summary>لم تُحاول الإرسال: الإعداد معطَّل أو ناقص. هذه حالة متوقَّعة لا خطأ.</summary>
    public static EmailDeliveryResult Skipped(string message)
    {
        return new EmailDeliveryResult
        {
            Sent = false,
            Attempted = false,
            Message = message
        };
    }

    /// <summary>حُوِلت المحاولة وفشلت — تستحق انتباه المشرف.</summary>
    public static EmailDeliveryResult Failed(string message, string? detail)
    {
        return new EmailDeliveryResult
        {
            Sent = false,
            Attempted = true,
            Message = message,
            Detail = detail
        };
    }
}
