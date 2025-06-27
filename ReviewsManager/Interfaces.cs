using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ReviewsManager;

internal sealed record GetOwnedGamesResponse {
    [JsonPropertyName("response")]
    public ResponseData? Response { get; set; }

    internal sealed record ResponseData {
        [JsonPropertyName("games")]
        public List<Game>? Games { get; set; }
    }

    internal sealed record Game {
        [JsonPropertyName("appid")]
        public uint AppId { get; set; }

        [JsonPropertyName("playtime_forever")]
        public uint PlayTimeForever { get; set; }
    }
}

public class AddReviewResponse {
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("strError")]
    public string? StrError { get; set; }
}

public class AddReviewsConfig {
    [JsonInclude]
    public string Comment { get; set; } = "⭐⭐⭐⭐⭐";

    [JsonInclude]
    public bool RatedUp { get; set; } = true;

    [JsonInclude]
    public bool IsPublic { get; set; } = true;

    [JsonInclude]
    public string Language { get; set; } = "english";

    [JsonInclude]
    public bool IsFree { get; set; }

    [JsonInclude]
    public bool AllowComments { get; set; } = true;

    [JsonConstructor]
    public AddReviewsConfig() { }
}
