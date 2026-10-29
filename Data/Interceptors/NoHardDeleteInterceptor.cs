using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using cashbook.Models;

namespace cashbook.Data.Interceptors;

/// <summary>
/// حماية أخيرة: لا حذف فعلي في النظام. أي محاولة لحذف خزنة أو قيد أو منشأة أو مستخدم
/// أو سجل حركة أو بصمة تُرفض هنا قبل وصولها إلى قاعدة البيانات، حتى لو كتبها كود مستقبلي بالخطأ.
/// الأرشفة تتم بتعديل <see cref="IArchivable.IsArchived"/> لا بالحذف.
///
/// المسموح حذفه: الجلسات، وصفوف الصلاحيات عند سحبها، وقيم الحقول المخصصة عند استبدالها
/// (كلها بيانات إعداد لا سجلات مالية، وتغييرها مسجّل في سجل التدقيق).
/// </summary>
public sealed class NoHardDeleteInterceptor : SaveChangesInterceptor
{
	public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
	{
		Guard(eventData.Context);
		return base.SavingChanges(eventData, result);
	}

	public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
	{
		Guard(eventData.Context);
		return base.SavingChangesAsync(eventData, result, cancellationToken);
	}

	private static void Guard(DbContext? context)
	{
		if (context == null)
		{
			return;
		}
		var blocked = context.ChangeTracker.Entries()
			.Where(e => e.State == EntityState.Deleted && IsProtected(e.Entity))
			.Select(e => e.Entity.GetType().Name)
			.Distinct()
			.ToList();
		if (blocked.Count > 0)
		{
			throw new InvalidOperationException("الحذف غير مسموح في النظام، استخدم الأرشفة بدلاً منه: " + string.Join(", ", blocked));
		}
	}

	private static bool IsProtected(object entity) =>
		entity is IArchivable or User or TransactionHistory or UserFingerprint or BusinessUser or Setting or ExchangeRate or TrashItem;
}
