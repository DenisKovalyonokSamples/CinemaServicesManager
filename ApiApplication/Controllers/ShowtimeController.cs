using CNM.Application.Database;
using CNM.Application.Database.Entities;
using CNM.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace CNM.Application.Controllers
{
    [ApiController]
    [Route("showtime")]
    public class ShowtimeController : ControllerBase
    {
        private readonly IShowtimesRepository _repo;
        private readonly IImdbClient _imdb;
        public ShowtimeController(IShowtimesRepository repo, IImdbClient imdb) // Added: DI constructor
        {
            _repo = repo;
            _imdb = imdb;
        }

        [HttpGet]
        [Authorize(Policy = "Read")] // Added: enforce Read policy
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
                    data = data.Where(x => x.Movie != null && x.Movie.Title.Contains(title));
                }
                return data.Any();
            });
            return Ok(list);
        }

        [HttpPost]
        [Authorize(Policy = "Write")] // Added: enforce Write policy
        public async Task<IActionResult> Post([FromBody] ShowtimeEntity payload, [FromQuery] string imdbApiKey)
        {
            if (payload?.Movie == null || string.IsNullOrWhiteSpace(payload.Movie.ImdbId))
                return BadRequest("movie.imdb_id required");
            var imdb = await _imdb.GetByIdAsync(payload.Movie.ImdbId, imdbApiKey);
            payload.Movie.Title = imdb?.title;
            payload.Movie.Stars = imdb?.stars;
            if (DateTime.TryParse(imdb?.releaseDate, out var rd)) payload.Movie.ReleaseDate = rd;
            var created = _repo.Add(payload);
            return Created($"/showtime/{created.Id}", created);
        }

        [HttpPut]
        [Authorize(Policy = "Write")] // Added: enforce Write policy
        public async Task<IActionResult> Put([FromBody] ShowtimeEntity payload, [FromQuery] string imdbApiKey)
        {
            if (payload.Movie != null && !string.IsNullOrWhiteSpace(payload.Movie.ImdbId))
            {
                var imdb = await _imdb.GetByIdAsync(payload.Movie.ImdbId, imdbApiKey);
                payload.Movie.Title = imdb?.title;
                payload.Movie.Stars = imdb?.stars;
                if (DateTime.TryParse(imdb?.releaseDate, out var rd)) payload.Movie.ReleaseDate = rd;
            }
            var updated = _repo.Update(payload);
            if (updated == null) return NotFound();
            return Ok(updated);
        }

        [HttpDelete("{id}")]
        [Authorize(Policy = "Write")] // Added: enforce Write policy
        public IActionResult Delete(int id)
        {
            var removed = _repo.Delete(id);
            if (removed == null) return NotFound();
            return NoContent();
        }

        [HttpPatch]
        public IActionResult Patch() // Added: test endpoint to return 500
        {
            throw new Exception("Test error");
        }
    }
}
