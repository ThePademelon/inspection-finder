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
            var data = DeserializePageData(url);

            @continue = false;
            await foreach (var listing in data)
            {
                @continue = true;
                if (!MatchesFilter(listing)) continue;
                Console.WriteLine($"Address:            {listing.Location}");
                Console.WriteLine($"Beds:               {listing.Beds}");
                Console.WriteLine($"Rent:               {listing.Price:$0.00}");
                Console.WriteLine($"Rent per Bed:       {listing.PricePerBed:$0.00}");
                Console.WriteLine($"Air Conditioning:   {ConvertToEmoji(listing.AirCon)}");
                Console.WriteLine($"Real Shower:        {ConvertToEmoji(listing.RealShower)}");
                Console.WriteLine($"Carpeted:           {ConvertToEmoji(listing.Carpeted)}");
                Console.WriteLine($"URL:                {listing.Url}");
                Console.WriteLine(horizontalRule);
            }
        } while (@continue);
    }

    private static bool MatchesFilter(Listing listing)
    {
        // TODO: YOUR FILTER HERE
        return true;
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

    private static async IAsyncEnumerable<Listing> DeserializePageData(string url)
    {
        var web = await GetDocument(url);
        var listingNodes = web.DocumentNode.Descendants("div")
            .Where(x => x.HasMatchingDataId("listing-card-inspection"));
        foreach (var listingNode in listingNodes)
        {
            yield return await ConvertToListing(listingNode);
        }
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
        listing.AirCon = Regex.IsMatch(searchText, @"\b(A/?C|air.?con(ditioning)?|split.?system|cooling)\b", RegexOptions.IgnoreCase) ? Answer.Yes : Answer.Maybe;

        var showerOverBath = Regex.IsMatch(searchText, @"\b(shower.over.bath(tub)?s?)\b", RegexOptions.IgnoreCase);
        var walkInShower = Regex.IsMatch(searchText, @"\b(walk.in showers?)\b", RegexOptions.IgnoreCase);
        listing.RealShower = ResolveAnswer(walkInShower, showerOverBath);

        var carpet = Regex.IsMatch(searchText, @"\bcarpet(ed|ing)?\b");
        var woodFloor = Regex.IsMatch(searchText, @"\b(timber|(hard)?wood) floor(ing|s)?\b");
        listing.Carpeted = ResolveAnswer(carpet, woodFloor);

        listing.Url = listingPage;
        return listing;
    }

    /// <summary>
    /// Resolves two booleans to an <see cref="Answer"/> as per the following table:
    /// <para>F F = Maybe</para>
    /// F T = No
    /// <para>T T = Yes</para>
    /// T F = Yes
    /// </summary>
    private static Answer ResolveAnswer(bool yes, bool no)
    {
        if (yes) return Answer.Yes;
        return no ? Answer.No : Answer.Maybe;
    }
}