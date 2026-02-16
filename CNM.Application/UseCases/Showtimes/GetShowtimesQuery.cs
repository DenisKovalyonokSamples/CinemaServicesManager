using System;
using System.Collections.Generic;
using CNM.Domain.Database.Entities;
using MediatR;

namespace CNM.Application.UseCases.Showtimes
{
    public class GetShowtimesQuery : IRequest<IEnumerable<ShowtimeEntity>>
    {
        public DateTime? Date { get; set; }
        public string Title { get; set; }
    }
}