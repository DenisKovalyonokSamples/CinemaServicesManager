namespace CNM.Domain.Database.Entities
{
    public class AuditoriumEntity
    {
        public int Id { get; set; }
        public System.Collections.Generic.List<ShowtimeEntity> Showtimes { get; set; }
        public int Seats { get; set; }
    }
}
