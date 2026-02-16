using CNM.Domain.Database;
using CNM.Domain.Database.Entities;
using CNM.Domain.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CNM.Domain.Repositories
{
    public class ShowtimesRepository : IShowtimesRepository
    {
        private readonly DatabaseContext _dbContext;
        public ShowtimesRepository(DatabaseContext dbContext)
        {
            _dbContext = dbContext;
        }

        public ShowtimeEntity Add(ShowtimeEntity showtimeEntity)
        {
            _dbContext.Showtimes.Add(showtimeEntity);
            _dbContext.SaveChanges();
            return showtimeEntity;
        }

        public ShowtimeEntity Delete(int id)
        {
            var entity = _dbContext.Showtimes.FirstOrDefault(showtime => showtime.Id == id);
            if (entity == null) return null;
            _dbContext.Showtimes.Remove(entity);
            _dbContext.SaveChanges();
            return entity;
        }

        public ShowtimeEntity GetByMovie(Func<MovieEntity, bool> predicate)
        {
            var movie = _dbContext.Movies.FirstOrDefault(m => predicate == null || predicate(m));
            if (movie == null) return null;
            return _dbContext.Showtimes.FirstOrDefault(showtime => showtime.Movie != null && showtime.Movie.ShowtimeId == movie.ShowtimeId);
        }

        public IEnumerable<ShowtimeEntity> GetCollection()
        {
            return GetCollection(null);
        }

        public IEnumerable<ShowtimeEntity> GetCollection(Func<ShowtimeEntity, bool> predicate)
        {
            var queryableShowtimes = _dbContext.Showtimes.AsQueryable();
            return predicate == null ? queryableShowtimes.ToList() : queryableShowtimes.Where(predicate).ToList();
        }

        public ShowtimeEntity Update(ShowtimeEntity showtimeEntity)
        {
            var existingShowtime = _dbContext.Showtimes.FirstOrDefault(s => s.Id == showtimeEntity.Id);
            if (existingShowtime == null) return null;
            existingShowtime.StartDate = showtimeEntity.StartDate;
            existingShowtime.EndDate = showtimeEntity.EndDate;
            existingShowtime.Schedule = showtimeEntity.Schedule;
            existingShowtime.AuditoriumId = showtimeEntity.AuditoriumId;
            if (showtimeEntity.Movie != null)
            {
                var movie = _dbContext.Movies.FirstOrDefault(m => m.ShowtimeId == existingShowtime.Id);
                if (movie == null)
                {
                    movie = new MovieEntity { ShowtimeId = existingShowtime.Id };
                    _dbContext.Movies.Add(movie);
                }
                movie.Title = showtimeEntity.Movie.Title;
                movie.ImdbId = showtimeEntity.Movie.ImdbId;
                movie.Stars = showtimeEntity.Movie.Stars;
                movie.ReleaseDate = showtimeEntity.Movie.ReleaseDate;
                existingShowtime.Movie = movie;
            }
            _dbContext.SaveChanges();
            return existingShowtime;
        }
    }
}
