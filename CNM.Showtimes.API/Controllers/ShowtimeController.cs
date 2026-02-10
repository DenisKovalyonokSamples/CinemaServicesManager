using CNM.Domain.Database; // keep this for DI registration
using DomainDb = CNM.Domain.Database;
using DomainEntities = CNM.Domain.Database.Entities;
using CNM.Showtimes.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace CNM.Showtimes.API.Controllers
{
    [ApiController]
    [Route("showtime")]
    public class ShowtimeController : ControllerBase
    {
        private readonly DomainDb.IShowtimesRepository _repo;
        private readonly IImdbClient _imdb;
        public ShowtimeController(DomainDb.IShowtimesRepository repo, IImdbClient imdb)
        {
            _repo = repo;
            _imdb = imdb;
        }

        [HttpGet]
        [Authorize(Policy = "Read")]
        public IActionResult Get([FromQuery] DateTime? date, [FromQuery] string title)
        {
            var list = _repo.GetCollection(q =>
            {
                var data = q;
                if (date.HasValue)
                {
                    data = data.Where(x => x.StartDate <= date.Value && x.EndDate >= date.Value);
                }
                if (!string.IsNullOrWhiteSpace(title))
                {
                    var term = title.Trim();
                    data = data.Where(x =>
                        x.Movie != null &&
                        x.Movie.Title != null &&
                        x.Movie.Title.Contains(term, StringComparison.OrdinalIgnoreCase));
                }
                return data.Any();
            });
            return Ok(list.ToList());
        }

        [HttpPost]
        [Authorize(Policy = "Write")]
        public async Task<IActionResult> Post([FromBody] DomainEntities.ShowtimeEntity payload, [FromQuery] string imdbApiKey)
        {
            if (payload?.Movie == null || string.IsNullOrWhiteSpace(payload.Movie.ImdbId))
                return BadRequest("movie.imdb_id required");
            if (string.IsNullOrWhiteSpace(imdbApiKey))
                return BadRequest("imdb_api_key required");

            var imdb = await _imdb.GetByIdAsync(payload.Movie.ImdbId, imdbApiKey);
            payload.Movie.Title = imdb?.title ?? payload.Movie.Title;
            payload.Movie.Stars = imdb?.stars ?? payload.Movie.Stars;
            if (DateTime.TryParse(imdb?.releaseDate, out var rd)) payload.Movie.ReleaseDate = rd;

            var created = _repo.Add(payload);
            return Created($"/showtime/{created.Id}", created);
        }

        [HttpPut]
        [Authorize(Policy = "Write")]
        public async Task<IActionResult> Put([FromBody] DomainEntities.ShowtimeEntity payload, [FromQuery] string imdbApiKey)
        {
            if (payload?.Movie != null && !string.IsNullOrWhiteSpace(payload.Movie.ImdbId) && !string.IsNullOrWhiteSpace(imdbApiKey))
            {
                var imdb = await _imdb.GetByIdAsync(payload.Movie.ImdbId, imdbApiKey);
                payload.Movie.Title = imdb?.title ?? payload.Movie.Title;
                payload.Movie.Stars = imdb?.stars ?? payload.Movie.Stars;
                if (DateTime.TryParse(imdb?.releaseDate, out var rd)) payload.Movie.ReleaseDate = rd;
            }
            var updated = _repo.Update(payload);
            if (updated == null) return NotFound();
            return Ok(updated);
        }

        [HttpDelete("{id}")]
        [Authorize(Policy = "Write")]
        public IActionResult Delete(int id)
        {
            var removed = _repo.Delete(id);
            if (removed == null) return NotFound();
            return NoContent();
        }

        [HttpPatch]
        public IActionResult Patch()
        {
            throw new Exception("Test error");
        }
    }
}
