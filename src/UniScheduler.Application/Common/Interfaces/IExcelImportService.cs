using UniScheduler.Application.Common.Models;

namespace UniScheduler.Application.Common.Interfaces;

public interface IExcelImportService
{
    Task<ImportPreviewDto> ParseAsync(Stream stream, Guid scheduleId, CancellationToken cancellationToken = default);
    Task<int> CommitAsync(ImportPreviewDto preview, Guid scheduleId, CancellationToken cancellationToken = default);
}
