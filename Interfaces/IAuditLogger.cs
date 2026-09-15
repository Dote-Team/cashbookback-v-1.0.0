using System.Collections.Generic;
using cashbook.Models;

namespace cashbook.Interfaces;

public interface IAuditLogger
{
	int PendingCount { get; }

	long DroppedCount { get; }

	long WrittenCount { get; }

	void Enqueue(AuditLog entry);

	void EnqueueRange(IEnumerable<AuditLog> entries);
}
