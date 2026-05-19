using System.Collections.Concurrent;
using MediatR;
using UniScheduler.Application.Features.Schedules.Commands;

namespace UniScheduler.Api.Services;

public interface IGenerationJobQueue
{
    Guid Enqueue(Guid scheduleId, int timeoutSeconds);
    GenerationJobStatus GetStatus(Guid scheduleId);
}

public record GenerationJobStatus(
    Guid ScheduleId,
    string Status,
    string? Message,
    string? Stage,
    int EntriesCreated,
    DateTime? CompletedAt);

public class GenerationJobQueue : IGenerationJobQueue
{
    private readonly ConcurrentDictionary<Guid, GenerationJobStatus> _statuses = new();
    private readonly ConcurrentQueue<(Guid jobId, Guid scheduleId, int timeout)> _queue = new();

    public Guid Enqueue(Guid scheduleId, int timeoutSeconds)
    {
        var jobId = scheduleId;
        _statuses[scheduleId] = new GenerationJobStatus(scheduleId, "queued", null, null, 0, null);
        _queue.Enqueue((jobId, scheduleId, timeoutSeconds));
        return jobId;
    }

    public GenerationJobStatus GetStatus(Guid scheduleId)
        => _statuses.TryGetValue(scheduleId, out var status)
            ? status
            : new GenerationJobStatus(scheduleId, "not_found", null, null, 0, null);

    public bool TryDequeue(out (Guid jobId, Guid scheduleId, int timeout) item)
        => _queue.TryDequeue(out item);

    public void SetStatus(Guid scheduleId, GenerationJobStatus status) => _statuses[scheduleId] = status;
}

public class GenerationBackgroundService : BackgroundService
{
    private readonly GenerationJobQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<GenerationBackgroundService> _logger;

    public GenerationBackgroundService(GenerationJobQueue queue, IServiceScopeFactory scopeFactory, ILogger<GenerationBackgroundService> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            if (_queue.TryDequeue(out var item))
            {
                var (_, scheduleId, timeout) = item;
                _queue.SetStatus(scheduleId, new GenerationJobStatus(scheduleId, "running", null, null, 0, null));

                var progress = new Progress<string>(stage =>
                {
                    var current = _queue.GetStatus(scheduleId);
                    _queue.SetStatus(scheduleId, current with { Stage = stage });
                });

                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
                    var result = await mediator.Send(new GenerateScheduleCommand(scheduleId, timeout, progress), stoppingToken);

                    _queue.SetStatus(scheduleId, new GenerationJobStatus(
                        scheduleId,
                        result.Success ? "completed" : "failed",
                        result.Message,
                        null,
                        result.EntriesCreated,
                        DateTime.UtcNow));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Generation failed for schedule {ScheduleId}", scheduleId);
                    _queue.SetStatus(scheduleId, new GenerationJobStatus(scheduleId, "failed", ex.Message, null, 0, DateTime.UtcNow));
                }
            }
            else
            {
                await Task.Delay(500, stoppingToken);
            }
        }
    }
}
