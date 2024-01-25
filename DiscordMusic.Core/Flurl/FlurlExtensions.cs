using Flurl.Http;

namespace DiscordMusic.Core.Flurl;

internal static class FlurlExtensions
{
    public static T WithBotToken<T>(this T obj, string token) where T : IHeadersContainer {
        return obj.WithHeader("Authorization", $"Bot {token}");
    }
    
    public static async Task<List<TItem>> GetPagedJsonAsync<TResponse, TItem>(
        this IFlurlRequest request,
        Func<TResponse, List<TItem>> getItems,
        Func<TResponse, int, List<TItem>, bool> hasNextPage,
        Action<IFlurlRequest, int> getNextPageRequest,
        Func<IFlurlRequest, CancellationToken, Task<IFlurlResponse>> sendRequest,
        CancellationToken ct)
    {
        var allItems = new List<TItem>();
        var index = 0;
        while (true)
        {
            getNextPageRequest(request, index);
            var response = await sendRequest(request, ct).ReceiveJson<TResponse>();
            var items = getItems(response);
            allItems.AddRange(items);

            if (!hasNextPage(response, index, items))
            {
                return allItems;
            }

            index++;
        }
    }
}