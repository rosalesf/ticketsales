using TicketSales.Models;

namespace TicketSales.Services;

public interface IRecurrenteCustomerService
{
    Task SynchronizeAsync(Order order, CancellationToken cancellationToken);
}
