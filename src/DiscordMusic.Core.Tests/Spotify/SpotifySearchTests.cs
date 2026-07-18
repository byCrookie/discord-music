using DiscordMusic.Core.Spotify;
using Flurl;

namespace DiscordMusic.Core.Tests.Spotify;

internal class SpotifySearchTests
{
    [Test]
    [MethodDataSource(nameof(SpotifyUrlData))]
    public async Task TryParseSpotifyUrlExtractsTypeAndId(
        string value,
        string expectedType,
        string expectedId
    )
    {
        var result = SpotifySearch.TryParseSpotifyUrl(new Url(value), out var spotifyUrl);

        await Assert.That(result).IsTrue();
        await Assert.That(spotifyUrl.Type.ToString()).IsEqualTo(expectedType);
        await Assert.That(spotifyUrl.Id).IsEqualTo(expectedId);
    }

    [Test]
    public async Task IsPremiumRequiredResponseReturnsTrueForSpotifyPremiumError()
    {
        var result = SpotifySearch.IsPremiumRequiredResponse(
            "Forbidden",
            "Active premium subscription required for the owner of the app."
        );

        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task IsPremiumRequiredResponseReturnsFalseForOtherForbiddenErrors()
    {
        var result = SpotifySearch.IsPremiumRequiredResponse("Forbidden", "Missing scope.");

        await Assert.That(result).IsFalse();
    }

    public static IEnumerable<(
        string value,
        string expectedType,
        string expectedId
    )> SpotifyUrlData()
    {
        yield return (
            "https://open.spotify.com/artist/0GOx72r5AAEKRGQFn3xqXK",
            "Artist",
            "0GOx72r5AAEKRGQFn3xqXK"
        );
        yield return (
            "https://open.spotify.com/playlist/37i9dQZF1DX0MD19TXm0aV",
            "Playlist",
            "37i9dQZF1DX0MD19TXm0aV"
        );
        yield return (
            "https://open.spotify.com/playlist/37i9dQZF1E4p5ofi3OATFS",
            "Playlist",
            "37i9dQZF1E4p5ofi3OATFS"
        );
        yield return (
            "https://open.spotify.com/album/1V6a99EbTTIegOhWoPxYI9",
            "Album",
            "1V6a99EbTTIegOhWoPxYI9"
        );
        yield return (
            "https://open.spotify.com/track/1Yk0cQdMLx5RzzFTYwmuld",
            "Track",
            "1Yk0cQdMLx5RzzFTYwmuld"
        );
    }
}
