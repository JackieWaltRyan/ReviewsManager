using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text.Json.Serialization;

namespace ReviewsManager;

internal sealed record ReviewsManagerConfig {
    [JsonInclude]
    public bool AddReviews { get; set; }

    [JsonInclude]
    public ReviewsConfig AddReviewsConfig { get; set; } = new();

    internal sealed record ReviewsConfig {
        [JsonInclude]
        public string Comment { get; set; } = "⭐⭐⭐⭐⭐";

        [JsonInclude]
        public bool RatedUp { get; set; } = true;

        [JsonInclude]
        public bool IsPublic { get; set; } = true;

        [JsonInclude]
        public string Language { get; set; } = "auto";

        [JsonInclude]
        public bool IsFree { get; set; }

        [JsonInclude]
        public bool AllowComments { get; set; } = true;

        [JsonInclude]
        public Collection<uint> BlackList { get; } = [];

        [JsonConstructor]
        public ReviewsConfig() { }
    }

    [JsonInclude]
    public bool DelReviews { get; set; }

    [JsonInclude]
    public uint Timeout { get; set; } = 6;

    [JsonConstructor]
    public ReviewsManagerConfig() { }
}

internal sealed record GetOwnedGamesResponse {
    [JsonPropertyName("response")]
    public ResponseData? Response { get; set; }

    internal sealed record ResponseData {
        [JsonPropertyName("games")]
        public List<Game>? Games { get; set; }

        internal sealed record Game {
            [JsonPropertyName("appid")]
            public uint AppId { get; set; }

            [JsonPropertyName("playtime_forever")]
            public uint PlayTimeForever { get; set; }
        }
    }
}

internal sealed record AddReviewResponse {
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("strError")]
    public string? StrError { get; set; }
}
