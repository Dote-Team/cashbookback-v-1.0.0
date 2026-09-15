using System;
using System.Threading;
using cashbook.Models.Constants;

namespace cashbook.Services;

public sealed class AuditScope
{
	private sealed class Restore : IDisposable
	{
		private readonly AuditScope? _previous;

		public Restore(AuditScope? previous)
		{
			_previous = previous;
		}

		public void Dispose()
		{
			Ambient.Value = _previous;
		}
	}

	private static readonly AsyncLocal<AuditScope?> Ambient = new AsyncLocal<AuditScope>();

	private int _changeCount;

	public static AuditScope? Current
	{
		get
		{
			return Ambient.Value;
		}
		set
		{
			Ambient.Value = value;
		}
	}

	public Guid CorrelationId { get; } = Guid.NewGuid();

	public Guid? UserId { get; set; }

	public string? Username { get; set; }

	public Guid? SessionId { get; set; }

	public string? DeviceToken { get; set; }

	public Guid? BusinessId { get; set; }

	public string? IpAddress { get; set; }

	public string? UserAgent { get; set; }

	public string? HttpMethod { get; set; }

	public string? Path { get; set; }

	public string? Action { get; set; }

	public string? RequestBody { get; set; }

	public string? EntityActionHint { get; set; }

	public int ChangeCount => _changeCount;

	public bool ChangesTruncated { get; set; }

	public string ActorName => string.IsNullOrWhiteSpace(Username) ? "النظام" : Username;

	public static IDisposable Begin(out AuditScope scope)
	{
		AuditScope value = Ambient.Value;
		scope = new AuditScope();
		Ambient.Value = scope;
		return new Restore(value);
	}

	public void RecordChange()
	{
		Interlocked.Increment(ref _changeCount);
	}

	public string ResolveDataAction(string entityName, string operation)
	{
		if (!string.IsNullOrWhiteSpace(Action))
		{
			return Action;
		}
		string resource = entityName.ToLowerInvariant();
		if (1 == 0)
		{
		}
		string text = ((operation == "Added") ? "create" : ((!(operation == "Deleted")) ? "update" : "delete"));
		if (1 == 0)
		{
		}
		string verb = text;
		return AuditAction.Generic(resource, verb);
	}
}
