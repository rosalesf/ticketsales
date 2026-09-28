namespace TicketSales.Services;

public interface IPersonalDataProtector
{
    string Protect(string value);
    string Unprotect(string value);
}
