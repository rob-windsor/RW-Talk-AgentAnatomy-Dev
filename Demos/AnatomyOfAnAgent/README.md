# The Anatomy of an AI Agent — .NET Developer Project

A practical C# project that builds a conference-travel AI agent progressively using **Microsoft Agent Framework**. The example is designed for developers who want to see what changes when an agent gains instructions, access to company knowledge, a business tool, a reusable skill, and finally a separately hosted MCP tool.

The fictional **Contoso Conference Prep Agent** helps an employee plan a trip to Contoso Dev Conference 2027 in Chicago. It can compare recommended hotels against a travel policy, work with conversation history, retrieve fictional annual spending, prepare a structured travel approval worksheet, and use a separate MCP flight-search service to improve that worksheet.

All company information, employees, travel expenses, hotel rates, and flight quotes in this tutorial are illustrative. **The application does not book travel, submit a request, authenticate employees, or grant approval.**

## What you will build

| Stage | Capability added | What it demonstrates |
| --- | --- | --- |
| Demo 1 — Instructions | A model-backed agent with an explicit role and response guidance | Better instructions influence behavior but do not provide private policy information. |
| Demo 2 — Knowledge | Three local Markdown documents, Lucene.NET section search, and `TextSearchProvider` | The agent retrieves relevant company facts and retains context within a session. |
| Demo 3 — Tools | `GetAnnualTravelSpend`, exposed with `AIFunctionFactory.Create` | Company policy and employee-specific business data are different sources. |
| Demo 4 — Skills | A `conference-travel-worksheet` skill loaded by `AgentSkillsProvider` | A task-specific procedure determines the worksheet structure without becoming global instructions. |
| Demo 5 — MCP | An MCP client connecting over Streamable HTTP to an independent Azure Functions server | The agent discovers and invokes a remotely exposed `SearchFlights` tool. |

The project remains a **.NET 10 console application** throughout. Each stage builds on the prior stage; the guide provides a complete `Program.cs` for each checkpoint so you can reproduce the results or compare changes without reconstructing earlier versions.

## Architecture

```text
User request
    |
    v
AnatomyOfAnAgent — .NET console application
    |-- Microsoft Agent Framework + Azure OpenAI model
    |-- Instructions and conversation session
    |-- Knowledge/ Markdown files
    |      +-- Lucene.NET section index -> TextSearchProvider
    |-- TravelSpendingTools.GetAnnualTravelSpend (direct C# function)
    |-- Skills/conference-travel-worksheet/SKILL.md
    +-- MCP client (Demo 5 only)
              |
              | Streamable HTTP
              v
        Separate Azure Functions MCP server
              +-- SearchFlights (fictional quotes)
```

The MCP server, **`AnatomyOfAnAgentMcp`**, is a different project and process running locally for this exercise. This repository's client-side integration appears in Demo 5, but the server must be built and running separately.

## The sample scenario

- Conference: **Contoso Dev Conference 2027**, Chicago, October 12–15, 2027.
- Selected hotel: **West Loop Hotel**, October 12–16, four nights at **$225 USD/night**, or **$900 USD** before taxes and fees.
- Chicago lodging base-rate limit: **$275 USD/night**.
- Annual conference travel allowance: **$7,500 USD**; fictional previous spending **$2,800 USD**; allowance remaining before this trip **$4,700 USD**.
- Airfare base round-trip cap: **$1,250 USD**.
- The flight-search fixture includes **CF-101 nonstop economy $690 USD** and a cheaper **CF-102 one-stop economy $540 USD**. Selecting the lowest-priced *nonstop* economy flight yields CF-101, not CF-102.
- With $900 lodging and $690 airfare, the **known proposed trip costs** are $1,590 USD and the **provisional remaining allowance** is $3,110 USD. Other expenses are still unknown.

## Prerequisites

You will need VS Code with C# Dev Kit, the .NET 10 SDK, Azure CLI, and access to an **Azure OpenAI resource and a deployed chat model that supports tool/function calling**. You must be authorized to invoke that deployment; model calls may incur Azure charges. Demo 5 additionally needs the separately hosted local Azure Functions MCP server and its prerequisites (including the tested Azurite configuration).

Package versions used in the documented project are pinned in the [demo guide](DEMO-GUIDE.md). This sample uses Lucene.NET **4.8.0-beta00018**, which is a prerelease version.

## Getting started

1. Follow [DEMO-GUIDE.md](DEMO-GUIDE.md) to create the project, add packages, and configure your Azure OpenAI deployment.
2. Complete Demos 1–4 in order, adding the knowledge documents, function tool, and skill at their respective stages.
3. Build and start the independent **`AnatomyOfAnAgentMcp`** MCP server, then complete Demo 5 to connect the agent to `SearchFlights`.

The guide includes the actual source files, test prompts, commands, expected results, and troubleshooting notes. It is intended to be followed directly, without access to a conference presentation.

## Important limitations

**Not a production travel system.** `EMP001` is a fictional identifier, not an authenticated principal; `GetAnnualTravelSpend` returns a hard-coded amount and performs no authorization. The Lucene index is local and in memory. The remote flight quotes are fixed synthetic fixtures, not live inventory or prices. The worksheet does not submit or approve anything, and a positive provisional balance is not proof of policy compliance for unknown costs.

Model-driven tool selection and response wording can vary between runs. Verify retrieved sources and actual tool invocations rather than treating plausible model text as evidence that a search or external action occurred.

## Further reading

- [Microsoft Agent Framework](https://learn.microsoft.com/en-us/agent-framework)
- [Microsoft Agent Framework — first agent with C#](https://learn.microsoft.com/en-us/agent-framework/get-started/your-first-agent)
- [Agent Skills](https://learn.microsoft.com/en-us/agent-framework/agents/skills)
- [Model Context Protocol](https://modelcontextprotocol.io/)
