using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using cashbook.Data;
using cashbook.Models;

namespace cashbook.Services;

public sealed class AuditWriterService : BackgroundService
{
	private const int MaxAttempts = 3;

	private readonly AuditLogger _logger;

	private readonly IServiceScopeFactory _scopeFactory;

	private readonly AuditSettings _settings;

	private readonly ILogger<AuditWriterService> _log;

	public AuditWriterService(AuditLogger logger, IServiceScopeFactory scopeFactory, IOptions<AuditSettings> settings, ILogger<AuditWriterService> log)
	{
		_logger = logger;
		_scopeFactory = scopeFactory;
		_settings = settings.Value;
		_log = log;
	}

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		if (!_settings.Enabled)
		{
			_log.LogWarning("سجل التدقيق م\u064fعط\u064e\u0651ل (AuditSettings:Enabled = false) — لا ت\u064fكتب أي سطور");
			return;
		}
		int maxBatch = ((_settings.BatchSize > 0) ? _settings.BatchSize : 200);
		List<AuditLog> batch = new List<AuditLog>(maxBatch);
		ChannelReader<AuditLog> reader = _logger.Reader;
		_log.LogInformation("كاتب سجل التدقيق يعمل — دفعة {Batch} سطر، حد\u0651 البطء {Slow} مللي ثانية", maxBatch, _settings.SlowRequestMs);
		while (!stoppingToken.IsCancellationRequested)
		{
			try
			{
				if (!(await reader.WaitToReadAsync(stoppingToken)))
				{
					break;
				}
				batch.Clear();
				AuditLog entry;
				while (batch.Count < maxBatch && reader.TryRead(out entry))
				{
					batch.Add(entry);
				}
				if (batch.Count > 0)
				{
					await WriteBatchAsync(batch, stoppingToken);
				}
				continue;
			}
			catch (OperationCanceledException)
			{
			}
			catch (Exception exception)
			{
				_log.LogError(exception, "خطأ غير متوقع في كاتب سجل التدقيق");
				await DelayAsync(stoppingToken);
				continue;
			}
			break;
		}
		await DrainAsync(stoppingToken);
	}

	private async Task WriteBatchAsync(List<AuditLog> batch, CancellationToken cancellationToken)
	{
		for (int attempt = 1; attempt <= 3; attempt++)
		{
			try
			{
				using IServiceScope scope = _scopeFactory.CreateScope();
				ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
				db.ChangeTracker.AutoDetectChangesEnabled = false;
				await db.Set<AuditLog>().AddRangeAsync(batch, cancellationToken);
				await db.SaveChangesAsync(cancellationToken);
				_logger.ReportWritten(batch.Count);
				break;
			}
			catch (OperationCanceledException)
			{
				throw;
			}
			catch (Exception exception)
			{
				if (attempt == 3)
				{
					_log.LogError(exception, "تعذ\u0651ر كتابة {Count} سطر تدقيق بعد {Attempts} محاولات — أ\u064fسقطت الدفعة", batch.Count, 3);
					break;
				}
				await Task.Delay(TimeSpan.FromSeconds(Math.Max(1, _settings.RetryDelaySeconds)), cancellationToken);
			}
		}
	}

	private async Task DrainAsync(CancellationToken cancellationToken)
	{
		List<AuditLog> remaining = new List<AuditLog>();
		AuditLog entry;
		while (_logger.Reader.TryRead(out entry))
		{
			remaining.Add(entry);
		}
		if (remaining.Count == 0)
		{
			return;
		}
		int maxBatch = ((_settings.BatchSize > 0) ? _settings.BatchSize : 200);
		foreach (AuditLog[] chunk in remaining.Chunk(maxBatch))
		{
			if (cancellationToken.IsCancellationRequested)
			{
				break;
			}
			using CancellationTokenSource timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10.0));
			await WriteBatchAsync(chunk.ToList(), timeout.Token);
		}
		_log.LogInformation("ك\u064fتب {Count} سطر تدقيق متبق\u064d\u0651 عند الإغلاق", remaining.Count);
	}

	private Task DelayAsync(CancellationToken cancellationToken)
	{
		return Task.Delay(TimeSpan.FromSeconds(Math.Max(1, _settings.RetryDelaySeconds)), cancellationToken);
	}
}
