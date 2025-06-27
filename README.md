# Reviews Manager Plugin for ArchiSteamFarm

ASF plugin for automatic adding and deleting reviews.

## Installation

- Download the .zip file from
  the [![GitHub Release](https://img.shields.io/github/v/release/JackieWaltRyan/ReviewsManager?display_name=tag&logo=github&label=latest%20release)](https://github.com/JackieWaltRyan/ReviewsManager/releases/latest)
- Locate the `plugins` folder inside your ASF folder. Create a new folder here and unpack the downloaded .zip file to
  that folder.
- (Re)start ASF, you should get a message indicating that the plugin loaded successfully.

## Usage

### Add Reviews Enable

Check the list of all games on the account and add reviews for all games that do not have them. To enable this feature,
add the following parameter to your bot's configuration file:

```json
{
  "AddMissingReviews": true
}
```

#### Add Reviews Configuration

```json
{
  "AddReviewsConfig": {
    "Comment": "⭐⭐⭐⭐⭐",
    "RatedUp": true,
    "IsPublic": true,
    "Language": "english",
    "IsFree": false,
    "AllowComments": true
  }
}
```

- `Comment` - `string` type with default value of `⭐⭐⭐⭐⭐`. Review text.


- `RatedUp` - `bool` type with default value of `true`. The value of the `Do you recommend this game?` field.


- `IsPublic` - `bool` type with default value of `true`. The value of the `Visibility` field. True - Public, False -
  Friends only.


- `Language` - `string` type with default value of `english`. The value of the `Language` field. Acceptable values:
  `schinese`, `tchinese`, `japanese`, `koreana`, `thai`, `bulgarian`, `czech`, `danish`, `german`, `english`, `spanish`,
  `latam`, `greek`, `french`, `italian`, `indonesian`, `hungarian`, `dutch`, `norwegian`, `polish`, `portuguese`,
  `brazilian`, `romanian`, `russian`, `finnish`, `swedish`, `turkish`, `vietnamese`, `ukrainian`.


- `IsFree` - `bool` type with default value of `false`. The value of the
  `Check this box if you received this product for free` field.


- `AllowComments` - `bool` type with default value of `true`. The value of the `Allow Comments` field.

### Delete Reviews Enable

Check the list of all games on the account and delete all existing reviews for games that are no longer on the account (
mainly applies to free games or demos, which can be deleted from the library). To enable this feature, add the following
parameter to your bot's configuration file:

```json
{
  "DelMissingReviews": true
}
```
