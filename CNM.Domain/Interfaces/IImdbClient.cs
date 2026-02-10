using CNM.Domain.Models;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace CNM.Domain.Interfaces
{
    public interface IImdbClient
    {
        Task<bool> PingAsync();
        Task<ImdbTitleResponse> GetByIdAsync(string imdbId, string apiKey);
    }
}
