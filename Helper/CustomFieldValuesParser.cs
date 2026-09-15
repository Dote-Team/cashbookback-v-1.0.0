using System;
using System.Collections.Generic;
using System.Text.Json;
using cashbook.Dto.transaction;

namespace cashbook.Helper;

/// <summary>
/// تحليل حقل <c>CustomFieldValues</c> القادم كنصّ داخل نموذج multipart.
///
/// <para>السبب: الحقل نصّ JSON حرّ يكتبه العميل، فإذا كانت صيغته معطوبة رمى
/// <see cref="JsonSerializer"/> استثناءً يلتقطه مستودع الحركات ويُغلّفه كخطأ
/// خادم <c>500</c> برسالة عامّة لا تشرح للمستخدم ما الخطأ. هذا المحلّل يحوّل
/// الفشل إلى رسالة عربية واضحة يُرجعها المتحكّم برمز <c>400</c>.</para>
///
/// <para>ويُستخدم أيضاً داخل المستودع قبل الكتابة، فلا تتحوّل صيغة معطوبة إلى
/// حذف قيم قائمة عند التعديل.</para>
/// </summary>
public static class CustomFieldValuesParser
{
	/// <summary>رسالة موحّدة تُعرض للمستخدم عند فشل التحليل.</summary>
	public const string InvalidFormatMessage = "صيغة الحقول المخصصة غير صحيحة — يجب أن تكون مصفوفة JSON من كائنات بالشكل: [{\"customFieldId\":\"...\",\"value\":\"...\"}].";

	private static readonly JsonSerializerOptions Options = new JsonSerializerOptions
	{
		PropertyNameCaseInsensitive = true
	};

	/// <summary>
	/// يحاول تحليل النص إلى قائمة قيم حقول مخصصة.
	/// </summary>
	/// <param name="json">النص القادم من العميل. الفارغ أو الأبيض يعني «لا قيم» ولا يُعدّ خطأً.</param>
	/// <param name="values">القائمة الناتجة. فارغة عند الفشل أو عند غياب القيم.</param>
	/// <param name="error">رسالة عربية عند الفشل، وإلا <c>null</c>.</param>
	/// <returns><c>true</c> إذا كان النص صالحاً أو غائباً، و<c>false</c> إذا كان معطوباً.</returns>
	public static bool TryParse(string? json, out List<CustomFieldValueCreateDto> values, out string? error)
	{
		values = new List<CustomFieldValueCreateDto>();
		error = null;

		if (string.IsNullOrWhiteSpace(json))
		{
			return true;
		}

		List<CustomFieldValueCreateDto> parsed;
		try
		{
			parsed = JsonSerializer.Deserialize<List<CustomFieldValueCreateDto>>(json, Options);
		}
		catch (JsonException)
		{
			// صيغة ليست JSON صالحاً، أو بنية لا تطابق قائمة كائنات (مثل مصفوفة نصوص).
			error = InvalidFormatMessage;
			return false;
		}

		if (parsed == null)
		{
			// النص "null" صيغة صحيحة نحويّاً لكنها بلا معنى — تُعامَل كغياب قيم.
			return true;
		}

		for (int index = 0; index < parsed.Count; index++)
		{
			if (parsed[index] == null)
			{
				error = InvalidFormatMessage;
				return false;
			}

			if (parsed[index].CustomFieldId == Guid.Empty)
			{
				// معرّف فارغ كان ينتهي إلى خطأ مفتاح أجنبي = 500، وهذا مدخل معطوب أصلاً.
				error = "معرّف الحقل المخصص (customFieldId) مطلوب لكل قيمة — القيمة رقم " + (index + 1) + ".";
				return false;
			}
		}

		values = parsed;
		return true;
	}
}
