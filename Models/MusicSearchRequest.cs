namespace SongFinder2_back.Models
{
    public class MusicSearchRequest
    {
        public string Mood { get; set; } = string.Empty;
        public string Genre { get; set; } = string.Empty;
        public string YearRange { get; set; } = string.Empty;
        public int Count { get; set; } = 10;
    }
}
