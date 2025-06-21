using ArchiSteamFarm.Helpers.Json;
using ArchiSteamFarm.Steam;
using ASFEnhance.Data;
using ASFEnhance.Data.Common;
using ASFEnhance.Data.IStoreBrowseService;
using ASFEnhance.Data.Plugin;
using ASFEnhance.Data.WebApi;
using System.Text;

namespace ReviewsManager;

/// <summary>
/// 网络请求
/// </summary>
public static class WebRequest {
    /// <summary>
    /// 获取评测内容
    /// </summary>
    /// <param name="bot"></param>
    /// <param name="gameId"></param>
    /// <returns></returns>
    internal static async Task<string?> GetReviewContent(this Bot bot, uint gameId) {
        var absPath = await bot.GetProfileLink().ConfigureAwait(false);

        var request = new Uri(SteamCommunityURL, $"{absPath}/recommended/{gameId}/");
        var referer = new Uri(SteamCommunityURL, $"{absPath}/recommended/");

        var response = await bot.ArchiWebHandler.UrlGetToHtmlDocumentWithSession(request, referer: referer).ConfigureAwait(false);

        if (response?.Content == null) {
            return null;
        }

        if (response.FinalUri.LocalPath.EndsWith("recommended/")) {
            return Langs.ReviewNotReviewYet;
        }

        var content = response.Content;

        var gameName = content.QuerySelector("div.profile_small_header_text>a:nth-child(5)>span.profile_small_header_location")?.TextContent?.Trim();
        var rateUp = content.QuerySelector("#ReviewTitle>div.ratingSummaryHeader>img:nth-child(2)")?.GetAttribute("src")?.EndsWith("icon_thumbsUp.png?v=1");
        var reviewText = content.QuerySelector("#ReviewText")?.TextContent?.Trim();

        var sb = new StringBuilder();
        sb.AppendLine(Langs.MultipleLineResult);
        sb.AppendLineFormat(Langs.ReviewGameName, gameName);
        sb.AppendLineFormat(Langs.ReviewMark, rateUp == null ? Langs.ParseError : (rateUp.Value ? Langs.RateUp : Langs.RateDown));
        sb.AppendLineFormat(Langs.ReviewContent, reviewText);

        return sb.ToString();
    }

    /// <summary>
    /// 发布游戏评测
    /// </summary>
    /// <param name="bot"></param>
    /// <param name="gameId"></param>
    /// <param name="comment"></param>
    /// <param name="rateUp"></param>
    /// <param name="isPublic"></param>
    /// <param name="enComment"></param>
    /// <param name="forFree"></param>
    /// <returns></returns>
    public static async Task<RecommendGameResponse?> PublishReview(this Bot bot, uint gameId, string comment, bool rateUp = true, bool isPublic = true, bool enComment = true, bool forFree = false) {
        var request = new Uri(SteamStoreURL, "/friends/recommendgame");
        var referer = new Uri(SteamStoreURL, $"/app/{gameId}");

        var data = new Dictionary<string, string>(11, StringComparer.Ordinal) {
            { "appid", gameId.ToString() },
            { "steamworksappid", gameId.ToString() },
            { "comment", comment + '\u200D' },
            { "rated_up", rateUp ? "true" : "false" },
            { "is_public", isPublic ? "true" : "false" },
            { "language", DefaultOrCurrentLanguage },
            { "received_compensation", forFree ? "1" : "0" },
            { "disable_comments", enComment ? "0" : "1" },
        };

        var response = await bot.ArchiWebHandler.UrlPostToJsonObjectWithSession<RecommendGameResponse>(request, data: data, referer: referer).ConfigureAwait(false);

        return response?.Content;
    }

    /// <summary>
    /// 删除游戏评测
    /// </summary>
    /// <param name="bot"></param>
    /// <param name="gameId"></param>
    /// <returns></returns>
    public static async Task<bool> DeleteRecommend(this Bot bot, uint gameId) {
        var request = new Uri(SteamCommunityURL, $"/profiles/{bot.SteamID}/recommended/");
        var referer = new Uri(request, $"/{gameId}/");

        var data = new Dictionary<string, string>(3, StringComparer.Ordinal) {
            { "action", "delete" },
            { "appid", gameId.ToString() }
        };

        await bot.ArchiWebHandler.UrlPostWithSession(request, data: data, referer: referer).ConfigureAwait(false);

        return true;
    }
}
