# Reviews Manager Plugin for ArchiSteamFarm

[![GitHub release version](https://img.shields.io/github/v/release/JackieWaltRyan/ReviewsManager.svg?label=Stable&logo=github)](https://github.com/JackieWaltRyan/ReviewsManager/releases/latest)
[![GitHub release date](https://img.shields.io/github/release-date/JackieWaltRyan/ReviewsManager.svg?label=Released&logo=github)](https://github.com/JackieWaltRyan/ReviewsManager/releases/latest)
[![Github release downloads](https://img.shields.io/github/downloads/JackieWaltRyan/ReviewsManager/latest/total.svg?label=Downloads&logo=github)](https://github.com/JackieWaltRyan/ReviewsManager/releases/latest)

ASF plugin for automatic adding and deleting reviews.

## Installation

1. Download the .zip file from
   the [![GitHub Release](https://img.shields.io/github/v/release/JackieWaltRyan/ReviewsManager?display_name=tag&logo=github&label=latest%20release)](https://github.com/JackieWaltRyan/ReviewsManager/releases/latest).<br><br>
2. Locate the `plugins` folder inside your ASF folder. Create a new folder here and unpack the downloaded .zip file to
   that folder.<br><br>
3. (Re)start ASF, you should get a message indicating that the plugin loaded successfully.

## Usage

Default configuration. To change this feature, add the following parameter to your bot's config file:

```json
{
  "ReviewsManagerConfig": {
    "AddReviews": false,
    "AddReviewsConfig": {
      "Comment": "⭐⭐⭐⭐⭐",
      "RatedUp": true,
      "IsPublic": true,
      "Language": "auto",
      "IsFree": false,
      "AllowComments": true,
      "BlackList": []
    },
    "DelReviews": false,
    "Timeout": 6
  }
}
```

- `AddReviews` - `bool` type with default value of `false`. If `true`, check the list of all games on the account and
  add reviews for all games that do not have them.
- #### AddReviewsConfig:
    - `Comment` - `string` type with default value of `⭐⭐⭐⭐⭐`. Review text.<br><br>
    - `RatedUp` - `bool` type with default value of `true`. The value of the `Do you recommend this game?`
      field.<br><br>
    - `IsPublic` - `bool` type with default value of `true`. The value of the `Visibility` field. True - Public, False -
      Friends only.<br><br>
    - `Language` - `string` type with default value of `auto`. The value of the `Language` field. Acceptable values:
      `auto`, `schinese`, `tchinese`, `japanese`, `koreana`, `thai`, `bulgarian`, `czech`, `danish`, `german`,
      `english`, `spanish`, `latam`, `greek`, `french`, `italian`, `indonesian`, `hungarian`, `dutch`, `norwegian`,
      `polish`, `portuguese`, `brazilian`, `romanian`, `russian`, `finnish`, `swedish`, `turkish`, `vietnamese`,
      `ukrainian`. If the value is set to `auto`, the plugin will use the current account language if it can detect it.
      If the account language cannot be detected, the value `english` will be used.<br><br>
    - `IsFree` - `bool` type with default value of `false`. The value of the
      `Check this box if you received this product for free` field.<br><br>
    - `AllowComments` - `bool` type with default value of `true`. The value of the `Allow Comments` field.<br><br>
    - `BlackList` - `List<uint>` type with default value of being empty. List of `appIDs` reviews for which should never
      be published, even if possible. The main purpose is that there are some games in Steam for which reviews cannot be
      published, despite the fact that all the conditions for them are met. To prevent the plugin from sending
      unnecessary requests to the server every time and wasting time, you can disable such games in this list.<br><br>
- `DelReviews` - `bool` type with default value of `false`. If `true`, check the list of all games on the account and
  delete all existing reviews for games that are no longer on the account (mainly applies to free games or demos, which
  can be deleted from the library).<br><br>
- `Timeout` - `uint` type with default value of `6`. This is the wait time in hours between re-checks of all games and
  reviews on the account. That is, this is the moment when there are no reviews to delete (or this feature is disabled)
  and/or there are no reviews to add (or this feature is disabled) Reviews Manager will wait 6 hours before re-checking
  the account. By default, this value is 6 hours. Since a large number of requests to Steam servers can be created
  during the check, and since the appearance of new games on the account with a played time of more than 5 minutes (
  Steam requirement for writing a review) is a rare occurrence, it is highly recommended not to set a low value for this
  parameter!
