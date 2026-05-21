using Microsoft.EntityFrameworkCore;
using UniScheduler.Domain.Entities;
using UniScheduler.Domain.Enums;

namespace UniScheduler.Infrastructure.Persistence;

public static class DbSeeder
{
    private static readonly Guid DefaultUniversityId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    public static async Task SeedAsync(ApplicationDbContext db)
    {
        // Seed default university
        if (!await db.Universities.AnyAsync())
        {
            db.Universities.Add(new University
            {
                Id = DefaultUniversityId,
                Name = "Университет",
                ShortName = "УН"
            });
            await db.SaveChangesAsync();
        }

        // Seed default admin user
        if (!await db.AppUsers.AnyAsync())
        {
            var admin = new AppUser
            {
                Username = "admin",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin123"),
                Role = "SuperAdmin"
            };
            db.AppUsers.Add(admin);
            await db.SaveChangesAsync();

            db.UserUniversityAccesses.Add(new UserUniversityAccess
            {
                UserId = admin.Id,
                UniversityId = DefaultUniversityId,
                Role = UniversityRole.Admin
            });
            await db.SaveChangesAsync();
        }

        // Seed default pair time slots for the default university
        if (!await db.PairTimeSlots.AnyAsync())
        {
            db.PairTimeSlots.AddRange(
                new PairTimeSlot { UniversityId = DefaultUniversityId, PairNumber = 1, StartTime = new TimeOnly(8,  0), EndTime = new TimeOnly(9,  35) },
                new PairTimeSlot { UniversityId = DefaultUniversityId, PairNumber = 2, StartTime = new TimeOnly(9, 50), EndTime = new TimeOnly(11, 25) },
                new PairTimeSlot { UniversityId = DefaultUniversityId, PairNumber = 3, StartTime = new TimeOnly(11, 40), EndTime = new TimeOnly(13, 15) },
                new PairTimeSlot { UniversityId = DefaultUniversityId, PairNumber = 4, StartTime = new TimeOnly(13, 45), EndTime = new TimeOnly(15, 20) },
                new PairTimeSlot { UniversityId = DefaultUniversityId, PairNumber = 5, StartTime = new TimeOnly(15, 35), EndTime = new TimeOnly(17, 10) },
                new PairTimeSlot { UniversityId = DefaultUniversityId, PairNumber = 6, StartTime = new TimeOnly(17, 25), EndTime = new TimeOnly(19,  0) },
                new PairTimeSlot { UniversityId = DefaultUniversityId, PairNumber = 7, StartTime = new TimeOnly(19, 15), EndTime = new TimeOnly(20, 50) }
            );
            await db.SaveChangesAsync();
        }

        // Seed default solver settings for the default university
        if (!await db.SolverSettings.AnyAsync())
        {
            db.SolverSettings.Add(new SolverSettings { UniversityId = DefaultUniversityId });
            await db.SaveChangesAsync();
        }
    }
}
