namespace RentFinder;

internal class ListingFilter
{
    public decimal MaxPricePerBed { get; set; }
    public decimal MaxPriceOneBed { get; set; }
    public List<Answer> AcceptableAirCons { get; set; }
    public List<Answer> AcceptableRealShowers { get; set; }
    public List<Answer> AcceptableCarpets { get; set; }
}