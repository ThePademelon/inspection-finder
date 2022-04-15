namespace RentFinder;

public class UriBuilderPlus : UriBuilder
{
    public UriBuilderPlus(string s) : base(s)
    {
    }

    public void AddPath(string path) => Path += path;

    public void AddQuery(string query)
    {
        if (Query.StartsWith("?")) Query += "&";
        else Query += "?";
        Query += query;
    }
}