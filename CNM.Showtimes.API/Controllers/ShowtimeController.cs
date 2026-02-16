using CNM.Domain.Database; // keep this for DI registration
using CNM.Domain.Repositories;
using DomainDb = CNM.Domain.Interfaces;
using DomainEntities = CNM.Domain.Database.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Linq;
using System.Threading.Tasks;
using CNM.Domain.Interfaces;

namespace CNM.Showtimes.API.Controllers
{
    [ApiController]
    [Route("showtime")]
    public class ShowtimeController : ControllerBase
    {
        private readonly DomainDb.IShowtimesRepository _showtimesRepository;
        private readonly IImdbClient _imdbClient;
        public ShowtimeController(DomainDb.IShowtimesRepository showtimesRepository, IImdbClient imdbClient)
        {
            _showtimesRepository = showtimesRepository;
            _imdbClient = imdbClient;
        }

        [HttpGet]
        [Authorize(Policy = "Read")]
        public IActionResult Get([FromQuery] DateTime? date, [FromQuery] string title)
        {
            var titleTerm = title?.Trim();
            var showtimes = _showtimesRepository.GetCollection(showtime =>
                (!date.HasValue || (showtime.StartDate <= date.Value && showtime.EndDate >= date.Value)) &&
                (string.IsNullOrWhiteSpace(titleTerm) || (showtime.Movie != null && showtime.Movie.Title != null && showtime.Movie.Title.Contains(titleTerm, StringComparison.OrdinalIgnoreCase)))
            );
            return Ok(showtimes.ToList());
        }

        [HttpPost]
        [Authorize(Policy = "Write")]
        public async Task<IActionResult> Post([FromBody] DomainEntities.ShowtimeEntity payload, [FromQuery] string imdbApiKey)
        {
            if (payload?.Movie == null || string.IsNullOrWhiteSpace(payload.Movie.ImdbId))
                return BadRequest("movie.imdb_id required");
            if (string.IsNullOrWhiteSpace(imdbApiKey))
                return BadRequest("imdb_api_key required");

            var imdbTitle = await _imdbClient.GetByIdAsync(payload.Movie.ImdbId, imdbApiKey);
            payload.Movie.Title = imdbTitle?.title ?? payload.Movie.Title;
            payload.Movie.Stars = imdbTitle?.stars ?? payload.Movie.Stars;
            if (DateTime.TryParse(imdbTitle?.releaseDate, out var parsedReleaseDate)) payload.Movie.ReleaseDate = parsedReleaseDate;

            var createdShowtime = _showtimesRepository.Add(payload);
            return Created($"/showtime/{createdShowtime.Id}", createdShowtime);
        }

        [HttpPut]
        [Authorize(Policy = "Write")]
        public async Task<IActionResult> Put([FromBody] DomainEntities.ShowtimeEntity payload, [FromQuery] string imdbApiKey)
        {
            if (payload?.Movie != null && !string.IsNullOrWhiteSpace(payload.Movie.ImdbId) && !string.IsNullOrWhiteSpace(imdbApiKey))
            {
                var imdbTitle = await _imdbClient.GetByIdAsync(payload.Movie.ImdbId, imdbApiKey);
                payload.Movie.Title = imdbTitle?.title ?? payload.Movie.Title;
                payload.Movie.Stars = imdbTitle?.stars ?? payload.Movie.Stars;
                if (DateTime.TryParse(imdbTitle?.releaseDate, out var parsedReleaseDate)) payload.Movie.ReleaseDate = parsedReleaseDate;
            }
            var updatedShowtime = _showtimesRepository.Update(payload);
            if (updatedShowtime == null) return NotFound();
            return Ok(updatedShowtime);
        }

        [HttpDelete("{id}")]
        [Authorize(Policy = "Write")]
        public IActionResult Delete(int id)
        {
            var deletedShowtime = _showtimesRepository.Delete(id);
            if (deletedShowtime == null) return NotFound();
            return NoContent();
        }

        [HttpPatch]
        public IActionResult Patch()
        {
            throw new Exception("Test error");
        }
    }
}
