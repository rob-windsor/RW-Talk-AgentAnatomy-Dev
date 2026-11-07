using System.ComponentModel;

public static class TravelSpendingTools
{
    [Description(
        "Gets the total conference travel spending recorded " +
        "for an employee in 2027. Returns a fictional amount in USD.")]
    public static decimal GetAnnualTravelSpend(
        [Description(
            "The employee identifier supplied by the application.")]
        string employeeId)
    {
        Console.WriteLine();
        Console.WriteLine("[BUSINESS TOOL] GetAnnualTravelSpend called");
        Console.WriteLine($"[BUSINESS TOOL] Employee ID: {employeeId}");

        // Fixed fictional data; employeeId is not an authenticated identity.
        // This example has no authorization checks or database.
        const decimal amountSpent = 2800m;
        Console.WriteLine($"[BUSINESS TOOL] Result: ${amountSpent:N2} USD");
        return amountSpent;
    }
}