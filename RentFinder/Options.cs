using CommandLine;

namespace RentFinder;

public class Options
{
    [Option(Required = true)]
    public string Location { get; set; }

    [Option(Required = true)]
    public DateTime Day { get; set; }
}