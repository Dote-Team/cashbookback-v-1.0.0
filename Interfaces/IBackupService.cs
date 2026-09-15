using System;
using System.IO;
using System.Threading.Tasks;
using cashbook.Dto.backup;

namespace cashbook.Interfaces;

public interface IBackupService
{
	Task<byte[]> ExportBookAsync(Guid bookId, string password, Guid userId);

	Task<BackupManifestDto> InspectAsync(Stream file, string password);

	Task<BackupImportResultDto> ImportAsync(Stream file, string password, Guid businessId, Guid? targetBookId, string? newBookName, Guid userId);

	Task DumpAsync(string filePath, string password, string? outputDirectory = null);
}
