using System;
using System.Collections.Generic;
using cashbook.Dto.transaction;
using cashbook.Helper;
using cashbook.Models;
using cashbook.Models.Constants;
using cashbook.Models.Enums;

namespace cashbook.Validators;

public static class TransactionValidator
{
	public static List<string> ValidateCreate(CreateTransactionDto dto)
	{
		List<string> errors = ValidateRules(dto.Type, dto.Amount, dto.Currency, dto.ExchangeRate, dto.ExchangeDate, (dto.Date == default(DateTime)) ? ((DateTime?)null) : new DateTime?(dto.Date));
		AppendCustomFieldErrors(dto.CustomFieldValues, errors);
		return errors;
	}

	public static List<string> ValidateUpdate(UpdateTransactionDto dto, Transaction existing, out ResolvedTransactionValues resolved)
	{
		resolved = Resolve(dto, existing);
		List<string> errors = ValidateRules(resolved.Type, resolved.Amount, resolved.Currency, resolved.ExchangeRate, resolved.ExchangeDate, resolved.Date);
		AppendCustomFieldErrors(dto.CustomFieldValues, errors);
		return errors;
	}

	/// <summary>
	/// يضيف خطأ عربياً إذا كانت صيغة الحقول المخصصة معطوبة.
	///
	/// <para>وجوده هنا مقصود: المتحكّم يفحص نتيجة هذا المدقّق ويردّ برمز
	/// <c>400</c> قبل أن يصل الطلب إلى الكتابة، فلا يتحوّل خطأ الصيغة إلى
	/// خطأ خادم <c>500</c>.</para>
	/// </summary>
	private static void AppendCustomFieldErrors(string? customFieldValues, List<string> errors)
	{
		if (!CustomFieldValuesParser.TryParse(customFieldValues, out _, out string? error) && !string.IsNullOrEmpty(error))
		{
			errors.Add(error);
		}
	}

	public static ResolvedTransactionValues Resolve(UpdateTransactionDto dto, Transaction existing)
	{
		string text = TransactionTypes.Normalize(existing.Type) ?? existing.Type;
		string type = TransactionTypes.Normalize(dto.Type) ?? dto.Type ?? text;
		bool flag = dto.Currency.HasValue && dto.Currency.Value != existing.Currency;
		bool flag2 = !string.IsNullOrWhiteSpace(dto.Type) && TransactionTypes.Normalize(dto.Type) != TransactionTypes.Normalize(existing.Type);
		bool flag3 = flag | flag2;
		return new ResolvedTransactionValues
		{
			Type = type,
			Amount = (dto.Amount ?? existing.Amount),
			Currency = (dto.Currency ?? existing.Currency),
			Date = (dto.Date ?? existing.Date),
			ExchangeRate = (flag3 ? dto.ExchangeRate : (dto.ExchangeRate ?? existing.ExchangeRate)),
			ExchangeDate = (flag3 ? dto.ExchangeDate : (dto.ExchangeDate ?? existing.ExchangeDate))
		};
	}

	public static List<string> ValidateRules(string? type, decimal amount, CurrencyCode currency, decimal? exchangeRate, DateTime? exchangeDate, DateTime? date)
	{
		List<string> list = new List<string>();
		if (!Enum.IsDefined(typeof(CurrencyCode), currency))
		{
			list.Add("العملة غير مدعومة — القيم المقبولة: USD (دولار) أو IQD (دينار).");
		}
		string text = TransactionTypes.Normalize(type);
		if (text == null)
		{
			list.Add("نوع الحركة غير معروف — القيم المقبولة: إدخال/إيداع (cash in) أو إخراج/سحب (cash out).");
		}
		if (!MoneyMath.TryValidateAmount(amount, currency, out string error))
		{
			list.Add(error);
		}
		switch (currency)
		{
			case CurrencyCode.USD:
				{
					if (!MoneyMath.TryValidateRate(exchangeRate, out string error2))
					{
						list.Add(error2);
					}
					bool flag = text == "cash in";
					bool flag2 = text == "cash out";
					if (!date.HasValue || date.Value == default(DateTime))
					{
						list.Add(flag2 ? "تاريخ الإخراج إلزامي لحركات سحب الدولار." : "تاريخ الإدخال الفعلي إلزامي لحركات إيداع الدولار.");
					}
					if (flag && (!exchangeDate.HasValue || exchangeDate.Value == default(DateTime)))
					{
						list.Add("تاريخ الصرف إلزامي لحركات إيداع الدولار.");
					}
					if (flag2 && exchangeDate.HasValue)
					{
						list.Add("حركة سحب الدولار تقبل تاريخا\u064b واحدا\u064b فقط (تاريخ الإخراج) ولا ت\u064fسج\u064e\u0651ل بتاريخ صرف.");
					}
					break;
				}
			case CurrencyCode.IQD:
				if (exchangeRate.HasValue)
				{
					list.Add("سعر الصرف ي\u064fسج\u064e\u0651ل لحركات الدولار فقط.");
				}
				if (exchangeDate.HasValue)
				{
					list.Add("تاريخ الصرف ي\u064fسج\u064e\u0651ل لحركات إيداع الدولار فقط.");
				}
				break;
		}
		return list;
	}
}
