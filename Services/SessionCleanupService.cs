using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using cashbook.Data;

namespace cashbook.Services;

/// <summary>
/// جدول Sessions لم يكن يُنظَّف: كل دخول يضيف صفاً يبقى للأبد. هذه الخدمة تحذف كل ست ساعات
/// الجلسات التي انتهت منذ يوم على الأقل، على دفعات صغيرة لا تقفل الجدول.
/// الجلسة المنتهية لا تصلح لشيء: لا للدخول ولا لتجديد الرمز.
/// </summary>
public sealed class SessionCleanupService : BackgroundService
{
	private const int BatchSize = 2000;

	private const int MaxBatchesPerSweep = 100;

	private static readonly TimeSpan Interval = TimeSpan.FromHours(6);

	private readonly IServiceScopeFactory _scopeFactory;

	private readonly ILogger<SessionCleanupService> _log;

	public SessionCleanupService(IServiceScopeFactory scopeFactory, ILogger<SessionCleanupService> log)
	{
		_scopeFactory = scopeFactory;
		_log = log;
	}

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		try
		{
			await Task.Delay(TimeSpan.FromMinutes(3), stoppingToken);
			while (!stoppingToken.IsCancellationRequested)
			{
				try
				{
					await SweepAsync(stoppingToken);
				}
				catch (Exception exception) when (exception is not OperationCanceledException)
				{
					_log.LogError(exception, "فشل تنظيف الجلسات المنتهية");
				}
				await Task.Delay(Interval, stoppingToken);
			}
		}
		catch (OperationCanceledException)
		{
		}
	}

	private async Task SweepAsync(CancellationToken cancellationToken)
	{
		DateTime cutoff = DateTime.UtcNow.AddDays(-1);
		using IServiceScope scope = _scopeFactory.CreateScope();
		ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
		int total = 0;
		for (int batch = 0; batch < MaxBatchesPerSweep; batch++)
		{
			int deleted = await db.Database.ExecuteSqlRawAsync("DELETE TOP (@batchSize) FROM [Sessions] WHERE [ExpiredAt] < @cutoff",
				new[] { new SqlParameter("@batchSize", BatchSize), new SqlParameter("@cutoff", cutoff) }, cancellationToken);
			total += deleted;
			if (deleted < BatchSize)
			{
				break;
			}
		}
		if (total > 0)
		{
			_log.LogInformation("حُذفت {Count} جلسة منتهية", total);
		}
	}
}
