using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;
using Microsoft.Extensions.Logging;

namespace AnatomyOfAnAgentMcp;

public sealed class SearchFlights(ILogger<SearchFlights> logger)
{
   [Function(nameof(SearchFlightsTool))]
    public string SearchFlightsTool(
        [McpToolTrigger(
            "SearchFlights",
            "Search fictional round-trip conference flights. " +
            "Returns three itineraries with economy and business-class " +
            "base fares in USD. Does not book flights.")]
        ToolInvocationContext context,
        [McpToolProperty("origin", "Origin airport IATA code (for example YYZ).", true)]
        string origin,
        [McpToolProperty("destination", "Destination airport IATA code (for example ORD).", true)]
        string destination,
        [McpToolProperty("departureDate", "Outbound date, YYYY-MM-DD.", true)]
        string departureDate,
        [McpToolProperty("returnDate", "Return date, YYYY-MM-DD.", true)]
        string returnDate)
    {
        logger.LogInformation(
            "SearchFlights called: {Origin} to {Destination}, {Departure} to {Return}",
            origin, destination, departureDate, returnDate);

        // Synthetic fixture only: no airline integration or live inventory.
        if (!string.Equals(origin, "YYZ", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(destination, "ORD", StringComparison.OrdinalIgnoreCase) ||
            !TryParseDate(departureDate, out var dep) || dep != new DateOnly(2027, 10, 12) ||
            !TryParseDate(returnDate, out var ret) || ret != new DateOnly(2027, 10, 16))
        {
            return JsonSerializer.Serialize(new
            {
                source = "Synthetic Contoso demo data",
                status = "No fixture configured for the requested route and dates",
                itineraries = Array.Empty<object>()
            });
        }

        var result = new
        {
            source = "Synthetic Contoso demo data - not real prices or availability",
            currency = "USD",
            origin = "YYZ",
            destination = "ORD",
            departureDate = "2027-10-12",
            returnDate = "2027-10-16",
            fareBasis = "Base round-trip airfare only; taxes and fees excluded",
            itineraries = new[]
            {
                new
                {
                    quoteId = "CF-101",
                    carrier = "Contoso Air",
                    routing = "Nonstop",
                    economyBaseFare = 690,
                    businessBaseFare = 1450
                },
                new
                {
                    quoteId = "CF-102",
                    carrier = "Northstar Airways",
                    routing = "One stop",
                    economyBaseFare = 540,
                    businessBaseFare = 1180
                },
                new
                {
                    quoteId = "CF-103",
                    carrier = "Lakefront Airlines",
                    routing = "Nonstop",
                    economyBaseFare = 820,
                    businessBaseFare = 1290
                }
            }
        };

        logger.LogInformation("SearchFlights returning 3 fictional itineraries with 6 fares.");
        return JsonSerializer.Serialize(result, new JsonSerializerOptions
        {
            WriteIndented = true
        });
    }

    private static bool TryParseDate(string input, out DateOnly date)
    {
        if (DateOnly.TryParseExact(input, "yyyy-MM-dd", out date)) return true;
        if (DateTimeOffset.TryParse(input, out var dto)) { date = DateOnly.FromDateTime(dto.Date); return true; }
        date = default;
        return false;
    }
}
