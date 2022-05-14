namespace RentFinder;

public partial class MailTemplate
{
    private readonly Listing _listing;

    public MailTemplate(Listing listing)
    {
        _listing = listing;
    }
}