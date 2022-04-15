using CommandLine;

namespace RentFinder;

public class Options
{
    [Option('l', "location", HelpText = "The location slug used by Domain to identify locations. Usually in the form suburb-state-postcode.", Required = true)]
    public string Location { get; set; }

    [Option('d', "day", Required = true, HelpText = "The day to search for inspections in the format yyyy-MM-dd")]
    public DateTime Day { get; set; }

    [Option('f', "filter", HelpText = "The path to your filter file.")]
    public string FilterFilePath { get; set; }
}