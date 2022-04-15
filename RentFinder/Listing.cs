namespace RentFinder;

public class Listing
{
    public int Beds { get; set; }
    public string Location { get; set; }
    public decimal Price { get; set; }
    public decimal PricePerBed => Price / Beds;
    public Answer AirCon { get; set; }
    public Answer RealShower { get; set; }
    public Answer Carpeted { get; set; }
    public string Url { get; set; }
}