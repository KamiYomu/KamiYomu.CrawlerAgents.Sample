using System;

using KamiYomu.CrawlerAgents.Core;
using KamiYomu.CrawlerAgents.Core.Catalog;
using KamiYomu.CrawlerAgents.Sample;

using Microsoft.Extensions.Logging;

using Spectre.Console;

#region startup
AnsiConsole.MarkupLine("[bold underline green]KamiYomu AgentCrawler Validator[/]\n");
using ILoggerFactory loggerFactory = LoggerFactory.Create(builder =>
{
    _ = builder.AddConsole();
});

ILogger logger = loggerFactory.CreateLogger<Program>();

Dictionary<string, object> options = new()
{
    { CrawlerAgentSettings.DefaultInputs.KamiYomuILogger, logger },
    { "Mirror", "https://kamiyomu.com" }
};
ICrawlerAgent crawler = new SampleCrawlerAgent(options);
using HttpClientHandler handler = new()
{
    AllowAutoRedirect = true
};
using HttpClient httpClient = new(handler);
httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
    CrawlerAgentSettings.HttpUserAgent);

List<(string Method, bool Success, string Message)> results = [];

PagedResult<Manga> mangaResult = await crawler.SearchAsync("Sample", new PaginationOptions(1, 30), CancellationToken.None);
#endregion

#region Test GetFaviconAsync
try
{
    AnsiConsole.MarkupLine($"[bold underline green] Start Testing the method {nameof(ICrawlerAgent.GetFaviconAsync)}... [/]\n");
    Uri? favicon = await crawler.GetFaviconAsync(CancellationToken.None);
    results.Add((nameof(ICrawlerAgent.GetFaviconAsync), favicon != null && favicon.IsAbsoluteUri, favicon?.ToString() ?? "Returned null"));
}
catch (Exception ex)
{
    results.Add((nameof(ICrawlerAgent.GetFaviconAsync), false, ex.Message));
}
#endregion

#region Test SearchAsync
try
{
    AnsiConsole.MarkupLine($"[bold underline green] Start Testing the method {nameof(ICrawlerAgent.SearchAsync)}... [/]\n");
    Thread.Sleep(1000);
    int count = mangaResult.Data?.Count() ?? 0;
    results.Add((nameof(ICrawlerAgent.SearchAsync), count > 0, $"Returned {count} result(s)"));
    results.Add(($"{nameof(Manga.Id)} is not empty", !string.IsNullOrWhiteSpace(mangaResult.Data.ElementAt(0).Id), $"{mangaResult.Data.ElementAt(0).Id}"));
    results.Add(($"{nameof(Manga.Title)} is not empty", !string.IsNullOrWhiteSpace(mangaResult.Data.ElementAt(0).Title), $"{mangaResult.Data.ElementAt(0).Title}"));
    results.Add(($"{nameof(Manga.Description)} is not empty", !string.IsNullOrWhiteSpace(mangaResult.Data.ElementAt(0).Description), $"{mangaResult.Data.ElementAt(0).Description}"));
    results.Add(($"{nameof(Manga.WebSiteUrl)} is well formated", Uri.IsWellFormedUriString(mangaResult.Data.ElementAt(0).WebSiteUrl, UriKind.Absolute), $"{mangaResult.Data.ElementAt(0).WebSiteUrl}"));
    results.Add(($"{nameof(Manga.CoverUrl)} is well formated", Uri.IsWellFormedUriString(mangaResult.Data.ElementAt(0).CoverUrl.ToString(), UriKind.Absolute), $"{mangaResult.Data.ElementAt(0).CoverUrl}"));
    results.Add(($"{nameof(Manga.WebSiteUrl)} is found", (await httpClient.GetAsync(mangaResult.Data.ElementAt(0).WebSiteUrl.ToString())).IsSuccessStatusCode, $"{mangaResult.Data.ElementAt(0).WebSiteUrl}"));
    results.Add(($"{nameof(Manga.CoverUrl)} is found", (await httpClient.GetAsync(mangaResult.Data.ElementAt(0).CoverUrl.ToString())).IsSuccessStatusCode, $"{mangaResult.Data.ElementAt(0).CoverFileName}"));
    results.Add(($"{nameof(Manga.CoverFileName)} is not empty", !string.IsNullOrWhiteSpace(mangaResult.Data.ElementAt(0).CoverFileName), $"{mangaResult.Data.ElementAt(0).CoverFileName}"));
    results.Add(($"{nameof(Manga.IsFamilySafe)} is not empty", true, $"{mangaResult.Data.ElementAt(0).IsFamilySafe}"));



    using HttpResponseMessage response = await httpClient.GetAsync(
        mangaResult.Data.ElementAt(0).CoverUrl,
        HttpCompletionOption.ResponseHeadersRead);

    byte[] coverImageBytes = await response.Content.ReadAsByteArrayAsync();

    results.Add((nameof(HttpClient.GetByteArrayAsync), coverImageBytes.Any(), $"Returned {coverImageBytes.Count()} bytes as result(s)"));

}
catch (Exception ex)
{
    results.Add((nameof(ICrawlerAgent.SearchAsync), false, ex.Message));
}
#endregion

#region Test GetByIdAsync
try
{
    AnsiConsole.MarkupLine($"[bold underline green] Start Testing the method {nameof(ICrawlerAgent.GetByIdAsync)}... [/]\n");
    Thread.Sleep(1000);
    Manga manga = await crawler.GetByIdAsync(mangaResult.Data.ElementAt(0)?.Id, CancellationToken.None);
    bool any = mangaResult.Data?.Any() ?? false;
    results.Add((nameof(ICrawlerAgent.GetByIdAsync), any, $"Returning {manga.Title} and its cover {manga.CoverUrl}."));
    results.Add(($"{nameof(Manga.Id)} is not empty", !string.IsNullOrWhiteSpace(manga.Id), $"{manga.Id}"));
    results.Add(($"{nameof(Manga.Title)} is not empty", !string.IsNullOrWhiteSpace(manga.Title), $"{manga.Title}"));
    results.Add(($"{nameof(Manga.Description)} is not empty", !string.IsNullOrWhiteSpace(manga.Description), $"{manga.Description}"));
    results.Add(($"{nameof(Manga.WebSiteUrl)} is well formated", Uri.IsWellFormedUriString(manga.WebSiteUrl, UriKind.Absolute), $"{manga.WebSiteUrl}"));
    results.Add(($"{nameof(Manga.CoverUrl)} is well formated", Uri.IsWellFormedUriString(manga.CoverUrl.ToString(), UriKind.Absolute), $"{manga.CoverFileName}"));
    results.Add(($"{nameof(Manga.WebSiteUrl)} is found", (await httpClient.GetAsync(manga.WebSiteUrl.ToString())).IsSuccessStatusCode, $"{manga.WebSiteUrl}"));
    results.Add(($"{nameof(Manga.CoverUrl)} is found", (await httpClient.GetAsync(manga.CoverUrl.ToString())).IsSuccessStatusCode, $"{manga.CoverUrl}"));
    results.Add(($"{nameof(Manga.CoverFileName)} is not empty", !string.IsNullOrWhiteSpace(manga.CoverFileName), $"{manga.CoverFileName}"));
    results.Add(($"{nameof(Manga.IsFamilySafe)} is not empty", true, $"{manga.IsFamilySafe}"));
    results.Add(($"{nameof(Manga.IsFamilySafe)} is not empty", true, $"{manga.IsFamilySafe}"));

    ;

}
catch (Exception ex)
{
    results.Add((nameof(ICrawlerAgent.GetByIdAsync), false, ex.Message));
}
#endregion

#region Test GetChaptersAsync
try
{
    AnsiConsole.MarkupLine($"[bold underline green] Start Testing the method {nameof(ICrawlerAgent.GetChaptersAsync)}... [/]\n");
    Thread.Sleep(1000);
    PagedResult<Chapter>? chaptersResult = await crawler.GetChaptersAsync(mangaResult.Data.First(), new PaginationOptions(0, 300, 300), CancellationToken.None);
    int count = chaptersResult?.Data.Count() ?? 0;
    List<decimal>? allNumbers = chaptersResult.Data?
    .Select(c => c.Number)
    .OrderByDescending(p => p)
    .ToList();

    string numbersString = allNumbers != null ? string.Join(";", allNumbers) : string.Empty;
    results.Add((nameof(ICrawlerAgent.GetChaptersAsync), !string.IsNullOrWhiteSpace(chaptersResult?.Data.FirstOrDefault()?.Id), $"Returned {chaptersResult?.Data.FirstOrDefault()?.Id} result(s)"));
    results.Add(($"{nameof(Chapter.Id)} is not empty", !string.IsNullOrWhiteSpace(chaptersResult.Data?.FirstOrDefault().Id), $"{chaptersResult.Data?.FirstOrDefault().Id}"));
    results.Add(($"{nameof(Chapter.Title)} is not empty", !string.IsNullOrWhiteSpace(chaptersResult?.Data?.FirstOrDefault().Title), $"{chaptersResult?.Data?.FirstOrDefault().Title}"));
    results.Add(($"{nameof(Chapter.Uri)} is well formatted", Uri.IsWellFormedUriString(chaptersResult.Data?.FirstOrDefault().Uri.ToString(), UriKind.Absolute), $"{chaptersResult.Data?.FirstOrDefault().Uri}"));
    results.Add(($"{nameof(Chapter.Uri)} is found", (await httpClient.GetAsync(chaptersResult.Data?.FirstOrDefault().Uri.ToString())).IsSuccessStatusCode, $"{chaptersResult.Data?.FirstOrDefault().Uri}"));
    results.Add(($"{nameof(Chapter.Number)} is not empty", allNumbers.Count > 0, numbersString));
    results.Add(($"{nameof(allNumbers.Count)} is not empty", allNumbers.Count > 0, allNumbers.Count.ToString()));
    results.Add(($"{nameof(Chapter.ParentManga)} is not empty", chaptersResult.Data?.FirstOrDefault() != null, $"{chaptersResult.Data?.FirstOrDefault().ParentManga.Title}"));
}
catch (Exception ex)
{
    results.Add((nameof(ICrawlerAgent.GetChaptersAsync), false, ex.Message));
}
#endregion

#region Test GetByteArrayAsync
try
{
    AnsiConsole.MarkupLine($"[bold underline green] Start Testing the method {nameof(HttpClient.GetByteArrayAsync)}... [/]\n");
    Thread.Sleep(1000);
    PagedResult<Chapter> chaptersResult = await crawler.GetChaptersAsync(mangaResult.Data.First(), new PaginationOptions(0, 10), CancellationToken.None);
    Thread.Sleep(1000);
    IEnumerable<Page> chapterImages = await crawler.GetChapterPagesAsync(chaptersResult.Data.Last(), CancellationToken.None);

    httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
        CrawlerAgentSettings.HttpUserAgent);

    using HttpResponseMessage response = await httpClient.GetAsync(
        chapterImages.ElementAt(0).ImageUrl,
        HttpCompletionOption.ResponseHeadersRead);

    byte[] imageBytes = await response.Content.ReadAsByteArrayAsync();

    results.Add(($"{nameof(Chapter.Uri)} is not empty", !string.IsNullOrWhiteSpace(chaptersResult.Data?.FirstOrDefault().Uri.ToString()), $"{chaptersResult.Data?.FirstOrDefault().Uri}"));
    results.Add(($"Count is not empty", chapterImages.Any(), $"{chapterImages.Count()}"));
    results.Add((nameof(HttpClient.GetByteArrayAsync), chapterImages.Any(), $"Returned {imageBytes.Count()} bytes as result(s)"));
}
catch (Exception ex)
{
    results.Add((nameof(HttpClient.GetByteArrayAsync), false, ex.Message));
}
#endregion

#region Display results
Table table = new Table()
    .Title("[yellow]Test Results[/]")
    .Border(TableBorder.Rounded)
    .AddColumn("Method")
    .AddColumn("Status")
    .AddColumn("Message");

foreach ((string method, bool success, string message) in results)
{
    _ = table.AddRow(
        $"[bold]{method}[/]",
        success ? "[green]✔ Passed[/]" : "[red]✘ Failed[/]",
         Markup.Escape(message)
    );
}

AnsiConsole.Write(table);
#endregion
