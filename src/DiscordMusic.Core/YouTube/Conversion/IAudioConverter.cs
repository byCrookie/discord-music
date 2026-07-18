using System.IO.Abstractions;
using ErrorOr;

namespace DiscordMusic.Core.YouTube.Conversion;

internal interface IAudioConverter
{
    Task<ErrorOr<Success>> ConvertToPcmAsync(
        IFileInfo input,
        IFileInfo output,
        CancellationToken ct
    );
}
