# Conference Flight Search — Azure Functions MCP Server

This project, **`AnatomyOfAnAgentMcp`**, is a small, locally hosted **Model Context Protocol (MCP) server** built with **C#**, **.NET 10**, and **Azure Functions (isolated worker)**. It provides one tool, `SearchFlights`, to the conference-travel agent built in the companion **`AnatomyOfAnAgent`** project.

The project files live directly in `Developer\Demos\AnatomyOfAnAgentMcp`, alongside this README and the demo guide. The project file is `AnatomyOfAnAgentMcp.csproj`; do not create an additional nested project folder.

The purpose is to show how an agent can discover and use a capability implemented in a **separate process** through MCP, without adding that operation to the agent application's source code. Unlike the main project's five progressively built stages, you build this server **once**, test it independently, and then use it in Demo 5.

The server returns **fixed, fictional flight quotes**. It does **not** search airlines, check live availability, book flights, access personal data, or deploy anything to Azure. Its local development endpoint is not a production security configuration.

## What it does

The `SearchFlights` tool accepts four required text parameters:

| Parameter | Example | Description |
| --- | --- | --- |
| `origin` | `YYZ` | Origin airport IATA code |
| `destination` | `ORD` | Destination airport IATA code |
| `departureDate` | `2027-10-12` | Outbound date, `YYYY-MM-DD` |
| `returnDate` | `2027-10-16` | Return date, `YYYY-MM-DD` |

For the supported route and dates, it returns three fictional itineraries, each with economy and business-class base round-trip fares in USD:

| Quote | Routing | Economy | Business |
| --- | --- | ---: | ---: |
| CF-101 — Contoso Air | Nonstop | $690 | $1,450 |
| CF-102 — Northstar Airways | One stop | $540 | $1,180 |
| CF-103 — Lakefront Airlines | Nonstop | $820 | $1,290 |

The tool returns an empty itinerary list with an explanatory status for other route/date combinations. Prices exclude taxes and fees, and are not real quotes.

When the traveler asks for the **lowest-priced nonstop economy** itinerary, the agent should select **CF-101 ($690)** rather than the cheaper connecting option **CF-102 ($540)**. The client combines the selected price with knowledge and other capabilities; the MCP server itself does **not** interpret corporate travel policy, calculate the travel budget, or prepare the worksheet.

## Where the server fits

```text
AnatomyOfAnAgent (C# .NET 10 console application)
  Microsoft Agent Framework + MCP client
                |
                | MCP over Streamable HTTP
                v
http://localhost:7071/runtime/webhooks/mcp
                |
                v
AnatomyOfAnAgentMcp (separate Azure Functions process)
  MCP extension -> SearchFlights -> fixed fictional quotes
```

The main application also uses local knowledge search, a direct C# business tool, and a worksheet skill. **Those components remain in the main project.** MCP is an optional way of exposing an external tool, not a replacement for the agent framework or a required component of every agent.

## Technology and prerequisites

- **Visual Studio Code** with **C# Dev Kit** and the **Azure Functions** extension.
- **.NET 10 SDK** and **Azure Functions Core Tools v4** (the MCP trigger requires a sufficiently recent v4 release; use **4.0.7030 or newer**).
- Azure Functions **isolated worker** and `Microsoft.Azure.Functions.Worker.Extensions.Mcp` **1.6.0** (the version recorded for the validated local setup).
- **Azurite**, used by the tested local configuration with `AzureWebJobsStorage=UseDevelopmentStorage=true`.

The server runs locally and does **not** need Azure CLI authentication or an Azure deployment. The companion agent requires its **own** Azure OpenAI access, described in its documentation.

## Build and run

Follow **[DEMO-GUIDE.md](DEMO-GUIDE.md)** to scaffold the project, configure the MCP extension, add `SearchFlights.cs`, and verify the endpoint. The tested local workflow is:

1. Start Azurite.
2. From the `AnatomyOfAnAgentMcp` project folder, run `dotnet run` and leave the Functions host running.
3. Use MCP Inspector or another MCP-capable client to discover and **invoke** `SearchFlights` at `http://localhost:7071/runtime/webhooks/mcp`.
4. Run **Demo 5** from the main `AnatomyOfAnAgent` project in a second terminal.
5. Inspect the **Functions host logs** to confirm that `SearchFlightsTool` executed; discovering a tool is not the same as invoking it.

## What is deliberately not included

This is a teaching sample rather than a deployable corporate travel service. It has no airline API, credentialed employee identity, permission-aware flight lookup, booking workflow, persistent database, or production authorization. The synthetic data is appropriate only for an isolated development demonstration. Do not publish this unauthenticated local prototype as a production endpoint.

## Related documentation

- [Build the MCP server step by step](DEMO-GUIDE.md)
- Companion agent: `AnatomyOfAnAgent` — its `README.md` and `DEMO-GUIDE.md` explain the five-stage agent and client integration.
- [Host an MCP server on Azure Functions](https://learn.microsoft.com/en-us/azure/azure-functions/functions-mcp-tutorial?pivots=programming-language-csharp&tabs=mcp-extension)
- [Azure Functions MCP bindings](https://learn.microsoft.com/en-us/azure/azure-functions/functions-bindings-mcp)
- [Model Context Protocol](https://modelcontextprotocol.io/)
