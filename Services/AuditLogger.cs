using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using Microsoft.Extensions.Options;
using cashbook.Interfaces;
using cashbook.Models;

namespace cashbook.Services;

public sealed class AuditLogger : IAuditLogger
{
	private readonly Channel<AuditLog> _channel;

	private readonly AuditSettings _settings;

	private long _dropped;

	private long _written;

	public ChannelReader<AuditLog> Reader => _channel.Reader;

	public int PendingCount => _channel.Reader.Count;

	public long DroppedCount => Interlocked.Read(in _dropped);

	public long WrittenCount => Interlocked.Read(in _written);

	public AuditLogger(IOptions<AuditSettings> settings)
	{
		_settings = settings.Value;
		_channel = Channel.CreateBounded<AuditLog>(new BoundedChannelOptions((_settings.QueueCapacity > 0) ? _settings.QueueCapacity : 5000)
		{
			FullMode = BoundedChannelFullMode.Wait,
			SingleReader = true,
			SingleWriter = false
		});
	}

	public void Enqueue(AuditLog entry)
	{
		if (!_settings.Enabled || entry == null)
		{
			return;
		}
		try
		{
			if (!_channel.Writer.TryWrite(entry))
			{
				Interlocked.Increment(ref _dropped);
			}
		}
		catch
		{
			Interlocked.Increment(ref _dropped);
		}
	}

	public void EnqueueRange(IEnumerable<AuditLog> entries)
	{
		if (!_settings.Enabled || entries == null)
		{
			return;
		}
		foreach (AuditLog entry in entries)
		{
			Enqueue(entry);
		}
	}

	internal void ReportWritten(int count)
	{
		Interlocked.Add(ref _written, count);
	}
}
