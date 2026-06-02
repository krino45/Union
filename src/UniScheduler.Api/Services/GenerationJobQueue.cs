using System.Collections.Concurrent;
using MediatR;
using UniScheduler.Application.Features.Schedules.Commands;

namespace UniScheduler.Api.Services;

public interface IGenerationJobQueue
{
    Guid Enqueue(Guid scheduleId, int timeoutSeconds, IReadOnlyList<Guid>? planIds = null, bool polish = false);
    GenerationJobStatus GetStatus(Guid scheduleId);
    bool TryCancel(Guid scheduleId);
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
    private readonly ConcurrentDictionary<Guid, GenerationJobStatus> statuses = new();
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> cancellations = new();
    private readonly ConcurrentQueue<(Guid jobId, Guid scheduleId, int timeout, IReadOnlyList<Guid>? planIds, bool polish)> queue = new();

    public Guid Enqueue(Guid scheduleId, int timeoutSeconds, IReadOnlyList<Guid>? planIds = null, bool polish = false)
    {
        var jobId = scheduleId;
        statuses[scheduleId] = new GenerationJobStatus(scheduleId, "queued", null, null, 0, null);
        queue.Enqueue((jobId, scheduleId, timeoutSeconds, planIds, polish));
        return jobId;
    }

    public GenerationJobStatus GetStatus(Guid scheduleId)
        => statuses.TryGetValue(scheduleId, out var status)
            ? status
            : new GenerationJobStatus(scheduleId, "not_found", null, null, 0, null);

    public bool TryDequeue(out (Guid jobId, Guid scheduleId, int timeout, IReadOnlyList<Guid>? planIds, bool polish) item)
        => queue.TryDequeue(out item);

    public void SetStatus(Guid scheduleId, GenerationJobStatus status) => statuses[scheduleId] = status;

    public void RegisterCancellation(Guid scheduleId, CancellationTokenSource cts)
        => cancellations[scheduleId] = cts;

    public void UnregisterCancellation(Guid scheduleId)
    {
        if (cancellations.TryRemove(scheduleId, out var cts))
            cts.Dispose();
    }

    public bool TryCancel(Guid scheduleId)
    {
        if (cancellations.TryGetValue(scheduleId, out var cts))
        {
            cts.Cancel();
            return true;
        }
        if (statuses.TryGetValue(scheduleId, out var s) && s.Status == "queued")
        {
            statuses[scheduleId] = s with { Status = "failed", Message = "Отменено пользователем", CompletedAt = DateTime.UtcNow };
            return true;
        }
        return false;
    }
}

public class GenerationBackgroundService : BackgroundService
{
    private readonly GenerationJobQueue queue;
    private readonly IServiceScopeFactory scopeFactory;
    private readonly ILogger<GenerationBackgroundService> logger;

    public GenerationBackgroundService(GenerationJobQueue queue, IServiceScopeFactory scopeFactory, ILogger<GenerationBackgroundService> logger)
    {
        this.queue = queue;
        this.scopeFactory = scopeFactory;
        this.logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            if (queue.TryDequeue(out var item))
            {
                var (_, scheduleId, timeout, planIds, polish) = item;

                var queued = queue.GetStatus(scheduleId);
                if (queued.Status == "failed")
                    continue;

                queue.SetStatus(scheduleId, new GenerationJobStatus(scheduleId, "running", null, null, 0, null));

                using var cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                queue.RegisterCancellation(scheduleId, cts);

                var progress = new Progress<string>(stage =>
                {
                    var current = queue.GetStatus(scheduleId);
                    queue.SetStatus(scheduleId, current with { Stage = stage });
                });

                try
                {
                    using var scope = scopeFactory.CreateScope();
                    var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
                    var result = await mediator.Send(new GenerateScheduleCommand(scheduleId, timeout, progress, planIds, polish), cts.Token);

                    queue.SetStatus(scheduleId, new GenerationJobStatus(
                        scheduleId,
                        result.Success ? "completed" : "failed",
                        result.Message,
                        null,
                        result.EntriesCreated,
                        DateTime.UtcNow));
                }
                catch (OperationCanceledException) when (cts.IsCancellationRequested && !stoppingToken.IsCancellationRequested)
                {
                    var current = queue.GetStatus(scheduleId);
                    queue.SetStatus(scheduleId, new GenerationJobStatus(
                        scheduleId, "failed", "Отменено пользователем",
                        null, current.EntriesCreated, DateTime.UtcNow));
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Generation failed for schedule {ScheduleId}", scheduleId);
                    queue.SetStatus(scheduleId, new GenerationJobStatus(scheduleId, "failed", ex.Message, null, 0, DateTime.UtcNow));
                }
                finally
                {
                    queue.UnregisterCancellation(scheduleId);
                }
            }
            else
            {
                await Task.Delay(500, stoppingToken);
            }
        }
    }
}
