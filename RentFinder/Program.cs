using CommandLine;
using RentFinder;

await Parser.Default.ParseArguments<Options>(args).WithParsedAsync(InspectionFinder.Search);