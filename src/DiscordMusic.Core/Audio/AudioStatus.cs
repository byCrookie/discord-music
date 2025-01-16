namespace DiscordMusic.Core.Audio;

public readonly record struct AudioStatus(AudioState State, TimeSpan Position, TimeSpan Length)
{
    public static AudioStatus Stopped => new(AudioState.Stopped, TimeSpan.Zero, TimeSpan.Zero);
}
