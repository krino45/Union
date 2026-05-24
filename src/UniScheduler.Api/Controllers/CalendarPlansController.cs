using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniScheduler.Application.DTOs;
using UniScheduler.Application.Features.CalendarPlans;
using UniScheduler.Domain.Enums;

namespace UniScheduler.Api.Controllers;

[ApiController]
[Route("api/calendar-plans")]
[Authorize(Roles = "Admin,SuperAdmin")]
public class CalendarPlansController : ControllerBase
{
    private readonly IMediator _mediator;
    public CalendarPlansController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    public async Task<ActionResult<List<CalendarPlanDto>>> GetAll(
        [FromQuery] int? academicYear, [FromQuery] Term? term, CancellationToken ct)
        => Ok(await _mediator.Send(new GetCalendarPlansQuery(academicYear, term), ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CalendarPlanDto>> GetById(Guid id, CancellationToken ct)
        => Ok(await _mediator.Send(new GetCalendarPlanQuery(id), ct));

    [HttpPost]
    public async Task<ActionResult<CalendarPlanDto>> Create([FromBody] UpsertCalendarPlanDto dto, CancellationToken ct)
    {
        var result = await _mediator.Send(new CreateCalendarPlanCommand(dto), ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<CalendarPlanDto>> Update(Guid id, [FromBody] UpsertCalendarPlanDto dto, CancellationToken ct)
        => Ok(await _mediator.Send(new UpdateCalendarPlanCommand(id, dto), ct));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new DeleteCalendarPlanCommand(id), ct);
        return NoContent();
    }
}
