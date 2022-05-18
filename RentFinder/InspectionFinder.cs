using System.Diagnostics;
using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using HtmlAgilityPack;

namespace RentFinder;

public class InspectionFinder
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

    private Dictionary<string, SupplementalData>? _supplementalData;

    public static async Task SearchWithOptions(Options options)
    {
        var finder = new InspectionFinder
        {
            _supplementalData = await DeserializeSupplementalData(options.SupplemetalDataPath)
        };
        await finder.Search(options);
    }

    private static async Task<Dictionary<string, SupplementalData>?> DeserializeSupplementalData(string supplementalDataPath)
    {
        if (string.IsNullOrEmpty(supplementalDataPath)) return null;
        var dataText = await File.ReadAllTextAsync(supplementalDataPath);
        return JsonSerializer.Deserialize<Dictionary<string, SupplementalData>>(dataText, new JsonSerializerOptions { Converters = { new JsonStringEnumConverter() }});
    }

    private async Task Search(Options options)
    {
        var page = 1;
        bool @continue;
        const string horizontalRule = "================================================================";
        Console.WriteLine(horizontalRule);
        var filter = await GetFilter(options.FilterFilePath);
        do
        {
            var uriBuilder = new UriBuilderPlus($"https://www.domain.com.au/rent/{options.Location}/");
            if (options.Day != null)
            {
                uriBuilder.AddPath("inspection-times");
                uriBuilder.AddQuery($"inspectiondate={options.Day:yyyy-MM-dd}");
            }

            uriBuilder.AddQuery($"page={page++}");
            var data = DeserializePageData(uriBuilder.ToString());

            @continue = false;
            await foreach (var listing in data)
            {
                @continue = true;
                if (filter != null && !MatchesFilter(filter, listing)) continue;
                Console.WriteLine($"Address:            {listing.Location}");
                Console.WriteLine($"Beds:               {listing.Beds}");
                Console.WriteLine($"Rent:               {listing.Price:$0.00}");
                Console.WriteLine($"Rent per Bed:       {listing.PricePerBed:$0.00}");
                Console.WriteLine($"Air Conditioning:   {ConvertToEmoji(listing.AirCon)}");
                Console.WriteLine($"Real Shower:        {ConvertToEmoji(listing.RealShower)}");
                Console.WriteLine($"Carpeted:           {ConvertToEmoji(listing.Carpeted)}");
                Console.WriteLine($"Secure Entrance:    {ConvertToEmoji(listing.SecureEntrance)}");
                foreach (var email in listing.AgentEmails) Console.WriteLine($"Agent Email:        mailto:{email}");
                Console.WriteLine($"URL:                {listing.Url}");
                Console.WriteLine(horizontalRule);
            }
        } while (@continue);
    }

    private static async Task<ListingFilter?> GetFilter(string filterPath)
    {
        if (string.IsNullOrEmpty(filterPath)) return null;
        var filterText = await File.ReadAllTextAsync(filterPath);
        return JsonSerializer.Deserialize<ListingFilter>(filterText, new JsonSerializerOptions { Converters = { new JsonStringEnumConverter() }});
    }

    private static bool MatchesFilter(ListingFilter filter, Listing listing)
    {
        if (listing.Ignored) return false;
        var match = listing.PricePerBed <= filter.MaxPricePerBed;
        match |= listing.Beds == 1 && listing.Price <= filter.MaxPriceOneBed;
        match &= filter.AcceptableAirCons.Contains(listing.AirCon);
        match &= filter.AcceptableRealShowers.Contains(listing.RealShower);
        match &= filter.AcceptableCarpets.Contains(listing.Carpeted);
        match &= filter.AcceptableSecureEntrances.Contains(listing.SecureEntrance);
        return match;
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

    private async IAsyncEnumerable<Listing> DeserializePageData(string url)
    {
        var web = await GetDocument(url);
        var listingNodes = web.DocumentNode.Descendants("div")
            .Where(x => x.HasMatchingDataId("listing-card-inspection") 
                        || x.HasMatchingDataId("listing-card-wrapper-premiumplus")
                        || x.HasMatchingDataId("listing-card-wrapper-standardpp")
                        || x.HasMatchingDataId("listing-card-wrapper-elite")
                        || x.HasMatchingDataId("listing-card-wrapper-elitepp"));
        foreach (var listingNode in listingNodes)
        {
            var listing = await ConvertToListing(listingNode);
            ApplySupplementalData(ref listing);
            yield return listing;
        }
    }

    private void ApplySupplementalData(ref Listing listing)
    {
        if (_supplementalData is null || !_supplementalData.TryGetValue(listing.Slug, out var supplementalData)) return;
        listing.Beds = supplementalData.Beds ?? listing.Beds;
        listing.Carpeted = supplementalData.Carpeted ?? listing.Carpeted;
        listing.Price = supplementalData.Price ?? listing.Price;
        listing.AirCon = supplementalData.AirCon ?? listing.AirCon;
        listing.RealShower = supplementalData.RealShower ?? listing.RealShower;
        listing.Ignored = supplementalData.Ignored ?? listing.Ignored;
        listing.SecureEntrance = supplementalData.SecureEntrance ?? listing.SecureEntrance;
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

        var listingPage = listingNode.Descendants("a").Select(x => x.GetAttributeValue("href", null)).First();
        var pageHtml = await GetDocument(listingPage);
        var listingJsonRaw = pageHtml.DocumentNode.Descendants("script")
            .Single(x => x.Id.Equals("__NEXT_DATA__")).InnerText;

        var pageProps = JsonNode.Parse(listingJsonRaw)!["props"]!["pageProps"]!;

        listing.Beds = (int) pageProps["beds"]!;
        listing.Location = (string) pageProps["address"]!;
        listing.Slug = new Uri(((string) pageProps["listingUrl"])!).Segments.Last();

        var priceText = (string) pageProps["listingSummary"]!["price"]!;
        priceText = priceText.Replace(",", string.Empty);
        var match = Regex.Match(priceText, @"\$\d+(\.\d+)?");
        if (match.Success) listing.Price = decimal.Parse(match.Value, NumberStyles.Currency);
        else Debug.WriteLine($"Failed to parse price '{priceText}'");
        
        var searchText = GetSearchText(pageProps);
        listing.AirCon = Regex.IsMatch(searchText, @"\b(A/?C|air.?con(ditioning)?|split.?system|cooling)\b", RegexOptions.IgnoreCase) ? Answer.Yes : Answer.Maybe;

        var showerOverBath = Regex.IsMatch(searchText, @"\b(shower.over.bath(tub)?s?)\b", RegexOptions.IgnoreCase);
        var walkInShower = Regex.IsMatch(searchText, @"\b(walk.in showers?)\b", RegexOptions.IgnoreCase);
        listing.RealShower = ResolveAnswer(walkInShower, showerOverBath);

        var carpet = Regex.IsMatch(searchText, @"\bcarpet(ed|ing)?\b", RegexOptions.IgnoreCase);
        var woodFloor = Regex.IsMatch(searchText, @"\b(((timber|(hard)?wood(en)?) floor(ing|s|boards)?)|floorboards)\b", RegexOptions.IgnoreCase);
        listing.Carpeted = ResolveAnswer(carpet, woodFloor);

        var secureEntrance = Regex.IsMatch(searchText, @"\b(secur(e|ity) ?(building)? entr(ance|y)|intercom)\b", RegexOptions.IgnoreCase);
        listing.SecureEntrance = ResolveAnswer(secureEntrance, false);

        var agentsArray = pageProps["agents"]!.AsArray();
        listing.AgentEmails = GetEmails(agentsArray);

        listing.Url = listingPage;
        return listing;
    }

    private static IEnumerable<string> GetEmails(JsonArray agents)
    {
        foreach (var agent in agents)
        {
            var email64 = (string?) agent?["email"];
            if (email64 is not null) yield return Encoding.UTF8.GetString(Convert.FromBase64String(email64));
        }
    }

    private static string GetSearchText(JsonNode pageProps)
    {
        var searchTextBuilder = new StringBuilder();

        var pageProp = pageProps["description"];
        if (!string.IsNullOrEmpty(pageProp?.ToString()))
        {
            searchTextBuilder.AppendLine(string.Join(' ', pageProp.AsArray().Select(x => (string?) x)));
        }

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

        return searchTextBuilder.ToString();
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