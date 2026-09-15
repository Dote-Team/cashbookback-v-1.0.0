using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using cashbook.Data;
using cashbook.Interfaces;
using cashbook.Models;

namespace cashbook.Services;

public sealed class AuditRetentionService : BackgroundService
{
	private const int DeleteBatchSize = 5000;

	private const int MaxBatchesPerSweep = 200;

	private readonly IAuditLogger _auditLogger;

	private readonly IServiceScopeFactory _scopeFactory;

	private readonly AuditSettings _settings;

	private readonly ILogger<AuditRetentionService> _log;

	public AuditRetentionService(IAuditLogger auditLogger, IServiceScopeFactory scopeFactory, IOptions<AuditSettings> settings, ILogger<AuditRetentionService> log)
	{
		_auditLogger = auditLogger;
		_scopeFactory = scopeFactory;
		_settings = settings.Value;
		_log = log;
	}

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		if (!_settings.Enabled || !_settings.RetentionEnabled)
		{
			_log.LogInformation("تنظيف سجل التدقيق معط\u064e\u0651ل — الاحتفاظ دائم");
			return;
		}
		await Task.Delay(TimeSpan.FromMinutes(2.0), stoppingToken);
		TimeSpan interval = TimeSpan.FromHours(Math.Max(1, _settings.RetentionSweepHours));
		while (!stoppingToken.IsCancellationRequested)
		{
			try
			{
				await SweepAsync(stoppingToken);
			}
			catch (OperationCanceledException)
			{
				break;
			}
			catch (Exception exception)
			{
				_log.LogError(exception, "فشل تنظيف سجل التدقيق");
			}
			try
			{
				await Task.Delay(interval, stoppingToken);
			}
			catch (OperationCanceledException)
			{
				break;
			}
		}
	}

	private async Task SweepAsync(CancellationToken cancellationToken)
	{
		DateTime cutoff = DateTime.UtcNow.AddDays(-_settings.RetentionDays);
		using IServiceScope scope = _scopeFactory.CreateScope();
		ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
		int total = 0;
		for (int batch = 0; batch < 200; batch++)
		{
			int deleted = await db.Database.ExecuteSqlRawAsync("DELETE TOP (@batchSize) FROM [AuditLogs] WHERE [OccurredAt] < @cutoff", new SqlParameter[2]
			{
				new SqlParameter("@batchSize", 5000),
				new SqlParameter("@cutoff", cutoff)
			}, cancellationToken);
			total += deleted;
			if (deleted < 5000)
			{
				break;
			}
		}
		if (total != 0)
		{
			_log.LogInformation("ن\u064fظ\u0651ف سجل التدقيق: ح\u064fذف {Count} سطر أقدم من {Cutoff:yyyy-MM-dd}", total, cutoff);
			_auditLogger.Enqueue(new AuditLog
			{
				OccurredAt = DateTime.UtcNow,
				CorrelationId = Guid.NewGuid(),
				Category = "System",
				Action = "audit.retention.purge",
				Severity = "Warning",
				IsSuccess = true,
				Username = "النظام",
				Summary = $"صيانة دورية: ح\u064fذف {total} سطر تدقيق أقدم من {cutoff:yyyy-MM-dd} (مدة الاحتفاظ {_settings.RetentionDays} يوما\u064b)",
				DataJson = $"{{\"deleted\":{total},\"cutoff\":\"{cutoff:O}\",\"retentionDays\":{_settings.RetentionDays}}}"
			});
		}
	}
}
