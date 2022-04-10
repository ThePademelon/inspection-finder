using HtmlAgilityPack;

namespace RentFinder;

public static class HtmlNodeExtensions
{
    public static bool HasMatchingDataId(this HtmlNode node, string value)
    {
        return value.Equals(node.GetAttributeValue("data-testid", null));
    }
}