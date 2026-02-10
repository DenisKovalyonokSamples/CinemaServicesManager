using CNM.Domain.Database.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;

namespace CNM.Domain.Database
{
    public class ShowtimesRepository : IShowtimesRepository
    {
        private readonly DatabaseContext _context;
        public ShowtimesRepository(DatabaseContext context)
        {
            _context = context;
        }

        public ShowtimeEntity Add(ShowtimeEntity showtimeEntity)
        {
            _context.Showtimes.Add(showtimeEntity);
            _context.SaveChanges();
            return showtimeEntity;
        }

        public ShowtimeEntity Delete(int id)
        {
            var entity = _context.Showtimes.FirstOrDefault(x => x.Id == id);
            if (entity == null) return null;
            _context.Showtimes.Remove(entity);
            _context.SaveChanges();
            return entity;
        }

        public ShowtimeEntity GetByMovie(Func<IQueryable<MovieEntity>, bool> filter)
        {
            var query = _context.Movies.AsQueryable();
            if (filter != null && !filter(query)) return null;
            return _context.Showtimes.FirstOrDefault(x => x.Movie != null);
        }

        public IEnumerable<ShowtimeEntity> GetCollection()
        {
            return GetCollection(null);
        }

        public IEnumerable<ShowtimeEntity> GetCollection(Func<IQueryable<ShowtimeEntity>, bool> filter)
        {
            var query = _context.Showtimes.AsQueryable();
            if (filter == null) return query.ToList();
            var ok = filter(query);
            return ok ? query.ToList() : Enumerable.Empty<ShowtimeEntity>();
        }

        public ShowtimeEntity Update(ShowtimeEntity showtimeEntity)
        {
            var existing = _context.Showtimes.FirstOrDefault(x => x.Id == showtimeEntity.Id);
            if (existing == null) return null;
            existing.StartDate = showtimeEntity.StartDate;
            existing.EndDate = showtimeEntity.EndDate;
            existing.Schedule = showtimeEntity.Schedule;
            existing.AuditoriumId = showtimeEntity.AuditoriumId;
            if (showtimeEntity.Movie != null)
            {
                var movie = _context.Movies.FirstOrDefault(m => m.ShowtimeId == existing.Id);
                if (movie == null)
                {
                    movie = new MovieEntity { ShowtimeId = existing.Id };
                    _context.Movies.Add(movie);
                }
                movie.Title = showtimeEntity.Movie.Title;
                movie.ImdbId = showtimeEntity.Movie.ImdbId;
                movie.Stars = showtimeEntity.Movie.Stars;
                movie.ReleaseDate = showtimeEntity.Movie.ReleaseDate;
                existing.Movie = movie;
            }
            _context.SaveChanges();
            return existing;
        }
    }
}
