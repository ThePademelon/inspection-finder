public class Listing
{
    public int Beds { get; set; }
    public string Location { get; set; }
    public decimal Price { get; set; }
    public decimal PricePerBed => Price / Beds;
    public bool AirCon { get; set; }
}