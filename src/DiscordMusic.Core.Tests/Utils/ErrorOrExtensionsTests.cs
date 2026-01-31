using DiscordMusic.Core.Utils;
using ErrorOr;

namespace DiscordMusic.Core.Tests.Utils;

public class ErrorOrExtensionsTests
{
    [Test]
    public Task ToErrorContent_SingleError_Snapshot()
    {
        var error = Error.Validation(
            code: "Validation.Required",
            description: "You need to provide a query."
        );

        ErrorOr<Success> result = error;

        var content = result.ToErrorContent();

        return Verify(content);
    }

    [Test]
    public Task ToErrorContent_SingleErrorWithMultilineDescription_IncludesDetailsBlock_Snapshot()
    {
        var error = Error.Unexpected(
            code: "YouTube.DownloadFailed",
            description: "Downloading from YouTube failed.\nExit code: 1\nThis is a sub error line"
        );

        ErrorOr<Success> result = error;

        var content = result.ToErrorContent();

        return Verify(content);
    }

    [Test]
    public Task ToErrorContent_MultipleErrors_Snapshot()
    {
        var errors = new[]
        {
            Error.NotFound(code: "Session.NotFound", description: "No active music session."),
            Error.Validation(code: "Permissions.Missing", description: "You don't have permission to do that."),
        };

        ErrorOr<Success> result = errors;

        var content = result.ToErrorContent();

        return Verify(content);
    }
}
