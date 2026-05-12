using Microsoft.EntityFrameworkCore;
using UniScheduler.Domain.Entities;

namespace UniScheduler.Infrastructure.Persistence;

public static class DbSeeder
{
    public static async Task SeedAsync(ApplicationDbContext db)
    {
        // Seed default admin user if none exists
        if (!await db.AppUsers.AnyAsync())
        {
            db.AppUsers.Add(new AppUser
            {
                Username = "admin",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin123"),
                Role = "Admin"
            });
            await db.SaveChangesAsync();
        }
    }
}
