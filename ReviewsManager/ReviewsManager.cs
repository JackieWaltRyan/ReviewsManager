using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AngleSharp.Dom;
using ArchiSteamFarm.Core;
using ArchiSteamFarm.Helpers.Json;
using ArchiSteamFarm.Plugins.Interfaces;
using ArchiSteamFarm.Steam;
using ArchiSteamFarm.Steam.Integration;
using ArchiSteamFarm.Web.Responses;

namespace ReviewsManager;

internal sealed partial class ReviewsManager : IGitHubPluginUpdates, IBotModules {
    public string Name => nameof(ReviewsManager);
    public string RepositoryName => "JackieWaltRyan/ReviewsManager";
    public Version Version => typeof(ReviewsManager).Assembly.GetName().Version ?? throw new InvalidOperationException(nameof(Version));

    public Dictionary<string, bool> AddEnable = new();
    public Dictionary<string, bool> DelEnable = new();

    public Dictionary<string, AddReviewsConfig> AddReviewsConfig = new();

    public Dictionary<string, Timer> GetTimers = new();
    public Dictionary<string, Timer> AddTimers = new();
    public Dictionary<string, Timer> DelTimers = new();

    public Task OnLoaded() => Task.CompletedTask;

    public Task OnBotInitModules(Bot bot, IReadOnlyDictionary<string, JsonElement>? additionalConfigProperties = null) {
        if (additionalConfigProperties != null) {
            AddEnable[bot.BotName] = false;
            DelEnable[bot.BotName] = false;

            AddReviewsConfig[bot.BotName] = new AddReviewsConfig();

            GetTimers[bot.BotName] = new Timer(async e => await GetAllReviews(bot).ConfigureAwait(false), null, Timeout.Infinite, Timeout.Infinite);

            foreach (KeyValuePair<string, JsonElement> configProperty in additionalConfigProperties) {
                switch (configProperty.Key) {
                    case "AddMissingReviews" when configProperty.Value.ValueKind is JsonValueKind.True or JsonValueKind.False: {
                        bool isEnabled = configProperty.Value.GetBoolean();

                        bot.ArchiLogger.LogGenericInfo($"AddMissingReviews: {isEnabled}");

                        AddEnable[bot.BotName] = isEnabled;

                        break;
                    }

                    case "AddReviewsConfig": {
                        AddReviewsConfig? config = configProperty.Value.ToJsonObject<AddReviewsConfig>();

                        if (config != null) {
                            AddReviewsConfig[bot.BotName] = config;
                        }

                        break;
                    }

                    case "DelMissingReviews" when configProperty.Value.ValueKind is JsonValueKind.True or JsonValueKind.False: {
                        bool isEnabled = configProperty.Value.GetBoolean();

                        bot.ArchiLogger.LogGenericInfo($"DelMissingReviews: {isEnabled}");

                        DelEnable[bot.BotName] = isEnabled;

                        break;
                    }
                }
            }

            if (AddEnable[bot.BotName] || DelEnable[bot.BotName]) {
                bot.ArchiLogger.LogGenericInfo($"AddReviewsConfig: {AddReviewsConfig[bot.BotName].ToJsonText()}");

                GetTimers[bot.BotName].Change(1, -1);
            }
        }

        return Task.CompletedTask;
    }

    [GeneratedRegex("https://steamcommunity\\.com/app/(?<subID>\\d+)", RegexOptions.CultureInvariant)]
    private static partial Regex ExistingReviewsRegex();

    public static async Task<List<uint>> LoadingExistingReviews(Bot bot, int page = 1) {
        try {
            List<uint> reviewList = [];

            bot.ArchiLogger.LogGenericInfo($"Checking existing reviews: Page {page}");

            HtmlDocumentResponse? rawResponse = await bot.ArchiWebHandler.UrlGetToHtmlDocumentWithSession(new Uri($"{ArchiWebHandler.SteamCommunityURL}/profiles/{bot.SteamID}/recommended/?p={page}")).ConfigureAwait(false);

            IDocument? response = rawResponse?.Content;

            if (response != null) {
                Regex existingReviewsRegex = ExistingReviewsRegex();
                MatchCollection existingReviewsMatches = existingReviewsRegex.Matches(response.Source.Text);

                if (existingReviewsMatches.Count > 0) {
                    foreach (Match match in existingReviewsMatches) {
                        if (uint.TryParse(match.Groups["subID"].Value, out uint subID)) {
                            reviewList.Add(subID);
                        }
                    }

                    List<uint> newReviewList = await LoadingExistingReviews(bot, page + 1).ConfigureAwait(false);

                    reviewList.AddRange(newReviewList);
                }
            } else {
                await Task.Delay(3000).ConfigureAwait(false);

                await LoadingExistingReviews(bot, page).ConfigureAwait(false);
            }

            return reviewList;
        } catch {
            await Task.Delay(3000).ConfigureAwait(false);

            return await LoadingExistingReviews(bot, page).ConfigureAwait(false);
        }
    }

    public async Task GetAllReviews(Bot bot) {
        const uint timeout = 1;

        if (bot.IsConnectedAndLoggedOn) {
            List<uint> addData = [];
            List<uint> delData = [];

            AddTimers[bot.BotName] = new Timer(async e => await AddReviews(bot, addData).ConfigureAwait(false), null, Timeout.Infinite, Timeout.Infinite);
            DelTimers[bot.BotName] = new Timer(async e => await DelReviews(bot, delData).ConfigureAwait(false), null, Timeout.Infinite, Timeout.Infinite);

            ObjectResponse<JsonElement>? rawResponse = await bot.ArchiWebHandler.UrlGetToJsonObjectWithSession<JsonElement>(new Uri($"https://api.steampowered.com/IPlayerService/GetOwnedGames/v1/?access_token={bot.AccessToken}&steamid={bot.SteamID}&include_played_free_games=true&skip_unvetted_apps=false")).ConfigureAwait(false);

            JsonElement? response = rawResponse?.Content;

            bot.ArchiLogger.LogGenericInfo(response.ToJsonText());

            GetOwnedGamesResponse? myDeserializedClass = JsonSerializer.Deserialize<GetOwnedGamesResponse>(response.ToJsonText());

            bot.ArchiLogger.LogGenericInfo(myDeserializedClass.ToJsonText());

            // List<Game>? games = response?.Response?.Games;
            //
            //
            // if (games != null) {
            //     if (games.Count > 0) {
            //         bot.ArchiLogger.LogGenericInfo($"Total games found: {games.Count}");
            //
            //         List<uint> reviews = await LoadingExistingReviews(bot).ConfigureAwait(false);
            //
            //         bot.ArchiLogger.LogGenericInfo($"Existing reviews found: {reviews.Count}");
            //
            //         List<uint> gamesIDs = [];
            //
            //         foreach (Game game in games) {
            //             gamesIDs.Add(game.AppId);
            //
            //             if ((game.PlaytimeForever >= 5) && !reviews.Contains(game.AppId)) {
            //                 addData.Add(game.AppId);
            //             }
            //         }
            //
            //         foreach (uint subID in reviews) {
            //             if (!gamesIDs.Contains(subID)) {
            //                 delData.Add(subID);
            //             }
            //         }
            //
            //         if (AddEnable[bot.BotName] && (addData.Count > 0)) {
            //             bot.ArchiLogger.LogGenericInfo($"Add reviews found: {addData.Count}");
            //
            //             AddTimers[bot.BotName].Change(1, -1);
            //         }
            //
            //         if (DelEnable[bot.BotName] && (delData.Count > 0)) {
            //             bot.ArchiLogger.LogGenericInfo($"Del reviews found: {delData.Count}");
            //
            //             DelTimers[bot.BotName].Change(1, -1);
            //         }
            //
            //         return;
            //     }
            // }
            //
            // bot.ArchiLogger.LogGenericInfo($"Status: Error | Next run: {DateTime.Now.AddMinutes(timeout):T}");
        } else {
            bot.ArchiLogger.LogGenericInfo($"Status: BotNotConnected | Next run: {DateTime.Now.AddMinutes(timeout):T}");
        }

        GetTimers[bot.BotName].Change(TimeSpan.FromMinutes(timeout), TimeSpan.FromMilliseconds(-1));
    }

    public async Task AddReviews(Bot bot, List<uint> addData) {
        uint timeout = 1;

        if (addData.Count > 0) {
            if (bot.IsConnectedAndLoggedOn) {
                uint gameId = addData[0];

                ObjectResponse<AddReviewResponse>? rawResponse = await bot.ArchiWebHandler.UrlPostToJsonObjectWithSession<AddReviewResponse>(
                    new Uri($"{ArchiWebHandler.SteamStoreURL}/friends/recommendgame?l=english"), data: new Dictionary<string, string>(9) {
                        { "appid", $"{gameId}" },
                        { "steamworksappid", $"{gameId}" },
                        { "comment", AddReviewsConfig[bot.BotName].Comment },
                        { "rated_up", AddReviewsConfig[bot.BotName].RatedUp.ToString() },
                        { "is_public", AddReviewsConfig[bot.BotName].IsPublic.ToString() },
                        { "language", bot.ArchiWebHandler.WebBrowser.CookieContainer.GetCookieValue(ArchiWebHandler.SteamStoreURL, "Steam_Language") ?? AddReviewsConfig[bot.BotName].Language },
                        { "received_compensation", AddReviewsConfig[bot.BotName].IsFree ? "1" : "0" },
                        { "disable_comments", AddReviewsConfig[bot.BotName].AllowComments ? "0" : "1" }
                    }, referer: new Uri($"{ArchiWebHandler.SteamStoreURL}/app/{gameId}")
                ).ConfigureAwait(false);

                AddReviewResponse? response = rawResponse?.Content;

                if (response != null) {
                    bot.ArchiLogger.LogGenericInfo(response.ToJsonText());

                    return;
                }

                bot.ArchiLogger.LogGenericInfo($"ID: {gameId} | Status: Error | Queue: {addData.Count} | Next run: {DateTime.Now.AddMinutes(timeout):T}");
            } else {
                bot.ArchiLogger.LogGenericInfo($"Status: BotNotConnected | Queue: {addData.Count} | Next run: {DateTime.Now.AddMinutes(timeout):T}");
            }

            AddTimers[bot.BotName].Change(TimeSpan.FromMinutes(timeout), TimeSpan.FromMilliseconds(-1));
        } else {
            timeout = 60;

            bot.ArchiLogger.LogGenericInfo($"Status: QueueIsEmpty | Queue: {addData.Count} | Next run: {DateTime.Now.AddMinutes(timeout):T}");

            GetTimers[bot.BotName].Change(TimeSpan.FromMinutes(timeout), TimeSpan.FromMilliseconds(-1));
        }
    }

    public async Task DelReviews(Bot bot, List<uint> delData) {
        uint timeout = 1;

        if (delData.Count > 0) {
            if (bot.IsConnectedAndLoggedOn) {
                uint gameId = delData[0];

                await bot.ArchiWebHandler.UrlPostWithSession(
                    new Uri($"{ArchiWebHandler.SteamCommunityURL}/profiles/{bot.SteamID}/recommended/"), data: new Dictionary<string, string>(9) {
                        { "action", "delete" },
                        { "appid", $"{gameId}" }
                    }, referer: new Uri($"{ArchiWebHandler.SteamCommunityURL}/profiles/{bot.SteamID}/recommended/{gameId}/")
                ).ConfigureAwait(false);

                delData.RemoveAt(0);

                bot.ArchiLogger.LogGenericInfo($"ID: {gameId} | Status: OK | Queue: {delData.Count}");

                DelTimers[bot.BotName].Change(TimeSpan.FromSeconds(3), TimeSpan.FromMilliseconds(-1));

                return;
            }

            bot.ArchiLogger.LogGenericInfo($"Status: BotNotConnected | Queue: {delData.Count} | Next run: {DateTime.Now.AddMinutes(timeout):T}");

            DelTimers[bot.BotName].Change(TimeSpan.FromMinutes(timeout), TimeSpan.FromMilliseconds(-1));
        } else {
            timeout = 60;

            bot.ArchiLogger.LogGenericInfo($"Status: QueueIsEmpty | Queue: {delData.Count} | Next run: {DateTime.Now.AddMinutes(timeout):T}");

            GetTimers[bot.BotName].Change(TimeSpan.FromMinutes(timeout), TimeSpan.FromMilliseconds(-1));
        }
    }
}
