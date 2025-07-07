using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
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

    public Dictionary<string, ReviewsManagerConfig> ReviewsManagerConfig = new();
    public Dictionary<string, Dictionary<string, Timer>> ReviewsManagerTimers = new();

    public Task OnLoaded() => Task.CompletedTask;

    public async Task OnBotInitModules(Bot bot, IReadOnlyDictionary<string, JsonElement>? additionalConfigProperties = null) {
        if (additionalConfigProperties != null) {
            if (ReviewsManagerTimers.TryGetValue(bot.BotName, out Dictionary<string, Timer>? dict)) {
                foreach (KeyValuePair<string, Timer> timers in dict) {
                    switch (timers.Key) {
                        case "GetAllReviews": {
                            await timers.Value.DisposeAsync().ConfigureAwait(false);

                            bot.ArchiLogger.LogGenericInfo("GetAllReviews Dispose.");

                            break;
                        }

                        case "AddReviews": {
                            await timers.Value.DisposeAsync().ConfigureAwait(false);

                            bot.ArchiLogger.LogGenericInfo("AddReviews Dispose.");

                            break;
                        }

                        case "DelReviews": {
                            await timers.Value.DisposeAsync().ConfigureAwait(false);

                            bot.ArchiLogger.LogGenericInfo("DelReviews Dispose.");

                            break;
                        }
                    }
                }
            }

            ReviewsManagerTimers[bot.BotName] = new Dictionary<string, Timer> {
                { "GetAllReviews", new Timer(async e => await GetAllReviews(bot).ConfigureAwait(false), null, Timeout.Infinite, Timeout.Infinite) }
            };

            ReviewsManagerConfig[bot.BotName] = new ReviewsManagerConfig();

            foreach (KeyValuePair<string, JsonElement> configProperty in additionalConfigProperties) {
                switch (configProperty.Key) {
                    case "ReviewsManagerConfig": {
                        ReviewsManagerConfig? config = configProperty.Value.ToJsonObject<ReviewsManagerConfig>();

                        if (config != null) {
                            ReviewsManagerConfig[bot.BotName] = config;
                        }

                        break;
                    }
                }
            }

            if (ReviewsManagerConfig[bot.BotName].AddReviews || ReviewsManagerConfig[bot.BotName].DelReviews) {
                bot.ArchiLogger.LogGenericInfo($"ReviewsManagerConfig: {ReviewsManagerConfig[bot.BotName].ToJsonText()}");

                ReviewsManagerTimers[bot.BotName]["GetAllReviews"].Change(1, -1);
            }
        }
    }

    [GeneratedRegex("""https://steamcommunity\.com/app/(?<subID>\d+)""", RegexOptions.CultureInvariant)]
    private static partial Regex ExistingReviewsRegex();

    public async Task<List<uint>> LoadingExistingReviews(Bot bot, uint page = 1) {
        try {
            List<uint> reviewList = [];

            if (!bot.IsConnectedAndLoggedOn || !ReviewsManagerTimers[bot.BotName].ContainsKey("GetAllReviews")) {
                return reviewList;
            }

            bot.ArchiLogger.LogGenericInfo($"Checking existing reviews: Page {page}");

            HtmlDocumentResponse? rawResponse = await bot.ArchiWebHandler.UrlGetToHtmlDocumentWithSession(new Uri($"{ArchiWebHandler.SteamCommunityURL}/profiles/{bot.SteamID}/recommended/?p={page}")).ConfigureAwait(false);

            string? response = rawResponse?.Content?.Source?.Text;

            if (response != null) {
                MatchCollection existingReviewsMatches = ExistingReviewsRegex().Matches(response);

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

    [GeneratedRegex("""g_strCurrentLanguage = "(?<languageID>\w+)";""", RegexOptions.CultureInvariant)]
    private static partial Regex GetLanguageRegex();

    public async Task GetAllReviews(Bot bot) {
        if (bot.IsConnectedAndLoggedOn) {
            List<uint> addData = [];
            List<uint> delData = [];

            ReviewsManagerTimers[bot.BotName]["AddReviews"] = new Timer(async e => await AddReviews(bot, addData).ConfigureAwait(false), null, Timeout.Infinite, Timeout.Infinite);
            ReviewsManagerTimers[bot.BotName]["DelReviews"] = new Timer(async e => await DelReviews(bot, delData).ConfigureAwait(false), null, Timeout.Infinite, Timeout.Infinite);

            ObjectResponse<GetOwnedGamesResponse>? rawResponse = await bot.ArchiWebHandler.UrlGetToJsonObjectWithSession<GetOwnedGamesResponse>(new Uri($"https://api.steampowered.com/IPlayerService/GetOwnedGames/v1/?access_token={bot.AccessToken}&steamid={bot.SteamID}&include_played_free_games=true&skip_unvetted_apps=false")).ConfigureAwait(false);

            List<GetOwnedGamesResponse.ResponseData.Game>? games = rawResponse?.Content?.Response?.Games;

            if (games != null) {
                bot.ArchiLogger.LogGenericInfo($"Total games found: {games.Count}");

                if (games.Count > 0) {
                    List<uint> reviews = await LoadingExistingReviews(bot).ConfigureAwait(false);

                    bot.ArchiLogger.LogGenericInfo($"Existing reviews found: {reviews.Count}");

                    List<uint> gamesIDs = [];

                    foreach (GetOwnedGamesResponse.ResponseData.Game game in games) {
                        gamesIDs.Add(game.AppId);

                        if ((game.PlayTimeForever >= 5) && !reviews.Contains(game.AppId) && !ReviewsManagerConfig[bot.BotName].AddReviewsConfig.BlackList.Contains(game.AppId)) {
                            addData.Add(game.AppId);
                        }
                    }

                    foreach (uint subID in reviews) {
                        if (!gamesIDs.Contains(subID)) {
                            delData.Add(subID);
                        }
                    }

                    if (ReviewsManagerConfig[bot.BotName].AddReviews) {
                        if (ReviewsManagerConfig[bot.BotName].AddReviewsConfig.Language == "auto") {
                            try {
                                HtmlDocumentResponse? rawLanguageResponse = await bot.ArchiWebHandler.UrlGetToHtmlDocumentWithSession(new Uri($"{ArchiWebHandler.SteamStoreURL}/account/languagepreferences")).ConfigureAwait(false);

                                string? languageResponse = rawLanguageResponse?.Content?.Source.Text;

                                if (languageResponse != null) {
                                    MatchCollection languageMatches = GetLanguageRegex().Matches(languageResponse);

                                    if (languageMatches.Count > 0) {
                                        ReviewsManagerConfig[bot.BotName].AddReviewsConfig.Language = languageMatches[0].Groups["languageID"].Value;
                                    } else {
                                        ReviewsManagerConfig[bot.BotName].AddReviewsConfig.Language = bot.ArchiWebHandler.WebBrowser.CookieContainer.GetCookieValue(ArchiWebHandler.SteamStoreURL, "Steam_Language") ?? "english";
                                    }
                                } else {
                                    ReviewsManagerConfig[bot.BotName].AddReviewsConfig.Language = bot.ArchiWebHandler.WebBrowser.CookieContainer.GetCookieValue(ArchiWebHandler.SteamStoreURL, "Steam_Language") ?? "english";
                                }
                            } catch {
                                ReviewsManagerConfig[bot.BotName].AddReviewsConfig.Language = bot.ArchiWebHandler.WebBrowser.CookieContainer.GetCookieValue(ArchiWebHandler.SteamStoreURL, "Steam_Language") ?? "english";
                            }
                        }

                        bot.ArchiLogger.LogGenericInfo($"Add reviews found: {addData.Count}");

                        ReviewsManagerTimers[bot.BotName]["AddReviews"].Change(1, -1);
                    }

                    if (ReviewsManagerConfig[bot.BotName].DelReviews) {
                        bot.ArchiLogger.LogGenericInfo($"Del reviews found: {delData.Count}");

                        ReviewsManagerTimers[bot.BotName]["DelReviews"].Change(1, -1);
                    }

                    return;
                }

                bot.ArchiLogger.LogGenericInfo($"Status: GameListIsEmpty | Next run: {DateTime.Now.AddHours(ReviewsManagerConfig[bot.BotName].Timeout):T}");

                ReviewsManagerTimers[bot.BotName]["GetAllReviews"].Change(TimeSpan.FromHours(ReviewsManagerConfig[bot.BotName].Timeout), TimeSpan.FromMilliseconds(-1));

                return;
            }

            bot.ArchiLogger.LogGenericInfo($"Status: Error | Next run: {DateTime.Now.AddMinutes(1):T}");
        } else {
            bot.ArchiLogger.LogGenericInfo($"Status: BotNotConnected | Next run: {DateTime.Now.AddMinutes(1):T}");
        }

        ReviewsManagerTimers[bot.BotName]["GetAllReviews"].Change(TimeSpan.FromMinutes(1), TimeSpan.FromMilliseconds(-1));
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
                        { "comment", ReviewsManagerConfig[bot.BotName].AddReviewsConfig.Comment },
                        { "rated_up", ReviewsManagerConfig[bot.BotName].AddReviewsConfig.RatedUp.ToString() },
                        { "is_public", ReviewsManagerConfig[bot.BotName].AddReviewsConfig.IsPublic.ToString() },
                        { "language", ReviewsManagerConfig[bot.BotName].AddReviewsConfig.Language },
                        { "received_compensation", ReviewsManagerConfig[bot.BotName].AddReviewsConfig.IsFree ? "1" : "0" },
                        { "disable_comments", ReviewsManagerConfig[bot.BotName].AddReviewsConfig.AllowComments ? "0" : "1" }
                    }, referer: new Uri($"{ArchiWebHandler.SteamStoreURL}/app/{gameId}")
                ).ConfigureAwait(false);

                AddReviewResponse? response = rawResponse?.Content;

                if (response != null) {
                    if (response.Success) {
                        addData.RemoveAt(0);

                        bot.ArchiLogger.LogGenericInfo($"ID: {gameId} | Status: OK | Queue: {addData.Count}");

                        ReviewsManagerTimers[bot.BotName]["AddReviews"].Change(TimeSpan.FromSeconds(3), TimeSpan.FromMilliseconds(-1));

                        return;
                    }

                    if ((response.StrError != null) && response.StrError.Contains("Please try again at a later time.", StringComparison.OrdinalIgnoreCase)) {
                        timeout = 10;

                        bot.ArchiLogger.LogGenericInfo($"ID: {gameId} | Status: RateLimitExceeded | Queue: {addData.Count} | Next run: {DateTime.Now.AddMinutes(timeout):T}");
                    } else {
                        addData.RemoveAt(0);

                        bot.ArchiLogger.LogGenericInfo($"ID: {gameId} | Status: {response.StrError} | Queue: {addData.Count} | Next run: {DateTime.Now.AddMinutes(timeout):T}");
                    }
                } else {
                    bot.ArchiLogger.LogGenericInfo($"ID: {gameId} | Status: Error | Queue: {addData.Count} | Next run: {DateTime.Now.AddMinutes(timeout):T}");
                }
            } else {
                bot.ArchiLogger.LogGenericInfo($"Status: BotNotConnected | Queue: {addData.Count} | Next run: {DateTime.Now.AddMinutes(timeout):T}");
            }

            ReviewsManagerTimers[bot.BotName]["AddReviews"].Change(TimeSpan.FromMinutes(timeout), TimeSpan.FromMilliseconds(-1));
        } else {
            bot.ArchiLogger.LogGenericInfo($"Status: QueueIsEmpty | Queue: {addData.Count} | Next run: {DateTime.Now.AddHours(ReviewsManagerConfig[bot.BotName].Timeout):T}");

            ReviewsManagerTimers[bot.BotName]["GetAllReviews"].Change(TimeSpan.FromHours(ReviewsManagerConfig[bot.BotName].Timeout), TimeSpan.FromMilliseconds(-1));
        }
    }

    public async Task DelReviews(Bot bot, List<uint> delData) {
        if (delData.Count > 0) {
            if (bot.IsConnectedAndLoggedOn) {
                uint gameId = delData[0];

                bool response = await bot.ArchiWebHandler.UrlPostWithSession(
                    new Uri($"{ArchiWebHandler.SteamCommunityURL}/profiles/{bot.SteamID}/recommended/"), data: new Dictionary<string, string>(3) {
                        { "action", "delete" },
                        { "appid", $"{gameId}" }
                    }, referer: new Uri($"{ArchiWebHandler.SteamCommunityURL}/profiles/{bot.SteamID}/recommended/{gameId}/")
                ).ConfigureAwait(false);

                if (response) {
                    delData.RemoveAt(0);

                    bot.ArchiLogger.LogGenericInfo($"ID: {gameId} | Status: OK | Queue: {delData.Count}");

                    ReviewsManagerTimers[bot.BotName]["DelReviews"].Change(TimeSpan.FromSeconds(3), TimeSpan.FromMilliseconds(-1));

                    return;
                }

                bot.ArchiLogger.LogGenericInfo($"ID: {gameId} | Status: Error | Queue: {delData.Count} | Next run: {DateTime.Now.AddMinutes(1):T}");
            } else {
                bot.ArchiLogger.LogGenericInfo($"Status: BotNotConnected | Queue: {delData.Count} | Next run: {DateTime.Now.AddMinutes(1):T}");
            }

            ReviewsManagerTimers[bot.BotName]["DelReviews"].Change(TimeSpan.FromMinutes(1), TimeSpan.FromMilliseconds(-1));
        } else {
            bot.ArchiLogger.LogGenericInfo($"Status: QueueIsEmpty | Queue: {delData.Count} | Next run: {DateTime.Now.AddHours(ReviewsManagerConfig[bot.BotName].Timeout):T}");

            ReviewsManagerTimers[bot.BotName]["GetAllReviews"].Change(TimeSpan.FromHours(ReviewsManagerConfig[bot.BotName].Timeout), TimeSpan.FromMilliseconds(-1));
        }
    }
}
