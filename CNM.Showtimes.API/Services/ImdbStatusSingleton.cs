using System.Threading;

namespace CNM.Showtimes.API.Services
{
    public class ImdbStatusSingleton
    {
        private int _statusChecks;
        public int StatusChecks => _statusChecks;
        public void Increment() => Interlocked.Increment(ref _statusChecks);
    }
}
