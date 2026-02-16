using System;
using CNM.Domain.Database;
using CNM.Domain.Database.Entities;

namespace CNM.Showtimes.Tests.Integration
{
    internal static class TestDataSeeder
    {
        public static void SeedShowtimes(DatabaseContext db)
        {
            db.Showtimes.Add(new ShowtimeEntity
            {
                // Id omitted to let the database auto-generate
                StartDate = new DateTime(2020, 1, 1),
                EndDate = new DateTime(2020, 1, 10),
                Movie = new MovieEntity { Title = "Seeded Movie", ImdbId = "ttseed" }
            });
            db.SaveChanges();
        }
    }
}
