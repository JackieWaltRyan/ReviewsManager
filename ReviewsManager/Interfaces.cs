using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ReviewsManager;

public class Game {
    [JsonPropertyName("appid")]
    public uint AppId { get; set; }

    [JsonPropertyName("playtime_forever")]
    public int PlaytimeForever { get; set; }
}

public class Response {
    [JsonPropertyName("game_count")]
    public int GameCount { get; set; }

    [JsonPropertyName("games")]
    public List<Game> Games { get; set; }
}

public class GetOwnedGamesResponse {
    [JsonPropertyName("response")]
    public Response Response { get; set; }
}

public class AddReviewResponse {
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("strError")]
    public string StrError { get; set; }
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
    public bool IsFree { get; set; } = false;

    [JsonInclude]
    public bool AllowComments { get; set; } = true;

    [JsonConstructor]
    public AddReviewsConfig() { }
}
