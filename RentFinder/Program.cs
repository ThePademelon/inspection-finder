using System.Globalization;
using System.IO.Compression;
using HtmlAgilityPack;
using RentFinder;

// TODO: Pass in these via CLI
const string locationId = "collingwood-vic-3066";
var day = DateTime.Now.ToString("yyyy-MM-dd");

// TODO: Handle paging
var data = await DeserializePageData($"https://www.domain.com.au/rent/{locationId}/inspection-times/?inspectiondate={day}");

const string horizontalRule = "==========================================================";
Console.WriteLine(horizontalRule);
foreach (var listing in data)
{
    Console.WriteLine($"Address:        {listing.Location}");
    Console.WriteLine($"Beds:           {listing.Beds}");
    Console.WriteLine($"Rent:           {listing.Price:$0.00}");
    Console.WriteLine($"Rent per Bed:   {listing.PricePerBed:$0.00}");
    Console.WriteLine(horizontalRule);
}

async Task<IEnumerable<Listing>> DeserializePageData(string url)
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

Listing ConvertToListing(HtmlNode listingNode)
{
    // TODO: Regex
    var bedsNode = listingNode.Descendants("span")
        .Single(x => x.HasMatchingDataId("property-features-text-container") &&
                     (x.InnerText.EndsWith("Beds") || x.InnerText.EndsWith("Bed")));
    var bedsText = bedsNode.InnerText.Replace(" Beds", null).Replace(" Bed", null);
    var beds = int.Parse(bedsText);

    var priceText = listingNode.Descendants("p").Single(x => x.HasMatchingDataId("listing-card-price")).InnerText;
    var price = decimal.Parse(priceText, NumberStyles.Currency);

    var locationText = listingNode.Descendants("h2").Single(x => x.HasMatchingDataId("address-wrapper")).InnerText;
    return new Listing {Beds = beds, Price = price, Location = locationText};
}