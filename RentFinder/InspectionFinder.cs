using System.Globalization;
using System.IO.Compression;
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
                Console.WriteLine(horizontalRule);
            }
        } while (@continue);
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
        var bedsRegex = new Regex(@"(\d+) Beds?");
        var bedsText = listingNode.Descendants("span")
            .Single(x => x.HasMatchingDataId("property-features-text-container") && bedsRegex.IsMatch(x.InnerText)).InnerText;
        var bedsNumberString = bedsRegex.Match(bedsText).Groups[1].Value;
        var beds = int.Parse(bedsNumberString);

        var priceRegex = new Regex(@"\$\d+(\.\d+)?");
        var priceText = listingNode.Descendants("p")
            .Single(x => x.HasMatchingDataId("listing-card-price"))
            .InnerText;
        var price = decimal.Parse(priceRegex.Match(priceText).Value, NumberStyles.Currency);

        var locationText = listingNode.Descendants("h2")
            .Single(x => x.HasMatchingDataId("address-wrapper"))
            .InnerText;

        var listingPage = listingNode.Descendants("a").Select(x => x.GetAttributeValue("href", null)).Single();
        var pageHtml = await GetDocument(listingPage);
        var descriptionText = pageHtml.DocumentNode.Descendants("script")
            .Single(x =>
                x.Id.Equals("__NEXT_DATA__")).InnerText;

        var descriptionData = JsonNode.Parse(descriptionText)!["props"]!["pageProps"]!["description"]!.ToString();
        var hasAc = Regex.IsMatch(descriptionData, @"\b(A/?C|air.?con(ditioning)?|split.?system)\b", RegexOptions.IgnoreCase);
        
        return new Listing {Beds = beds, Price = price, Location = locationText, AirCon = hasAc};
    }
}