using UniScheduler.Domain.Entities;

namespace UniScheduler.Application.Features.PairTimes;

public static class PairTimeDefaults
{
    private static readonly (int Pair, int SH, int SM, int EH, int EM)[] Slots =
    {
        (1, 8, 0, 9, 35),
        (2, 9, 50, 11, 25),
        (3, 11, 40, 13, 15),
        (4, 13, 45, 15, 20),
        (5, 15, 35, 17, 10),
        (6, 17, 25, 19, 0),
        (7, 19, 15, 20, 50),
    };

    public static List<PairTimeSlot> CreateFor(Guid universityId) =>
        Slots.Select(s => new PairTimeSlot
        {
            UniversityId = universityId,
            PairNumber = s.Pair,
            StartTime = new TimeOnly(s.SH, s.SM),
            EndTime = new TimeOnly(s.EH, s.EM)
        }).ToList();

    public static List<PairTimeDto> Dtos() =>
        Slots.Select(s => new PairTimeDto(s.Pair, $"{s.SH:00}:{s.SM:00}", $"{s.EH:00}:{s.EM:00}")).ToList();
}
