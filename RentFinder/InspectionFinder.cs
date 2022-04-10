using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using HtmlAgilityPack;

namespace RentFinder;

public static class InspectionFinder
{
    private static readonly HttpClient HttpClient;

    static InspectionFinder()
    {
        HttpClient = new HttpClient
        {
            DefaultRequestHeaders =
            {
                {"User-Agent", "Mozilla/5.0 (X11; Linux x86_64; rv:91.0) Gecko/20100101 Firefox/91.0"},
                {"Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8"},
                {"Accept-Encoding", "gzip, deflate, br"}
            },
        };
    }

    public static async Task Search(Options options)
    {
        var page = 1;
        bool @continue;
        const string horizontalRule = "================================================================";
        Console.WriteLine(horizontalRule);
        do
        {
            var url = $"https://www.domain.com.au/rent/{options.Location}/inspection-times/?inspectiondate={options.Day:yyyy-MM-dd}&page={page++}";
            var data = (await DeserializePageData(url)).ToList();

            @continue = data.Any();
            foreach (var listing in data)
            {
                var list = await listing;
                Console.WriteLine($"Address:            {list.Location}");
                Console.WriteLine($"Beds:               {list.Beds}");
                Console.WriteLine($"Rent:               {list.Price:$0.00}");
                Console.WriteLine($"Rent per Bed:       {list.PricePerBed:$0.00}");
                Console.WriteLine($"Air Conditioning:   {(list.AirCon ? '✅' : '❓')}");
                Console.WriteLine($"Real Shower:        {ConvertToEmoji(list.RealShower)}");
                Console.WriteLine(horizontalRule);
            }
        } while (@continue);
    }

    private static string ConvertToEmoji(Answer listRealShower)
    {
        return listRealShower switch
        {
            Answer.Maybe => "❓",
            Answer.No => "❌",
            Answer.Yes => "✅",
            _ => throw new ArgumentOutOfRangeException(nameof(listRealShower), listRealShower, null)
        };
    }

    private static async Task<IEnumerable<Task<Listing>>> DeserializePageData(string url)
    {
        var web = await GetDocument(url);
        return web.DocumentNode.Descendants("div")
            .Where(x => x.HasMatchingDataId("listing-card-inspection"))
            .Select(ConvertToListing);
    }

    private static async Task<HtmlDocument> GetDocument(string url)
    {
        var stream = await HttpClient.GetStreamAsync(url);
        var web = new HtmlDocument();
        web.Load(new GZipStream(stream, CompressionMode.Decompress));
        return web;
    }

    private static async Task<Listing> ConvertToListing(HtmlNode listingNode)
    {
        var listing = new Listing();

        var bedsRegex = new Regex(@"(\d+) Beds?");
        var bedsText = listingNode.Descendants("span")
            .Single(x => x.HasMatchingDataId("property-features-text-container") && bedsRegex.IsMatch(x.InnerText)).InnerText;
        var bedsNumberString = bedsRegex.Match(bedsText).Groups[1].Value;
        listing.Beds = int.Parse(bedsNumberString);

        var priceRegex = new Regex(@"\$\d+(\.\d+)?");
        var priceText = listingNode.Descendants("p")
            .Single(x => x.HasMatchingDataId("listing-card-price"))
            .InnerText;
        listing.Price = decimal.Parse(priceRegex.Match(priceText).Value, NumberStyles.Currency);

        listing.Location = listingNode.Descendants("h2")
            .Single(x => x.HasMatchingDataId("address-wrapper"))
            .InnerText;

        var listingPage = listingNode.Descendants("a").Select(x => x.GetAttributeValue("href", null)).Single();
        var pageHtml = await GetDocument(listingPage);
        var listingJsonRaw = pageHtml.DocumentNode.Descendants("script")
            .Single(x => x.Id.Equals("__NEXT_DATA__")).InnerText;

        var pageProps = JsonNode.Parse(listingJsonRaw)!["props"]!["pageProps"]!;
        var searchTextBuilder = new StringBuilder();
        searchTextBuilder.AppendLine(string.Join(' ', pageProps["description"]!.AsArray().Select(x => (string?) x)));

        var featuresData = pageProps["features"];
        if (featuresData != null)
        {
            searchTextBuilder.AppendLine(string.Join(' ', featuresData.AsArray().Select(x => (string?) x)));
        }

        var structuredFeaturesData = pageProps["structuredFeatures"];
        if (structuredFeaturesData != null)
        {
            searchTextBuilder.AppendLine(string.Join(' ', structuredFeaturesData.AsArray().Select(x => x?["name"])));
        }

        var searchText = searchTextBuilder.ToString();
        listing.AirCon = Regex.IsMatch(searchText, @"\b(A/?C|air.?con(ditioning)?|split.?system|cooling)\b", RegexOptions.IgnoreCase);
        var showerOverBath = Regex.IsMatch(searchText, @"\b(shower.over.bath)\b", RegexOptions.IgnoreCase);
        var walkInShower = Regex.IsMatch(searchText, @"\b(walk.in shower)\b", RegexOptions.IgnoreCase);
        if (walkInShower) listing.RealShower = Answer.Yes;
        else if (showerOverBath) listing.RealShower = Answer.No;

        return listing;
    }
}