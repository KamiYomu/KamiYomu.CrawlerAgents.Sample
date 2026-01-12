using System.ComponentModel;
using System.Globalization;
using System.Text.RegularExpressions;

using HtmlAgilityPack;

using KamiYomu.CrawlerAgents.Core;
using KamiYomu.CrawlerAgents.Core.Catalog;
using KamiYomu.CrawlerAgents.Core.Catalog.Builders;
using KamiYomu.CrawlerAgents.Core.Catalog.Definitions;
using KamiYomu.CrawlerAgents.Core.Inputs;

using Microsoft.Extensions.Logging;

using PuppeteerSharp;

using Page = KamiYomu.CrawlerAgents.Core.Catalog.Page;

namespace KamiYomu.CrawlerAgents.Sample;

[DisplayName("KamiYomu Crawler Agent – kamiyomu.com")]
[CrawlerSelect("Language", "Sample field, it has no effect", ["en", "pt_br", "pt"])]
[CrawlerCheckBox("ContentRating", "Sample field, it has no effect", true, "safe", 2, ["safe", "suggestive", "erotica", "pornographic"])]
[CrawlerSelect("Mirror", "MangaPark offers multiple mirror sites that may be online and useful. You can check their current status at https://mangaparkmirrors.pages.dev/",
    true, 0, [
        "https://kamiyomu.com",
        "https://kamiyomu.github.io",
    ])]
public class SampleCrawlerAgent : AbstractCrawlerAgent, ICrawlerAgent
{
    private bool _disposed = false;
    private readonly Uri _baseUri;
    private readonly string _language;
    private readonly bool _isSafe;
    private readonly bool _isSuggestive;
    private readonly bool _isErotica;
    private readonly bool _isPornographic;
    private readonly Lazy<Task<IBrowser>> _browser;
    public SampleCrawlerAgent(IDictionary<string, object> options) : base(options)
    {
        _browser = new Lazy<Task<IBrowser>>(CreateBrowserAsync, true);
        _language = Options.TryGetValue("Language", out object? language) && language is string languageValue ? languageValue : "en";
        _isSafe = Options.TryGetValue("ContentRating.safe", out object safe) && safe is bool safeValue && safeValue;
        _isSuggestive = Options.TryGetValue("ContentRating.suggestive", out object suggestive) && suggestive is bool suggestiveValue && suggestiveValue;
        _isErotica = Options.TryGetValue("ContentRating.erotica", out object erotica) && erotica is bool eroticaValue && eroticaValue;
        _isPornographic = Options.TryGetValue("ContentRating.pornographic", out object pornographic) && pornographic is bool pornographicValue && pornographicValue;
        string mirrorUrl = Options.TryGetValue("Mirror", out object? mirror) && mirror is string mirrorValue ? mirrorValue : "https://.net";
        _baseUri = new Uri(mirrorUrl);
    }

    protected virtual async Task DisposeAsync(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                if (_browser.IsValueCreated)
                {
                    await _browser.Value.Result.CloseAsync();
                    _browser.Value.Result.Dispose();
                }
            }

            _disposed = true;
        }
    }

    ~SampleCrawlerAgent()
    {
        _ = DisposeAsync(disposing: false);
    }

    // <inheritdoc/>
    public void Dispose()
    {
        _ = DisposeAsync(disposing: true);
        GC.SuppressFinalize(this);
    }

    public Task<IBrowser> GetBrowserAsync()
    {
        return _browser.Value;
    }

    private async Task<IBrowser> CreateBrowserAsync()
    {
        LaunchOptions launchOptions = new()
        {
            Headless = true,
            Timeout = TimeoutMilliseconds,
            Args = [
                "--disable-blink-features=AutomationControlled",
                "--no-sandbox",
                "--disable-dev-shm-usage"
            ]
        };

        return await Puppeteer.LaunchAsync(launchOptions);
    }

    private static bool IsGenreNotFamilySafe(string p)
    {
        return !string.IsNullOrWhiteSpace(p) && (p.Contains("adult", StringComparison.OrdinalIgnoreCase)
            || p.Contains("harem", StringComparison.OrdinalIgnoreCase)
            || p.Contains("hentai", StringComparison.OrdinalIgnoreCase)
            || p.Contains("ecchi", StringComparison.OrdinalIgnoreCase)
            || p.Contains("violence", StringComparison.OrdinalIgnoreCase)
            || p.Contains("smut", StringComparison.OrdinalIgnoreCase)
            || p.Contains("shota", StringComparison.OrdinalIgnoreCase)
            || p.Contains("sexual", StringComparison.OrdinalIgnoreCase));
    }

    private async Task PreparePageForNavigationAsync(IPage page)
    {
        page.Console += (sender, e) =>
        {
            // e.Message contains the console message
            Logger?.LogDebug($"[Browser Console] {e.Message.Type}: {e.Message.Text}");

            // You can also inspect arguments
            if (e.Message.Args != null)
            {
                foreach (IJSHandle? arg in e.Message.Args)
                {
                    Logger?.LogDebug($"   Arg: {arg.RemoteObject.Value}");
                }
            }
        };

        Logger.LogInformation("Parameters: ");
        Logger.LogInformation("{parameter}: {value}", nameof(_language), _language);
        Logger.LogInformation("{parameter}: {value}", nameof(_isSafe), _isSafe);
        Logger.LogInformation("{parameter}: {value}", nameof(_isSuggestive), _isSuggestive);
        Logger.LogInformation("{parameter}: {value}", nameof(_isErotica), _isErotica);
        Logger.LogInformation("{parameter}: {value}", nameof(_isPornographic), _isPornographic);
        Logger.LogInformation("{parameter}: {value}", nameof(_baseUri), _baseUri);

        await page.EmulateTimezoneAsync("America/Toronto");

        DateTime fixedDate = DateTime.Now;

        string fixedDateIso = fixedDate.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);

        _ = await page.EvaluateExpressionOnNewDocumentAsync($@"
            // Freeze time to a specific date
            const fixedDate = new Date('{fixedDateIso}');
            Date = class extends Date {{
                constructor(...args) {{
                    if (args.length === 0) {{
                        return fixedDate;
                    }}
                    return super(...args);
                }}
                static now() {{
                    return fixedDate.getTime();
                }}
            }};
        ");
    }

    private string NormalizeUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return string.Empty;
        }

        if (!url.StartsWith("/") && Uri.TryCreate(url, UriKind.Absolute, out Uri? absolute))
        {
            return absolute.ToString();
        }

        Uri resolved = new(_baseUri, url);
        return resolved.ToString();
    }
    public Task<Uri> GetFaviconAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult(new Uri($"https://avatars.githubusercontent.com/u/242082676?s=200&v=4"));
    }

    public async Task<PagedResult<Manga>> SearchAsync(string titleName, PaginationOptions paginationOptions, CancellationToken cancellationToken)
    {
        IBrowser browser = await GetBrowserAsync();
        using IPage page = await browser.NewPageAsync();
        await PreparePageForNavigationAsync(page);
        await page.SetUserAgentAsync(HttpClientDefaultUserAgent);
        int pageNumber = string.IsNullOrWhiteSpace(paginationOptions?.ContinuationToken)
                        ? 1
                        : int.Parse(paginationOptions.ContinuationToken);

        Uri targetUri = new(new Uri(_baseUri.ToString()), $"samples/gallery/?q={titleName}");
        _ = await page.GoToAsync(targetUri.ToString(), new NavigationOptions
        {
            WaitUntil = [WaitUntilNavigation.DOMContentLoaded, WaitUntilNavigation.Load],
            Timeout = TimeoutMilliseconds
        });


        string content = await page.GetContentAsync();

        HtmlDocument document = new();
        document.LoadHtml(content);

        List<Manga> mangas = [];
        if (pageNumber > 0)
        {
            HtmlNodeCollection nodes = document.DocumentNode.SelectNodes("//*[@class='manga-card']");
            if (nodes != null)
            {
                foreach (HtmlNode divNode in nodes)
                {
                    Manga manga = ConvertToMangaFromList(divNode);
                    mangas.Add(manga);
                }
            }
        }

        return PagedResultBuilder<Manga>.Create()
            .WithData(mangas)
            .WithPaginationOptions(new PaginationOptions((pageNumber + 1).ToString()))
            .Build();
    }

    private Manga ConvertToMangaFromList(HtmlNode divNode)
    {
        // id (can be derived from href)
        string websiteUrl = divNode.GetAttributeValue("href", string.Empty);
        string id = websiteUrl.Trim('/').Split('/').Last();

        // title
        string title =
            divNode.SelectSingleNode(".//strong[contains(@class,'manga-title')]")
                   ?.InnerText
                   ?.Trim() ?? "Unknown Title";

        // author
        string author =
            divNode.SelectSingleNode(".//span[contains(@class,'manga-author')]")
                   ?.InnerText
                   ?.Replace("By", string.Empty)
                   ?.Trim() ?? "Unknown Author";

        // cover image
        HtmlNode imgNode = divNode.SelectSingleNode(".//img");
        string coverUrl = NormalizeUrl(imgNode?.GetAttributeValue("src", string.Empty) ?? string.Empty);
        string coverFileName = Path.GetFileName(coverUrl);

        // genres
        List<string> genres =
            divNode.SelectNodes(".//span[contains(@class,'card-genre-tag')]")
                   ?.Select(g => g.InnerText.Trim())
                   .ToList()
            ?? [];

        // alternative titles (from data-title attribute)
        List<string> altTitles = [];
        string dataTitle = divNode.GetAttributeValue("data-title", string.Empty);
        if (!string.IsNullOrWhiteSpace(dataTitle))
        {
            altTitles.Add(dataTitle);
        }

        // --- Build Manga ---
        Manga manga = MangaBuilder.Create()
            .WithId(id)
            .WithTitle(title)
            .WithAuthors([author])
            .WithDescription("No Description Available")
            .WithCoverUrl(string.IsNullOrEmpty(coverUrl) ? null : new Uri(coverUrl))
            .WithCoverFileName(coverFileName)
            .WithWebsiteUrl(NormalizeUrl(websiteUrl))
            .WithAlternativeTitles(
                altTitles.Select((p, i) => new { i = i.ToString(), p })
                         .ToDictionary(x => x.i, x => x.p))
            .WithLatestChapterAvailable(0)
            .WithLastVolumeAvailable(0)
            .WithTags([.. genres])
            .WithOriginalLanguage(_language)
            .WithIsFamilySafe(!genres.Any(IsGenreNotFamilySafe))
            .Build();

        return manga;
    }


    public async Task<Manga> GetByIdAsync(string id, CancellationToken cancellationToken)
    {
        IBrowser browser = await GetBrowserAsync();
        using IPage page = await browser.NewPageAsync();
        await PreparePageForNavigationAsync(page);
        await page.SetUserAgentAsync(HttpClientDefaultUserAgent);

        string finalUrl = new Uri(_baseUri, $"samples/manga/{id}").ToString();
        IResponse response = await page.GoToAsync(finalUrl, new NavigationOptions
        {
            WaitUntil = [WaitUntilNavigation.DOMContentLoaded, WaitUntilNavigation.Load],
            Timeout = TimeoutMilliseconds
        });

        string content = await page.GetContentAsync();
        HtmlDocument document = new();
        document.LoadHtml(content);
        HtmlNode rootNode = document.DocumentNode.SelectSingleNode("//*[@class='manga-header']"); ;
        Manga manga = ConvertToMangaFromSingleBook(rootNode, id);

        return manga;
    }

    private Manga ConvertToMangaFromSingleBook(HtmlNode rootNode, string id)
    {
        // --- Title ---
        string title =
            rootNode.SelectSingleNode(".//h1")
                      ?.InnerText
                      ?.Trim() ?? "Unknown Title";

        // --- Alternative titles (use title as fallback) ---
        List<string> altTitles = [title];

        // --- Description ---
        string description =
            rootNode.SelectSingleNode(".//p[contains(@class,'description')]")
                      ?.InnerText
                      ?.Trim() ?? "No Description Available";

        // --- Authors ---
        List<string> authors = [];

        HtmlNode authorNode =
            rootNode.SelectSingleNode(
                ".//div[contains(@class,'meta-item')][span[text()='Author']]");

        if (authorNode != null)
        {
            string author =
                HtmlEntity.DeEntitize(authorNode.InnerText)
                          .Replace("Author", string.Empty)
                          .Trim();

            if (!string.IsNullOrWhiteSpace(author))
            {
                authors.Add(author);
            }
        }

        // --- Genres ---
        List<string> genres =
            rootNode.SelectNodes(".//span[contains(@class,'genre-pill')]")
                      ?.Select(g => g.InnerText.Trim())
                      .ToList()
            ?? [];

        // --- Cover ---
        HtmlNode imgNode = rootNode.SelectSingleNode(".//div[contains(@class,'manga-cover')]//img");

        string coverUrl = NormalizeUrl(imgNode?.GetAttributeValue("src", string.Empty) ?? string.Empty);
        string coverFileName = Path.GetFileName(coverUrl);

        // --- Website URL ---
        string href = NormalizeUrl($"samples/manga/{rootNode.SelectSingleNode("//link[@rel='canonical']")
            ?.GetAttributeValue("href", string.Empty)
            ?? id}");

        // --- Release Status ---
        string releaseStatus =
            rootNode.SelectSingleNode(
                ".//div[contains(@class,'meta-item')][span[text()='Status']]")
            ?.InnerText
            ?.Replace("Status", string.Empty)
            ?.Trim()
            ?? "Ongoing";

        // --- Build Manga ---
        Manga manga = MangaBuilder.Create()
            .WithId(id)
            .WithTitle(title)
            .WithAlternativeTitles(
                altTitles.Select((p, i) => new { i = i.ToString(), p })
                         .ToDictionary(x => x.i, x => x.p))
            .WithDescription(description)
            .WithAuthors([.. authors])
            .WithTags([.. genres])
            .WithCoverUrl(string.IsNullOrEmpty(coverUrl) ? null : new Uri(coverUrl))
            .WithCoverFileName(coverFileName)
            .WithWebsiteUrl(NormalizeUrl(href))
            .WithIsFamilySafe(!genres.Any(IsGenreNotFamilySafe))
            .WithReleaseStatus(releaseStatus.ToLowerInvariant() switch
            {
                "completed" => ReleaseStatus.Completed,
                "hiatus" => ReleaseStatus.OnHiatus,
                "cancelled" => ReleaseStatus.Cancelled,
                _ => ReleaseStatus.Continuing,
            })
            .Build();

        return manga;
    }


    public async Task<PagedResult<Chapter>> GetChaptersAsync(Manga manga, PaginationOptions paginationOptions, CancellationToken cancellationToken)
    {
        IBrowser browser = await GetBrowserAsync();
        using IPage page = await browser.NewPageAsync();
        await PreparePageForNavigationAsync(page);
        await page.SetUserAgentAsync(HttpClientDefaultUserAgent);

        string finalUrl = new Uri(_baseUri, $"samples/manga/{manga.Id}").ToString();
        IResponse response = await page.GoToAsync(finalUrl, new NavigationOptions
        {
            WaitUntil = [WaitUntilNavigation.DOMContentLoaded, WaitUntilNavigation.Load],
            Timeout = TimeoutMilliseconds
        });


        string content = await page.GetContentAsync();

        HtmlDocument document = new();
        document.LoadHtml(content);
        HtmlNode rootNode = document.DocumentNode.SelectSingleNode("//*[@class='chapter-section']");
        IEnumerable<Chapter> chapters = ConvertChaptersFromSingleBook(manga, rootNode);

        return PagedResultBuilder<Chapter>.Create()
                                          .WithPaginationOptions(new PaginationOptions(chapters.Count(), chapters.Count(), chapters.Count()))
                                          .WithData(chapters)
                                          .Build();
    }

    private IEnumerable<Chapter> ConvertChaptersFromSingleBook(Manga manga, HtmlNode rootNode)
    {
        List<Chapter> chapters = [];

        HtmlNodeCollection chapterDivs =
            rootNode.SelectNodes("//section[@id='chapters']//a[contains(@class,'chapter-item')]");

        if (chapterDivs == null)
        {
            return chapters;
        }

        foreach (HtmlNode chapterDiv in chapterDivs)
        {
            // --- URI ---
            string href = chapterDiv.GetAttributeValue("href", string.Empty);
            if (string.IsNullOrWhiteSpace(href))
            {
                continue;
            }

            Uri uri = new(NormalizeUrl(href));

            // --- Title ---
            string rawTitle =
                chapterDiv.SelectSingleNode(".//span[1]")
                          ?.InnerText
                          ?.Trim() ?? "Chapter";

            // --- Chapter number ---
            decimal number = 0;
            decimal volume = 0;

            // Expected format: "Chapter 1: Initializing Agent"
            Match match = Regex.Match(rawTitle, @"Chapter\s+([\d\.]+)", RegexOptions.IgnoreCase);
            if (match.Success && decimal.TryParse(match.Groups[1].Value, out decimal parsed))
            {
                number = parsed;
            }

            // --- Chapter ID (stable & deterministic) ---
            string chapterId = $"{manga.Id}-ch-{number:0.##}";

            // --- Build Chapter ---
            Chapter chapter = ChapterBuilder.Create()
                .WithId(chapterId)
                .WithTitle(rawTitle)
                .WithParentManga(manga)
                .WithVolume(volume)
                .WithNumber(number)
                .WithUri(uri)
                .WithTranslatedLanguage(_language)
                .Build();

            chapters.Add(chapter);
        }

        return chapters;
    }


    public async Task<IEnumerable<Page>> GetChapterPagesAsync(Chapter chapter, CancellationToken cancellationToken)
    {
        IBrowser browser = await GetBrowserAsync();
        using IPage page = await browser.NewPageAsync();

        await PreparePageForNavigationAsync(page);
        await page.SetUserAgentAsync(HttpClientDefaultUserAgent);

        _ = await page.GoToAsync(chapter.Uri.ToString(), new NavigationOptions
        {
            WaitUntil = [WaitUntilNavigation.DOMContentLoaded, WaitUntilNavigation.Load],
            Timeout = TimeoutMilliseconds
        });

        string content = await page.GetContentAsync();
        HtmlDocument document = new();
        document.LoadHtml(content);

        HtmlNodeCollection pageNodes = document.DocumentNode.SelectNodes("//div[@class='manga-stack']//img[@class='manga-page']");

        return ConvertToChapterPages(chapter, pageNodes);
    }

    private IEnumerable<Page> ConvertToChapterPages(
    Chapter chapter,
    HtmlNodeCollection pageNodes)
    {
        if (pageNodes == null || pageNodes.Count == 0)
        {
            return [];
        }

        List<Page> pages = [];

        int pageNumber = 1;

        foreach (HtmlNode node in pageNodes)
        {
            // --- Image URL ---
            string imageUrl = NormalizeUrl(node.GetAttributeValue("src", string.Empty));
            if (string.IsNullOrWhiteSpace(imageUrl))
            {
                continue;
            }

            // --- Page ID (stable & deterministic) ---
            string idAttr = $"{chapter.Id}-p-{pageNumber}";

            // --- Build Page ---
            Page page = PageBuilder.Create()
                .WithChapterId(chapter.Id)
                .WithId(idAttr)
                .WithPageNumber(pageNumber)
                .WithImageUrl(new Uri(NormalizeUrl(imageUrl)))
                .WithParentChapter(chapter)
                .Build();

            pages.Add(page);
            pageNumber++;
        }

        return pages;
    }

}
