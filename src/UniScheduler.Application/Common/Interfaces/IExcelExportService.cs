namespace UniScheduler.Application.Common.Interfaces;

public interface IExcelExportService
{
    Task<byte[]> ExportScheduleAsync(Guid scheduleId, Guid? groupId, Guid? teacherId, CancellationToken cancellationToken = default);
}
