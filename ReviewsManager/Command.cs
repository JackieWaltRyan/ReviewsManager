using System;
using System.Collections.Generic;
using ArchiSteamFarm.Core;
using ArchiSteamFarm.Localization;
using ArchiSteamFarm.Steam;
using ASFEnhance.Data.Common;
using ASFEnhance.Data.Plugin;
using SteamKit2;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualBasic;

namespace ReviewsManager;

internal static class Command {
    internal static async Task<string?> ResponsePublishReview(Bot bot, string strAppId, string comment) {
        if (string.IsNullOrEmpty(strAppId)) {
            throw new ArgumentNullException(nameof(strAppId));
        }

        if (string.IsNullOrEmpty(comment)) {
            throw new ArgumentNullException(nameof(comment));
        }

        if (!int.TryParse(strAppId, out int appId) || (appId == 0)) {
            throw new ArgumentException(null, nameof(strAppId));
        }

        if (!bot.IsConnectedAndLoggedOn) {
            return bot.FormatBotResponse(Strings.BotNotConnected);
        }

        bool rateUp = appId > 0;

        if (!rateUp) {
            appId = -appId;
        }

        var response = await WebRequest.PublishReview(bot, (uint) appId, comment, rateUp, true, false).ConfigureAwait(false);

        if (response == null || !response.Result) {
            return bot.FormatBotResponse(Langs.RecommendPublishFailed, response?.ErrorMsg);
        }

        var reviewUri = new Uri(SteamCommunityURL, $"/profiles/{bot.SteamID}/recommended/{appId}/");

        return bot.FormatBotResponse(string.Format(Langs.AccountSubItem, Langs.RecommendPublishSuccess, reviewUri));
    }

    internal static async Task<string?> ResponsePublishReview(string botNames, string appId, string review) {
        if (string.IsNullOrEmpty(botNames)) {
            throw new ArgumentNullException(nameof(botNames));
        }

        var bots = Bot.GetBots(botNames);

        if ((bots == null) || (bots.Count == 0)) {
            return FormatStaticResponse(Strings.BotNotFound, botNames);
        }

        var results = await Utilities.InParallel(bots.Select(bot => ResponsePublishReview(bot, appId, review))).ConfigureAwait(false);

        var responses = new List<string?>(results.Where(result => !string.IsNullOrEmpty(result)));

        return responses.Count > 0 ? string.Join(Environment.NewLine, responses) : null;
    }

    internal static async Task<string?> ResponseDeleteReview(Bot bot, string targetGameIds) {
        if (!bot.IsConnectedAndLoggedOn) {
            return bot.FormatBotResponse(Strings.BotNotConnected);
        }

        var sb = new StringBuilder();

        var games = targetGameIds.Split(',', StringSplitOptions.RemoveEmptyEntries);

        foreach (string game in games) {
            if (!uint.TryParse(game, out uint gameId) || (gameId == 0)) {
                sb.AppendLine(bot.FormatBotResponse(Strings.ErrorIsInvalid, nameof(gameId)));

                continue;
            }

            bool result = await WebRequest.DeleteRecommend(bot, gameId).ConfigureAwait(false);

            sb.AppendLine(bot.FormatBotResponse(Strings.BotAddLicense, gameId, result ? Langs.Success : Langs.Failure));
        }

        return sb.ToString();
    }

    internal static async Task<string?> ResponseDeleteReview(string botNames, string appId) {
        if (string.IsNullOrEmpty(botNames)) {
            throw new ArgumentNullException(nameof(botNames));
        }

        var bots = Bot.GetBots(botNames);

        if ((bots == null) || (bots.Count == 0)) {
            return FormatStaticResponse(Strings.BotNotFound, botNames);
        }

        var results = await Utilities.InParallel(bots.Select(bot => ResponseDeleteReview(bot, appId))).ConfigureAwait(false);

        var responses = new List<string?>(results.Where(result => !string.IsNullOrEmpty(result)));

        return responses.Count > 0 ? string.Join(Environment.NewLine, responses) : null;
    }
}
