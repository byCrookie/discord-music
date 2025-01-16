namespace DiscordMusic.Core.Utils;

public static class TimeSpanParser
{
    public static bool TryParse(string input, out TimeSpan result)
    {
        if (!int.TryParse(input, out var seconds))
        {
            var separators = input.Split(':').Length - 1;

            if (separators == 1)
            {
                if (TimeSpan.TryParse($"00:{input.Trim()}", out var timeSpan))
                {
                    result = timeSpan;
                    return true;
                }

                result = TimeSpan.Zero;
                return false;
            }

            if (separators == 0)
            {
                if (TimeSpan.TryParse($"00:00:{input.Trim()}", out var timeSpan))
                {
                    result = timeSpan;
                    return true;
                }

                result = TimeSpan.Zero;
                return false;
            }

            {
                if (TimeSpan.TryParse(input, out var timeSpan))
                {
                    result = timeSpan;
                    return true;
                }
            }

            result = TimeSpan.Zero;
            return false;
        }

        result = TimeSpan.FromSeconds(seconds);
        return true;
    }
}
