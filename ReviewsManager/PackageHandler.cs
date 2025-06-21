using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ArchiSteamFarm.Core;
using ArchiSteamFarm.Steam;

namespace ReviewsManager;

internal sealed class PackageHandler : IDisposable {
    internal readonly Bot Bot;
    internal readonly BotCache BotCache;
    internal readonly PackageFilter PackageFilter;
    private readonly PackageQueue PackageQueue;
    internal static ConcurrentDictionary<string, PackageHandler> Handlers = new();

    private readonly Timer UserDataRefreshTimer;
    private static readonly SemaphoreSlim AddHandlerSemaphore = new SemaphoreSlim(1, 1);
    private static readonly SemaphoreSlim ProcessChangesSemaphore = new SemaphoreSlim(1, 1);

    private PackageHandler(Bot bot, BotCache botCache, List<FilterConfig> filterConfigs, uint? packageLimit, bool pauseWhilePlaying) {
        Bot = bot;
        BotCache = botCache;
        PackageFilter = new PackageFilter(botCache, filterConfigs);
        PackageQueue = new PackageQueue(bot, botCache, packageLimit, pauseWhilePlaying);
        UserDataRefreshTimer = new Timer(async e => await FetchUserData().ConfigureAwait(false), null, Timeout.Infinite, Timeout.Infinite);
    }

    public void Dispose() {
        PackageQueue.Dispose();
        UserDataRefreshTimer.Dispose();
    }

    internal static async Task AddHandler(Bot bot, List<FilterConfig> filterConfigs, uint? packageLimit, bool pauseWhilePlaying) {
        if (Handlers.TryGetValue(bot.BotName, out PackageHandler? value)) {
            value.Dispose();
            Handlers.TryRemove(bot.BotName, out PackageHandler? _);
        }

        await AddHandlerSemaphore.WaitAsync().ConfigureAwait(false);

        try {
            string cacheFilePath = Bot.GetFilePath(String.Format("{0}_{1}", bot.BotName, nameof(ReviewsManager)), Bot.EFileType.Database);

            BotCache? botCache = await BotCache.CreateOrLoad(cacheFilePath).ConfigureAwait(false);

            if (botCache == null) {
                bot.ArchiLogger.LogGenericError(String.Format(ArchiSteamFarm.Localization.Strings.ErrorDatabaseInvalid, cacheFilePath));
                botCache = new(cacheFilePath);
            }

            Handlers.TryAdd(bot.BotName, new PackageHandler(bot, botCache, filterConfigs, packageLimit, pauseWhilePlaying));
        } finally {
            AddHandlerSemaphore.Release();
        }
    }

    internal static void OnAccountInfo(Bot bot, SteamUser.AccountInfoCallback callback) {
        if (!Handlers.ContainsKey(bot.BotName)) {
            return;
        }

        Handlers[bot.BotName].PackageFilter.UpdateAccountInfo(callback);
    }

    private void UpdateUserData() => UserDataRefreshTimer.Change(TimeSpan.Zero, TimeSpan.FromMinutes(15));

    private async Task FetchUserData() {
        if (!Bot.IsConnectedAndLoggedOn) {
            UserDataRefreshTimer.Change(TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(15));

            return;
        }

        Steam.UserData? userData = await WebRequest.GetUserData(Bot).ConfigureAwait(false);

        if (userData == null) {
            UserDataRefreshTimer.Change(TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(15));
            Bot.ArchiLogger.LogGenericError(String.Format(ArchiSteamFarm.Localization.Strings.ErrorObjectIsNull, userData));

            return;
        }

        PackageFilter.UpdateUserData(userData);

        UserDataRefreshTimer.Change(TimeSpan.FromMinutes(15), TimeSpan.FromMinutes(15));
    }

    private async static Task<bool> IsReady(uint maxWaitTimeSeconds = 120) {
        DateTime timeoutTime = DateTime.Now.AddSeconds(maxWaitTimeSeconds);

        do {
            bool ready = Handlers.Values.Where(x => x.Bot.BotConfig.Enabled && !x.PackageFilter.Ready).Count() == 0;

            if (ready) {
                return true;
            }

            await Task.Delay(TimeSpan.FromSeconds(1)).ConfigureAwait(false);
        } while (maxWaitTimeSeconds == 0 || DateTime.Now < timeoutTime);

        return false;
    }

    internal async static Task HandleChanges() {
        await ProcessChangesSemaphore.WaitAsync().ConfigureAwait(false);

        try {
            await IsReady().ConfigureAwait(false);

            HashSet<uint> appIDs = Handlers.Values.Where(x => x.Bot.IsConnectedAndLoggedOn).SelectMany(x => x.BotCache.ChangedApps).ToHashSet<uint>();
            HashSet<uint> packageIDs = Handlers.Values.Where(x => x.Bot.IsConnectedAndLoggedOn).SelectMany(x => x.BotCache.ChangedPackages).ToHashSet<uint>();
            HashSet<uint> newOwnedPackageIDs = Handlers.Values.Where(x => x.Bot.IsConnectedAndLoggedOn).SelectMany(x => x.BotCache.NewOwnedPackages).ToHashSet<uint>();
            packageIDs.UnionWith(newOwnedPackageIDs);

            if (appIDs.Count == 0 && packageIDs.Count == 0) {
                return;
            }

            foreach ((HashSet<uint>? batchedAppIDs, HashSet<uint>? batchedPackageIDs) in ProductInfo.GetProductIDBatches(appIDs, packageIDs)) {
                var productInfo = await ProductInfo.GetProductInfo(batchedAppIDs, batchedPackageIDs).ConfigureAwait(false);

                if (productInfo == null) {
                    continue;
                }

                await HandleProductInfo(productInfo).ConfigureAwait(false);
            }
        } finally {
            ProcessChangesSemaphore.Release();
        }
    }

    private async static Task HandleProductInfo(List<SteamApps.PICSProductInfoCallback> productInfo) {
        // Figure out which apps are free and add any wanted apps to the queue
        var appProductInfos = productInfo.SelectMany(static result => result.Apps.Values);

        if (appProductInfos.Count() > 0) {
            List<FilterableApp> apps = appProductInfos.Select(x => new FilterableApp(x)).ToList();

            // Filter out non-free apps
            apps.RemoveAll(app => {
                    if (!app.IsFree() || !app.IsAvailable()) {
                        Handlers.Values.ToList().ForEach(x => x.BotCache.RemoveChange(appID: app.ID));

                        return true;
                    }

                    return false;
                }
            );

            // Get the parents of the free apps
            HashSet<uint> parentIDs = apps.Where(app => app.ParentID != null).Select(app => app.ParentID!.Value).ToHashSet();
            var parentProductInfos = (await ProductInfo.GetProductInfo(appIDs: parentIDs).ConfigureAwait(false))?.SelectMany(static result => result.Apps.Values);

            if (parentProductInfos == null) {
                ASF.ArchiLogger.LogNullError(parentProductInfos);

                return;
            }

            if (parentProductInfos.Count() > 0) {
                apps.ForEach(app => {
                        if (app.ParentID != null) {
                            app.AddParent(parentProductInfos.FirstOrDefault(parent => parent.ID == app.ParentID));
                        }
                    }
                );
            }

            // Add wanted apps to the queue
            apps.ForEach(app => {
                    if (app.Type == EAppType.Beta) {
                        Handlers.Values.ToList().ForEach(x => x.HandlePlaytest(app));
                    } else {
                        Handlers.Values.ToList().ForEach(x => x.HandleFreeApp(app));
                    }
                }
            );
        }

        // Figure out which packages are free and add any wanted packages to the queue
        var packageProductInfos = productInfo.SelectMany(static result => result.Packages.Values);

        if (packageProductInfos.Count() > 0) {
            HashSet<uint> newOwnedPackageIDs = Handlers.Values.Where(x => x.Bot.IsConnectedAndLoggedOn).SelectMany(x => x.BotCache.NewOwnedPackages).ToHashSet<uint>();
            List<FilterablePackage> packages = packageProductInfos.Select(x => new FilterablePackage(x, newOwnedPackageIDs.Contains(x.ID))).ToList();

            // Filter out non-free, non-new packages
            packages.RemoveAll(package => {
                    if (!package.IsFree() || !package.IsAvailable()) {
                        Handlers.Values.ToList().ForEach(x => x.BotCache.RemoveChange(packageID: package.ID));

                        if (!package.IsNew) {
                            return true;
                        }
                    }

                    return false;
                }
            );

            // Get the apps contained in each package
            HashSet<uint> packageContentsIDs = packages.SelectMany(package => package.PackageContentIDs).ToHashSet();
            var packageContentProductInfos = (await ProductInfo.GetProductInfo(appIDs: packageContentsIDs).ConfigureAwait(false))?.SelectMany(static result => result.Apps.Values);

            if (packageContentProductInfos == null) {
                ASF.ArchiLogger.LogNullError(packageContentProductInfos);

                return;
            }

            packages.ForEach(package => package.AddPackageContents(packageContentProductInfos.Where(x => package.PackageContentIDs.Contains(x.ID))));

            // Filter out any packages which contain unavailable apps
            packages.RemoveAll(package => {
                    if (!package.IsAvailablePackageContents() && package.BillingType != EBillingType.NoCost) {
                        // Ignore this check for NoCost packages; assume that everything is available
                        // Ex: https://steamdb.info/sub/1011710 is redeemable even though it contains https://steamdb.info/app/235901/ (which as of Feb 12 2024 is some unknown app)
                        Handlers.Values.ToList().ForEach(x => x.BotCache.RemoveChange(packageID: package.ID));

                        if (!package.IsNew) {
                            return true;
                        }
                    }

                    return false;
                }
            );

            // Get the parents for the apps in each package
            HashSet<uint> parentIDs = packages.SelectMany(package => package.PackageContentParentIDs).ToHashSet();
            var parentProductInfos = (await ProductInfo.GetProductInfo(appIDs: parentIDs).ConfigureAwait(false))?.SelectMany(static result => result.Apps.Values);

            if (parentProductInfos == null) {
                ASF.ArchiLogger.LogNullError(parentProductInfos);

                return;
            }

            if (parentProductInfos.Count() > 0) {
                packages.ForEach(package => {
                        if (package.PackageContentParentIDs.Count != 0) {
                            package.AddPackageContentParents(parentProductInfos.Where(parent => package.PackageContentParentIDs.Contains(parent.ID)));
                        }
                    }
                );
            }

            // Add wanted packages to the queue or check new packages for free DLC
            packages.ForEach(package => {
                    if (package.IsNew) {
                        Handlers.Values.ToList().ForEach(x => x.HandleNewPackage(package));
                    } else {
                        Handlers.Values.ToList().ForEach(x => x.HandleFreePackage(package));
                    }
                }
            );
        }

        // Remove invalid apps from the app change list
        foreach (uint unknownAppID in productInfo.SelectMany(static result => result.UnknownApps)) {
            Handlers.Values.ToList().ForEach(x => x.BotCache.RemoveChange(appID: unknownAppID));
        }

        // Remove invalid packages from the package change list
        foreach (uint unknownPackageID in productInfo.SelectMany(static result => result.UnknownPackages)) {
            Handlers.Values.ToList().ForEach(x => x.BotCache.RemoveChange(packageID: unknownPackageID));
            Handlers.Values.ToList().ForEach(x => x.BotCache.RemoveChange(newOwnedPackageID: unknownPackageID));
        }

        // Save changes to the app/package change lists
        Handlers.Values.ToList().ForEach(x => x.BotCache.SaveChanges());
    }

    private void HandleFreeApp(FilterableApp app) {
        if (!BotCache.ChangedApps.Contains(app.ID)) {
            return;
        }

        if (!PackageFilter.Ready) {
            return;
        }

        try {
            if (!PackageFilter.IsRedeemableApp(app)) {
                return;
            }

            if (!PackageFilter.IsWantedApp(app)) {
                return;
            }

            PackageQueue.AddPackage(new Package(EPackageType.App, app.ID));
        } finally {
            BotCache.RemoveChange(appID: app.ID);
        }
    }

    private void HandleFreePackage(FilterablePackage package) {
        if (!BotCache.ChangedPackages.Contains(package.ID)) {
            return;
        }

        if (!PackageFilter.Ready) {
            return;
        }

        try {
            if (!PackageFilter.IsRedeemablePackage(package)) {
                return;
            }

            if (!PackageFilter.IsWantedPackage(package)) {
                return;
            }

            PackageQueue.AddPackage(new Package(EPackageType.Sub, package.ID, package.StartTime), package.PackageContentIDs);
        } finally {
            BotCache.RemoveChange(packageID: package.ID);
        }
    }

    private void HandlePlaytest(FilterableApp app) {
        if (!BotCache.ChangedApps.Contains(app.ID)) {
            return;
        }

        if (!PackageFilter.Ready) {
            return;
        }

        try {
            if (app.Parent == null) {
                return;
            }

            if (!PackageFilter.IsRedeemablePlaytest(app)) {
                return;
            }

            if (!PackageFilter.IsWantedPlaytest(app)) {
                return;
            }

            PackageQueue.AddPackage(new Package(EPackageType.Playtest, app.Parent.ID));
        } finally {
            BotCache.RemoveChange(appID: app.ID);
        }
    }

    private void HandleNewPackage(FilterablePackage package) {
        if (!BotCache.NewOwnedPackages.Contains(package.ID)) {
            return;
        }

        try {
            if (package.PackageContents.Count == 0) {
                return;
            }

            // Check for free DLC on newly added packages
            HashSet<uint> dlcAppIDs = new();

            foreach (FilterableApp app in package.PackageContents) {
                if (String.IsNullOrEmpty(app.ListOfDLC)) {
                    continue;
                }

                foreach (string dlcAppIDString in app.ListOfDLC.Split(",", StringSplitOptions.RemoveEmptyEntries)) {
                    if (!uint.TryParse(dlcAppIDString, out uint dlcAppID) || (dlcAppID == 0)) {
                        continue;
                    }

                    dlcAppIDs.Add(dlcAppID);
                }
            }

            if (dlcAppIDs.Count != 0) {
                BotCache.AddChanges(appIDs: dlcAppIDs);
                Utilities.InBackground(async () => await HandleChanges().ConfigureAwait(false));
            }
        } finally {
            BotCache.RemoveChange(newOwnedPackageID: package.ID);
        }
    }

    internal void HandleLicenseList(SteamApps.LicenseListCallback callback) {
        List<SteamApps.LicenseListCallback.License> newLicenses = callback.LicenseList.Where(license => !BotCache.SeenPackages.Contains(license.PackageID)).ToList();

        if (newLicenses.Count == 0) {
            return;
        }

        UpdateUserData();

        // Initialize SeenPackages
        if (BotCache.SeenPackages.Count == 0) {
            BotCache.UpdateSeenPackages(newLicenses);

            return;
        }

        BotCache.AddChanges(newOwnedPackageIDs: newLicenses.Select(license => license.PackageID).ToHashSet());
        BotCache.UpdateSeenPackages(newLicenses);
        Utilities.InBackground(async () => await HandleChanges().ConfigureAwait(false));
    }

    internal string GetStatus() => PackageQueue.GetStatus();
}
