using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ArchiSteamFarm.Helpers.Json;
using ArchiSteamFarm.Plugins.Interfaces;
using ArchiSteamFarm.Steam;
using ArchiSteamFarm.Web.Responses;

namespace ReviewsManager;

internal sealed class ReviewsManager : IGitHubPluginUpdates, IBotModules {
    public string Name => nameof(ReviewsManager);
    public string RepositoryName => "JackieWaltRyan/ReviewsManager";
    public Version Version => typeof(ReviewsManager).Assembly.GetName().Version ?? throw new InvalidOperationException(nameof(Version));
    public static Dictionary<string, Timer> BotTimers = new();

    public Task OnLoaded() => Task.CompletedTask;

    public Task OnBotInitModules(Bot bot, IReadOnlyDictionary<string, JsonElement>? additionalConfigProperties = null) {
        if (additionalConfigProperties == null) {
            return Task.CompletedTask;
        }

        bool isEnabled = false;
        bool addEnabled = true;
        bool delEnabled = true;

        uint timeout = 60;

        List<uint> addData = [];
        List<uint> delData = [];

        foreach (KeyValuePair<string, JsonElement> configProperty in additionalConfigProperties) {
            switch (configProperty.Key) {
                case "EnableReviewsManager" when configProperty.Value.ValueKind is JsonValueKind.True or JsonValueKind.False: {
                    isEnabled = configProperty.Value.GetBoolean();

                    bot.ArchiLogger.LogGenericInfo($"Enable Reviews Manager: {isEnabled}");

                    break;
                }

                case "ReviewsManagerTimeout" when configProperty.Value.ValueKind == JsonValueKind.Number: {
                    timeout = configProperty.Value.ToJsonObject<uint>();

                    bot.ArchiLogger.LogGenericInfo($"Reviews Manager Timeout: {timeout}");

                    break;
                }

                case "ReviewsManagerAdd" when configProperty.Value.ValueKind is JsonValueKind.True or JsonValueKind.False: {
                    addEnabled = configProperty.Value.GetBoolean();

                    bot.ArchiLogger.LogGenericInfo($"Add non-existent reviews: {addEnabled}");

                    break;
                }

                case "ReviewsManagerDel" when configProperty.Value.ValueKind is JsonValueKind.True or JsonValueKind.False: {
                    delEnabled = configProperty.Value.GetBoolean();

                    bot.ArchiLogger.LogGenericInfo($"Remove non-existent reviews: {delEnabled}");

                    break;
                }
            }
        }

        if (isEnabled) {
            // ReSharper disable once AsyncVoidMethod
            BotTimers.Add(bot.BotName, new Timer(async void (e) => await GetUserReviews(bot, addEnabled, addData, delEnabled, delData, timeout).ConfigureAwait(false), null, 0, Timeout.Infinite));
        }

        return Task.CompletedTask;
    }

    public static async Task GetUserReviews(Bot bot, bool addEnabled, List<uint> addData, bool delEnabled, List<uint> delData, uint timeout) {
        await BotTimers[bot.BotName].DisposeAsync().ConfigureAwait(false);

        ObjectResponse<JsonElement>? response = await bot.ArchiWebHandler.UrlGetToJsonObjectWithSession<JsonElement>(new Uri($"https://api.steampowered.com/IPlayerService/GetOwnedGames/v1/?access_token={bot.AccessToken}&steamid={bot.SteamID}&include_played_free_games=true&skip_unvetted_apps=false")).ConfigureAwait(false);

        JsonElement? items = response?.Content;

        bot.ArchiLogger.LogGenericInfo(items.ToJsonText());

        if (delEnabled && (delData.Count > 0)) {
            await DelReviews(bot, addEnabled, addData, delEnabled, delData, timeout).ConfigureAwait(false);
        } else if (addEnabled && (addData.Count > 0)) {
            await AddReviews(bot, addEnabled, addData, delEnabled, delData, timeout).ConfigureAwait(false);
        } else {
            // ReSharper disable once AsyncVoidMethod
            BotTimers[bot.BotName] = new Timer(async void (e) => await GetUserReviews(bot, addEnabled, addData, delEnabled, delData, timeout).ConfigureAwait(false), null, TimeSpan.FromMinutes(timeout), TimeSpan.FromMilliseconds(-1));
        }
    }

    public static async Task DelReviews(Bot bot, bool addEnabled, List<uint> addData, bool delEnabled, List<uint> delData, uint timeout) {
        await BotTimers[bot.BotName].DisposeAsync().ConfigureAwait(false);

        bot.ArchiLogger.LogGenericInfo($"Del non-existent reviews: {delData.ToJsonText()}");
    }

    public static async Task AddReviews(Bot bot, bool addEnabled, List<uint> addData, bool delEnabled, List<uint> delData, uint timeout) {
        await BotTimers[bot.BotName].DisposeAsync().ConfigureAwait(false);

        bot.ArchiLogger.LogGenericInfo($"Add non-existent reviews: {addData.ToJsonText()}");
    }
}
