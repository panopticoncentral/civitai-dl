using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Web;

namespace civitai;
internal class Program
{
    private static readonly HttpClient Client = new();
    
    private static string? _apiToken = "";
    private static string? _outputDirectory = "";
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    private const string BaseApiUrl = "https://civitai.com/api/trpc";

    private static async Task Main()
    {
        Console.WriteLine("Civitai Favorites Downloader");
        Console.WriteLine("----------------------------");

        Console.Write("Enter your Civitai API token: ");
        _apiToken = Console.ReadLine();

        if (string.IsNullOrWhiteSpace(_apiToken))
        {
            Console.WriteLine("API token is required. Exiting...");
            return;
        }

        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiToken);
        Client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        try
        {
            Console.Write("Enter output directory (leave blank for current directory): ");
            _outputDirectory = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(_outputDirectory))
            {
                _outputDirectory = Directory.GetCurrentDirectory();
            }
            else if (!Directory.Exists(_outputDirectory))
            {
                Directory.CreateDirectory(_outputDirectory);
            }

            Console.WriteLine("Finding your 'Favorite Post' collection...");
            var favoritePostsCollection = await GetFavoritePostCollectionAsync();
            if (favoritePostsCollection == null)
            {
                return;
            }

            Console.WriteLine($"Collection ID: {favoritePostsCollection.Id}");

            var favoritesDir = Path.Combine(_outputDirectory, "Favorite Posts");
            if (!Directory.Exists(favoritesDir))
            {
                Directory.CreateDirectory(favoritesDir);
            }

            // Download posts from the collection
            await DownloadFavoritePosts(favoritePostsCollection.Id, favoritesDir);

            Console.WriteLine("\nDownload complete!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred: {ex.Message}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"Inner exception: {ex.InnerException.Message}");
            }
        }
    }

    private static async Task<Collection?> GetFavoritePostCollectionAsync()
    {
        try
        {
            // ReSharper disable once StringLiteralTypo
            var collectionsResponse = await Client.GetAsync($"{BaseApiUrl}/collection.getById?input=%7B%22json%22%3A%7B%22id%22%3A886178%2C%22authed%22%3Atrue%7D%7D");
            collectionsResponse.EnsureSuccessStatusCode();
            var collectionsContent = await collectionsResponse.Content.ReadAsStringAsync();

            var trpcCollectionsResponse = JsonSerializer.Deserialize<TrpcResponse<CollectionsData>>(collectionsContent);
            if (trpcCollectionsResponse?.Result?.Data?.Json?.Collection?.Name != "Favorite Posts")
            {
                Console.WriteLine("'Favorite Posts' collection not found.");
                return null;
            }

            return trpcCollectionsResponse.Result.Data.Json.Collection;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting collections: {ex.Message}");
            return null;
        }
    }

    private static async Task DownloadFavoritePosts(int collectionId, string outputDir)
    {
        try
        {
            var cursor = "null";
            var cursorType = "undefined";
            var hasMore = true;
            var count = 0;

            while (hasMore)
            {
                var data =
                    $"{{\"json\":{{\"collectionId\":{collectionId},\"period\":\"AllTime\",\"sort\":\"Newest\",\"draftOnly\":null,\"followed\":null,\"include\":[\"cosmetics\"],\"browsingLevel\":31,\"cursor\":{cursor},\"authed\":true}},\"meta\":{{\"values\":{{\"draftOnly\":[\"undefined\"],\"followed\":[\"undefined\"],\"cursor\":[\"{cursorType}\"]}}}}}}";
                var encodedInput = HttpUtility.UrlEncode(data);

                var postsResponse = await Client.GetAsync($"{BaseApiUrl}/post.getInfinite?input={encodedInput}");
                postsResponse.EnsureSuccessStatusCode();
                var postsContent = await postsResponse.Content.ReadAsStringAsync();

                var postsResult = JsonSerializer.Deserialize<TrpcResponse<PostsData>>(postsContent);
                if (postsResult?.Result?.Data?.Json?.Items == null)
                {
                    Console.WriteLine("No posts found in this batch.");
                    break;
                }

                var posts = postsResult.Result.Data.Json.Items;
                Console.WriteLine($"Found {posts.Count} posts in this batch.");

                foreach (var post in posts)
                {
                    count++;
                    await DownloadPost(post, outputDir, count);
                }

                if (postsResult.Result.Data.Json.NextCursor != null)
                {
                    cursor = $"\"{postsResult.Result.Data.Json.NextCursor}\"";
                    cursorType = "Date";
                }
                else
                {
                    hasMore = false;
                }
            }

            Console.WriteLine($"Downloaded a total of {count} posts.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error downloading favorite posts: {ex.Message}");
        }
    }

    private static async Task DownloadPost(Post post, string outputDir, int count)
    {
        try
        {
            var postTitle = SanitizeFileName(!string.IsNullOrEmpty(post.Title) ? post.Title : "Untitled");
            var userName = SanitizeFileName(post.User?.Username ?? "Unknown User");
            var dirName = SanitizeFileName($"{post.Id}_{postTitle}");
            var postDir = Path.Combine(outputDir, userName, dirName);

            Console.WriteLine($"Downloading post {count}: {postTitle} (ID: {post.Id})");

            if (!Directory.Exists(postDir))
            {
                Directory.CreateDirectory(postDir);
            }

            var detailsPath = Path.Combine(postDir, "details.json");

            if (!File.Exists(detailsPath))
            {
                await File.WriteAllTextAsync(detailsPath, JsonSerializer.Serialize(post, Options));
            }

            if (post.Images is { Count: > 0 })
            {
                for (var i = 0; i < post.Images.Count; i++)
                {
                    var image = post.Images[i];
                    if (string.IsNullOrEmpty(image.Url) || string.IsNullOrEmpty(image.Name))
                    {
                        Console.WriteLine($"  - Skipping image {i + 1}/{post.Images.Count}");
                        continue;
                    }
                    var fileName = $"image_{image.Id}.jpg";
                    var filePath = Path.Combine(postDir, fileName);
                    if (!File.Exists(filePath))
                    {
                        await DownloadFileAsync($"https://image.civitai.com/xG1nkqKTMzGDvpLrqFT7WA/{image.Url}/original=true/{image.Name}", filePath);
                        Console.WriteLine($"  - Downloaded image {i + 1}/{post.Images.Count}");
                    }
                    else
                    {
                        Console.WriteLine($"  - Already downloaded image {i + 1}/{post.Images.Count}");
                    }
                }
            }
            else
            {
                Console.WriteLine("  No images found in this post.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error downloading post {post.Id}: {ex.Message}");
        }
    }

    private static async Task DownloadFileAsync(string url, string outputPath)
    {
        try
        {
            var response = await Client.GetAsync(url);
            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync();
            await using var fileStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None);
            await stream.CopyToAsync(fileStream);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error downloading file from {url}: {ex.Message}");
        }
    }

    private static string SanitizeFileName(string fileName)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var currentFilename = fileName;
        var previousFilename = "";

        while (currentFilename != previousFilename)
        {
            previousFilename = currentFilename;
            currentFilename = string.Join("_", fileName.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries))
                .TrimEnd('.').TrimEnd();
        }

        return currentFilename;
    }
}

#region Models

public class TrpcResponse<T>
{
    [JsonPropertyName("result")]
    public TrpcResult<T>? Result { get; set; }
}

public class TrpcResult<T>
{
    [JsonPropertyName("data")]
    public TrpcData<T>? Data { get; set; }
}

public class TrpcData<T>
{
    [JsonPropertyName("json")]
    public T? Json { get; set; }
}

public class CollectionsData
{
    [JsonPropertyName("collection")]
    public Collection? Collection { get; set; }
}

public class Collection
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }
}

public class PostsData
{
    [JsonPropertyName("items")]
    public List<Post> Items { get; set; } = [];

    [JsonPropertyName("nextCursor")]
    public string? NextCursor { get; set; }
}

public class Post
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("user")]
    public PostUser? User { get; set; }

    [JsonPropertyName("images")]
    public List<PostImage> Images { get; set; } = [];
}

public class PostUser
{
    [JsonPropertyName("username")]
    public string? Username { get; set; }
}

public class PostImage
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("url")]
    public string? Url { get; set; }
}

#endregion