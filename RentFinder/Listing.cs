namespace RentFinder;

public class Listing
{
    public int Beds { get; set; }
    public string Location { get; set; }
    public decimal? Price { get; set; }
    public decimal? PricePerBed => Beds == 0 ? null : Price / Beds;
    public Answer AirCon { get; set; }
    public Answer RealShower { get; set; }
    public Answer Carpeted { get; set; }
    public string Url { get; set; }
    public string Slug { get; set; }
}