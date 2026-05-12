using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniScheduler.Domain.Entities;

namespace UniScheduler.Infrastructure.Persistence.Configurations;

public class PairTimeSlotConfiguration : IEntityTypeConfiguration<PairTimeSlot>
{
    public void Configure(EntityTypeBuilder<PairTimeSlot> builder)
    {
        builder.HasKey(p => p.PairNumber);
        builder.Property(p => p.StartTime).IsRequired();
        builder.Property(p => p.EndTime).IsRequired();

        builder.HasData(
            new PairTimeSlot { PairNumber = 1, StartTime = new TimeOnly(8, 0),   EndTime = new TimeOnly(9, 35)  },
            new PairTimeSlot { PairNumber = 2, StartTime = new TimeOnly(9, 50),  EndTime = new TimeOnly(11, 25) },
            new PairTimeSlot { PairNumber = 3, StartTime = new TimeOnly(11, 40), EndTime = new TimeOnly(13, 15) },
            new PairTimeSlot { PairNumber = 4, StartTime = new TimeOnly(13, 45), EndTime = new TimeOnly(15, 20) },
            new PairTimeSlot { PairNumber = 5, StartTime = new TimeOnly(15, 35), EndTime = new TimeOnly(17, 10) },
            new PairTimeSlot { PairNumber = 6, StartTime = new TimeOnly(17, 25), EndTime = new TimeOnly(19, 0)  },
            new PairTimeSlot { PairNumber = 7, StartTime = new TimeOnly(19, 15), EndTime = new TimeOnly(20, 50) }
        );
    }
}
