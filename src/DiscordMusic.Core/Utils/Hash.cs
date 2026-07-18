using System.Security.Cryptography;
using System.Text;

namespace DiscordMusic.Core.Utils;

public class Hash
{
    public static string Sha256(string text) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
}
