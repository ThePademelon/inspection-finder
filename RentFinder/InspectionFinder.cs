using System.Globalization;
using System.IO.Compression;
using System.Text.RegularExpressions;
using HtmlAgilityPack;

namespace RentFinder;

public static class InspectionFinder
{
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
                Console.WriteLine($"Address:        {listing.Location}");
                Console.WriteLine($"Beds:           {listing.Beds}");
                Console.WriteLine($"Rent:           {listing.Price:$0.00}");
                Console.WriteLine($"Rent per Bed:   {listing.PricePerBed:$0.00}");
                Console.WriteLine(horizontalRule);
            }
        } while (@continue);
    }

    private static async Task<IEnumerable<Listing>> DeserializePageData(string url)
    {
        var stream = await new HttpClient
        {
            DefaultRequestHeaders =
            {
                {"User-Agent", "Mozilla/5.0 (X11; Linux x86_64; rv:91.0) Gecko/20100101 Firefox/91.0"},
                {"Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8"},
                {"Accept-Language", "en-US,en;q=0.5"},
                {"Accept-Encoding", "gzip, deflate, br"}
            },
        }.GetStreamAsync(url);
        var web = new HtmlDocument();
        web.Load(new GZipStream(stream, CompressionMode.Decompress));
        return web.DocumentNode.Descendants("div")
            .Where(x => x.HasMatchingDataId("listing-card-inspection"))
            .Select(ConvertToListing);
    }

    private static Listing ConvertToListing(HtmlNode listingNode)
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
        return new Listing {Beds = beds, Price = price, Location = locationText};
    }
}