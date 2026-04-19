using System.Collections.Generic;

namespace SongFinder2_back.Services
{
    public static class MoodMapper
    {
        public static List<string> GetSearchKeywords(string mood)
        {
            var m = mood.ToLowerInvariant().Trim();
            
            // Expanded vibe-based keywords to improve search accuracy without Audio Features.
            // We combine mood names with musical descriptors (instruments, vibes, etc.)
            return m switch
            {
                "весёлое" or "happy" or "upbeat" => new List<string> { "happy", "upbeat", "joyful", "party", "cheerful", "sunny", "positivity", "summer vibes", "радостное", "позитив", "праздник", "солнце" },
                "грустное" or "sad" or "melancholy" => new List<string> { "sad", "sorrow", "heartbreak", "lonely", "piano", "somber", "emotional", "crying", "грусть", "тоска", "печаль", "минор", "плач" },
                "энергичное" or "energetic" or "hype" => new List<string> { "energetic", "hype", "power", "pump up", "fast", "workout", "motivation", "intense", "драйв", "энергия", "сила", "мотивация", "быстро" },
                "спокойное" or "calm" or "chill" => new List<string> { "calm", "chill", "peaceful", "soft", "relaxing", "acoustic", "mellow", "serenity", "релакс", "уют", "тихо", "медитация", "спокойствие" },
                "меланхоличное" or "melancholic" or "moody" => new List<string> { "melancholic", "nostalgic", "moody", "atmospheric", "rainy", "slow", "dreamy", "deep", "ностальгия", "атмосферно", "дождь", "мечтательно" },
                "танцевальное" or "dance" or "club" => new List<string> { "dance", "disco", "club", "party", "house", "groove", "rhythm", "beats", "танцы", "дискотека", "ритм", "клубняк" },
                "агрессивное" or "aggressive" or "angry" => new List<string> { "aggressive", "angry", "dark", "heavy", "distorted", "raw", "fierce", "rage", "агрессия", "злость", "тяжеляк", "ярость" },
                "романтичное" or "romantic" or "love" => new List<string> { "romantic", "love", "passion", "sweet", "ballad", "soul", "intimate", "gentle", "романтика", "любовь", "нежность", "свидание" },
                "фоновое" or "ambient" or "background" => new List<string> { "ambient", "study", "lo-fi", "focus", "instrumental", "minimal", "lounge", "calm", "фон", "учеба", "эмбиент", "инструментал" },
                _ => string.IsNullOrWhiteSpace(m) ? new List<string>() : new List<string> { m }
            };
        }
    }
}


// We combine mood names with musical descriptors (instruments, vibes, etc.)// We combine mood names with musical descriptors (instruments, vibes, etc.)
