using CNM.Domain.Database.Entities;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CNM.Domain.Interfaces
{
    public interface IShowtimesRepository
    {
        IEnumerable<ShowtimeEntity> GetCollection();
        IEnumerable<ShowtimeEntity> GetCollection(Func<ShowtimeEntity, bool> predicate);
        ShowtimeEntity GetByMovie(Func<MovieEntity, bool> predicate);
        ShowtimeEntity Add(ShowtimeEntity showtimeEntity);
        ShowtimeEntity Update(ShowtimeEntity showtimeEntity);
        ShowtimeEntity Delete(int id);
    }
}
