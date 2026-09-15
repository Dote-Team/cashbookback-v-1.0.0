using System;
using System.Collections.Concurrent;

namespace cashbook.Services;

public class LoginThrottle : ILoginThrottle
{
	private sealed class Attempt
	{
		public int Count;

		public DateTime FirstFailureUtc;

		public DateTime BlockedUntilUtc;
	}

	private const int MaxFailures = 5;

	private static readonly TimeSpan Window = TimeSpan.FromMinutes(15.0);

	private static readonly TimeSpan Lockout = TimeSpan.FromMinutes(15.0);

	private readonly ConcurrentDictionary<string, Attempt> _attempts = new ConcurrentDictionary<string, Attempt>(StringComparer.OrdinalIgnoreCase);

	public bool IsBlocked(string key, out int retryAfterSeconds)
	{
		retryAfterSeconds = 0;
		if (string.IsNullOrEmpty(key) || !_attempts.TryGetValue(key, out Attempt value))
		{
			return false;
		}
		DateTime utcNow = DateTime.UtcNow;
		if (value.BlockedUntilUtc > utcNow)
		{
			retryAfterSeconds = (int)Math.Ceiling((value.BlockedUntilUtc - utcNow).TotalSeconds);
			return true;
		}
		if (utcNow - value.FirstFailureUtc > Window)
		{
			_attempts.TryRemove(key, out Attempt _);
		}
		return false;
	}

	public void RecordFailure(string key)
	{
		if (string.IsNullOrEmpty(key))
		{
			return;
		}
		DateTime now = DateTime.UtcNow;
		_attempts.AddOrUpdate(key, (string _) => new Attempt
		{
			Count = 1,
			FirstFailureUtc = now
		}, delegate(string _, Attempt existing)
		{
			if (now - existing.FirstFailureUtc > Window)
			{
				existing.Count = 1;
				existing.FirstFailureUtc = now;
				existing.BlockedUntilUtc = default(DateTime);
				return existing;
			}
			existing.Count++;
			if (existing.Count >= 5)
			{
				existing.BlockedUntilUtc = now.Add(Lockout);
			}
			return existing;
		});
	}

	public void Reset(string key)
	{
		if (!string.IsNullOrEmpty(key))
		{
			_attempts.TryRemove(key, out Attempt _);
		}
	}
}
