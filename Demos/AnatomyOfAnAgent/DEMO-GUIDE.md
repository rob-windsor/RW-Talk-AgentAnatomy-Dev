# Build the Conference Prep Agent — Step-by-Step Demo Guide

This attendee tutorial builds the **AnatomyOfAnAgent** C# console application progressively through five demos. It follows the same fictional Contoso conference-travel scenario at every stage. Each demo gives you an implementation, commands to run, and checks you can perform before moving on. You can follow it without attending the session or using the slide deck.

**Important:** Demo 5 consumes a separate Azure Functions MCP server. This guide covers the **client-side integration**; the server is a different project. All dollar amounts and employee records are fictional, and the application never submits, approves, or books travel.

## 0. Prepare your development environment

### Prerequisites

Install [Visual Studio Code](https://code.visualstudio.com/), the **C# Dev Kit** extension, the **.NET 10 SDK**, and [Azure CLI](https://learn.microsoft.com/en-us/cli/azure/install-azure-cli). Have an Azure OpenAI resource with a working chat-model deployment and permission to invoke it. The later stages were exercised with a `gpt-5-mini` deployment; use a deployment that supports the tool/function calling required by Demos 2–5. Model availability and behavior may differ by resource and region. These examples make billable model calls.

In the VS Code PowerShell terminal:

```powershell
dotnet --version
az --version
az login
az account show
```

### Create the project

Run these commands in a parent folder of your choice:

```powershell
mkdir AnatomyOfAnAgent
cd AnatomyOfAnAgent
dotnet new console --framework net10.0
code .
```

Add the packages at the versions used by the demonstrated implementation. You may add all packages now, or add the Lucene packages when you reach Demo 2 and the MCP client package when you reach Demo 5. These pinned versions are a reproducibility baseline; package APIs may change if you upgrade them.

```powershell
dotnet add package Microsoft.Agents.AI.OpenAI --version 1.27.0
dotnet add package Azure.AI.OpenAI --version 2.1.0
dotnet add package Azure.Identity --version 1.21.0

dotnet add package Lucene.Net --version 4.8.0-beta00018
dotnet add package Lucene.Net.Analysis.Common --version 4.8.0-beta00018
dotnet add package Lucene.Net.QueryParser --version 4.8.0-beta00018

dotnet add package ModelContextProtocol.Core --version 2.3.1
dotnet build
```

### Configure your own Azure OpenAI connection

The application reads the endpoint and deployment name from environment variables, so each stage can use your Azure OpenAI resource without editing the C# examples. This configuration differs from the original hard-coded demo environment but does not change the agent logic.

Store your connection values in `Properties/launchSettings.json`. Create the `Properties` folder at the project root if it does not already exist, then create the file with the following content:

```json
{
  "profiles": {
    "AnatomyOfAnAgent": {
      "commandName": "Project",
      "environmentVariables": {
        "AZURE_OPENAI_ENDPOINT": "https://YOUR-RESOURCE.openai.azure.com/",
        "AZURE_OPENAI_DEPLOYMENT": "YOUR-CHAT-DEPLOYMENT"
      }
    }
  }
}
```

Replace both placeholder values with your actual endpoint and deployed model name. `dotnet run` loads environment variables from this file automatically; no terminal commands are needed each session.

The session materials were prepared and tested using a **GPT-5 mini** deployment. Any Azure OpenAI chat model that supports tool/function calling should work, but output wording and tool-use behavior may differ from the documented examples.

`AzureCliCredential` uses your Azure CLI identity; it does **not** use an API key. Your signed-in identity needs permission to invoke the deployment (for example, an appropriate Azure OpenAI inference role on the resource). Being able to view the resource in the Azure portal does not necessarily grant inference permissions. Do not commit `launchSettings.json` with real endpoint or credential values. Add it to `.gitignore` if you initialise a Git repository for this project.

**Development workflow:** At each stage, replace `Program.cs` with the complete version shown in that stage. Retain all additional files introduced previously. Save each working version in Git or a named checkpoint if you want to compare stages. This is an *attendee build exercise*, not a requirement to edit source files during a live talk.

## Demo 1 — Instructions

**Goal:** Create a model-backed `ConferencePrepAgent`, ask a general hotel question, and then repeat a company-policy question using stronger instructions. Neither version has access to private company policy.

### 1.1 Stage A — Minimal instructions

Replace the generated `Program.cs` with:

```csharp
using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI;
using OpenAI.Chat;

var endpoint = new Uri(
    Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT")
    ?? throw new InvalidOperationException("Set AZURE_OPENAI_ENDPOINT."));
var deploymentName = Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT")
    ?? throw new InvalidOperationException("Set AZURE_OPENAI_DEPLOYMENT.");

var client = new AzureOpenAIClient(
    endpoint,
    new AzureCliCredential());

const string instructions = """
    You help employees prepare for conferences and business travel.
    Provide useful travel-planning advice.
    """;

AIAgent agent = client.GetChatClient(deploymentName).AsAIAgent(
    instructions: instructions,
    name: "ConferencePrepAgent");

var questions = new[]
{
    "I'm attending a conference in Chicago. What should I check before booking a hotel?",
    "What is our company's exact Chicago nightly hotel limit?"
};

foreach (var question in questions)
{
    Console.WriteLine($"\n[USER] {question}");
    var response = await agent.RunAsync(question);
    Console.WriteLine($"[AGENT]\n{response.Text}");
}
```

```powershell
dotnet run
```

**Check:** The agent can give general hotel guidance for the first question. For the second question, it cannot verify Contoso's precise Chicago nightly limit. Note how it handles the missing information — this is the baseline behavior without focused instructions.

### 1.2 Stage B — Improved instructions

Update `Program.cs` to replace the instructions and run only the policy question:

```csharp
using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI;
using OpenAI.Chat;

var endpoint = new Uri(
    Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT")
    ?? throw new InvalidOperationException("Set AZURE_OPENAI_ENDPOINT."));
var deploymentName = Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT")
    ?? throw new InvalidOperationException("Set AZURE_OPENAI_DEPLOYMENT.");

var client = new AzureOpenAIClient(
    endpoint,
    new AzureCliCredential());

const string instructions = """
    You are a conference travel assistant for Contoso employees.

    Help employees prepare for conferences and business travel.

    When answering:
    - Provide practical, concise guidance.
    - Distinguish verified company information from general advice.
    - Do not invent company policies, rates, or approval requirements.
    - If company-specific information is unavailable, say so clearly.
    - Do not claim to approve travel or make reservations.

    Organize ordinary answers using these headings:
    - What I can confirm
    - What I cannot verify
    - What you should do next
    """;

AIAgent agent = client.GetChatClient(deploymentName).AsAIAgent(
    instructions: instructions,
    name: "ConferencePrepAgent");

var prompt = "What is our company's exact Chicago nightly hotel limit?";
Console.WriteLine($"\n[USER] {prompt}");
var response = await agent.RunAsync(prompt);
Console.WriteLine($"[AGENT]\n{response.Text}");
```

```powershell
dotnet run
```

**Check:** The improved instructions should produce clear categories for what is and is not known. The agent still does not have access to the $275 limit — the key difference is *behavior*, not knowledge. The model may already acknowledge the missing information without the improved instructions; that is acceptable.

## Demo 2 — Knowledge and conversation state

**Goal:** Add company reference documents, build an in-memory Lucene.NET search of their Markdown sections, expose that search to Agent Framework through `TextSearchProvider`, and maintain a two-turn `AgentSession`.

### 2.1 Create the knowledge directory and files

Create `Knowledge/` at the project root. Add the three files below. The fictional annual allowance and airfare policy are present from the beginning; they are not introduced by editing the policy in Demo 3.

#### `Knowledge/Conference-Details-2027.md`

```markdown
# Contoso Dev Conference 2027

## Conference Information

Conference: Contoso Dev Conference 2027

Location: Chicago, Illinois, USA

Conference dates: October 12–15, 2027

Venue: Lakeshore Convention Center, Chicago

## Conference Hotels

The following hotels are recommended accommodations for attendees of Contoso Dev Conference 2027 in Chicago. Conference room rates are available to registered attendees, subject to room availability.

| Hotel | Location | Conference room rate |
| --- | --- | --- |
| West Loop Hotel | Chicago, Illinois | $225 USD per night |
| Lakeshore Hotel | Near the conference venue, Chicago, Illinois | $250 USD per night |
| Magnificent Mile Suites | Chicago, Illinois | $310 USD per night |

Hotel information:

- Rate type: Standard room, single or double occupancy
- Taxes and fees: Not included in the nightly room rates
- Availability: Subject to availability at the time of booking
- Booking: Hotel reservations must follow the company's corporate travel policy and approval requirements

These are recommended accommodation options, not confirmation that each hotel complies with the company's lodging policy. Employees should compare their selected hotel's nightly rate with the applicable corporate lodging limit before requesting approval or making reservations.

## Booking Considerations

Attendees should review the hotel's cancellation terms and confirm the total cost before making a reservation.

The conference hotel rate does not automatically mean the accommodation complies with an attendee's employer's travel policy.

Employees should consult their organization's travel policy and complete any required approvals before booking.

## Additional Information

Conference registration and hotel reservations are separate processes.

This document provides conference information for planning purposes only and does not confirm hotel availability or authorize travel.
```

#### `Knowledge/Corporate-Travel-Policy-2027.md`

```markdown
# Contoso Corporate Travel Policy 2027

## 1. Purpose

This policy establishes the requirements for employee business travel, including conference attendance, accommodation expenses, approvals, and reservations.

The policy applies to all Contoso employees traveling on company business.

## 2. Hotel Accommodation Limits

Employees must select accommodation that complies with the company's approved nightly lodging limits.

For business travel to Chicago, Illinois, the maximum permitted hotel room rate is $275 USD per night, excluding applicable taxes and mandatory hotel fees.

The nightly lodging limit applies to the base room rate, not the total cost of the stay.

A hotel rate below the nightly limit does not automatically authorize a reservation.

## 3. Airfare Limits

For business travel to conferences, the maximum permitted base round-trip airfare is $1,250 USD, excluding taxes and fees.

Employees may select economy or business class, provided the selected fare complies with the applicable airfare limit.

Airfare must also fit within the employee's remaining annual conference travel allowance.

A fare within the airfare limit does not automatically authorize a reservation. Manager approval is required before booking.

## 4. Annual Conference Travel Allowance

Each employee has an annual conference travel allowance of $7,500 USD for 2027.

The following expenses count toward this allowance:

- Conference registration
- Hotel accommodations
- Airfare
- Ground transportation

The employee's recorded conference travel spending must be retrieved from the company's travel expense system.

Remaining allowance is calculated by subtracting recorded spending from the annual allowance.

The annual allowance does not replace individual expense limits or the requirement for manager approval.

## 5. Travel Approval Requirements

Employees must obtain manager approval before making any travel reservations, including hotel bookings.

The required process is:

1. Prepare a business travel request that identifies the purpose, destination, dates, and estimated costs.
2. Submit the travel request through the company's approved travel-request process.
3. Obtain approval from the employee's manager.
4. Make reservations only after approval has been granted.

Employees must not interpret an AI-generated response as an official travel approval.

## 6. Booking Requirements

After receiving manager approval, employees must book accommodation through the company's approved corporate booking channel.

Employees should confirm room availability, cancellation conditions, taxes, mandatory fees, and the estimated total cost before completing a reservation.

## 7. Exceptions

If a proposed hotel rate or round-trip airfare exceeds the applicable corporate travel limit, employees must request and receive an approved exception before booking.

An exception request must explain the business reason for exceeding the applicable limit.

An exception is not automatically granted. Employees must obtain the required approval before making reservations.

Any approved exception remains subject to the employee's annual conference travel allowance.

## 8. Policy Verification

Employees are responsible for confirming that their proposed travel arrangements comply with the current corporate travel policy.

An informational response from an AI assistant does not constitute approval, submission of a travel request, or confirmation of a reservation.
```

#### `Knowledge/Conference-Travel-Checklist-2027.md`

```markdown
# Contoso Conference Travel Checklist 2027

## Purpose

This checklist helps Contoso employees prepare for business conference travel and complete the required approval and booking steps.

Employees should use this checklist alongside the current Corporate Travel Policy and the information provided by the conference organizer.

## 1. Confirm Conference Details

- Confirm the conference name, location, and dates.
- Identify the conference venue and recommended accommodation.
- Determine the required arrival and departure dates.
- Confirm the conference registration requirements.

## 2. Review Accommodation Options

- Check the recommended conference hotel's nightly room rate.
- Compare the base nightly rate against the applicable corporate lodging limit.
- Estimate the total accommodation cost based on the number of nights.
- Identify applicable taxes and mandatory hotel fees.
- Review room availability and cancellation conditions.

Important: A hotel rate that meets the company's spending limit does not automatically authorize a reservation.

## 3. Prepare the Travel Request

Before making reservations:

- Document the business purpose of the trip.
- Record the destination and planned travel dates.
- Identify the proposed hotel and estimated accommodation cost.
- Include other anticipated travel expenses, where applicable.
- Submit the travel request through Contoso's approved travel-request process.

## 4. Obtain Manager Approval

- Wait for the manager to review the submitted travel request.
- Confirm that manager approval has been granted.
- If the hotel exceeds the lodging limit, obtain an approved exception before booking.

Do not book travel before receiving the required approval.

## 5. Complete Reservations

After approval:

- Use Contoso's approved corporate booking channel.
- Confirm the hotel rate, dates, and cancellation terms.
- Complete the reservation.
- Retain the booking confirmation and relevant receipts.

## 6. Verify Completion

Before considering the arrangements complete:

- Confirm that the travel request was submitted successfully.
- Confirm that manager approval was recorded.
- Confirm that a hotel reservation exists in the approved booking system.
- Verify the reservation dates and accommodation details.

An AI assistant explaining these steps does not mean that the travel request was submitted, approval was granted, or a reservation was completed.

## Additional Guidance

If any required information is missing, employees should consult the appropriate conference documents, corporate travel policy, or travel administrator before proceeding.
```

### 2.2 Include the knowledge files in the build output

Add the following element *inside* the existing `<Project>` element of `AnatomyOfAnAgent.csproj`. Keep the other generated project settings intact.

```xml
<ItemGroup>
  <Content Include="Knowledge\**\*.md">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
  </Content>
</ItemGroup>
```

The search code below reads from `AppContext.BaseDirectory/Knowledge`, so the copy instruction matters. Verify that all three Markdown files appear under the output directory after a build.

### 2.3 Create `KnowledgeSearch.cs`

This helper splits Markdown by headings and indexes sections with Lucene.NET BM25, rather than returning whole documents. The index is constructed on first search, in memory. It returns at most eight relevant sections and displays console evidence of search execution.

```csharp
using System.Text.RegularExpressions;
using Lucene.Net.Analysis.Standard;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.QueryParsers.Classic;
using Lucene.Net.Search;
using Lucene.Net.Search.Similarities;
using Lucene.Net.Store;
using Lucene.Net.Util;
using Microsoft.Agents.AI;

public static class KnowledgeSearch
{
    private static readonly string KnowledgeFolder =
        Path.Combine(AppContext.BaseDirectory, "Knowledge");

    private const LuceneVersion Version = LuceneVersion.LUCENE_48;
    private const int MaxResults = 8;
    private static readonly Lazy<SearchIndex> Index = new(CreateIndex);

    public static IEnumerable<TextSearchProvider.TextSearchResult> Search(
        string query)
    {
        Console.WriteLine();
        Console.WriteLine($"[KNOWLEDGE SEARCH] Query: {query}");

        if (string.IsNullOrWhiteSpace(query))
        {
            Console.WriteLine("[KNOWLEDGE SEARCH] Empty query.");
            return [];
        }

        var index = Index.Value;
        if (index.Reader.NumDocs == 0)
        {
            Console.WriteLine("[KNOWLEDGE SEARCH] No indexed sections.");
            return [];
        }

        var parser = new MultiFieldQueryParser(
            Version,
            ["HeadingPath", "DocumentTitle", "Content"],
            index.Analyzer,
            new Dictionary<string, float>
            {
                ["HeadingPath"] = 3.0f,
                ["DocumentTitle"] = 2.0f,
                ["Content"] = 1.0f
            });

        // Agent-produced natural-language queries may contain Lucene syntax.
        var luceneQuery = parser.Parse(QueryParserBase.Escape(query));
        var hits = index.Searcher.Search(luceneQuery, MaxResults);
        var results = new List<TextSearchProvider.TextSearchResult>();

        foreach (var hit in hits.ScoreDocs)
        {
            var document = index.Searcher.Doc(hit.Doc);
            var fileName = document.Get("FileName") ?? "";
            var title = document.Get("DocumentTitle") ?? "";
            var heading = document.Get("HeadingPath") ?? "";
            var content = document.Get("Content") ?? "";

            string passage = $"""
                Source: {fileName}
                Document: {title}
                Section: {heading}

                {content}
                """;

            results.Add(new TextSearchProvider.TextSearchResult
            {
                SourceName = fileName,
                Text = passage
            });
        }

        Console.WriteLine(
            $"[KNOWLEDGE SEARCH] Found {results.Count} matching sections.");
        return results;
    }

    private static SearchIndex CreateIndex()
    {
        if (!System.IO.Directory.Exists(KnowledgeFolder))
        {
            throw new DirectoryNotFoundException(
                $"Knowledge folder not found: {KnowledgeFolder}");
        }

        var analyzer = new StandardAnalyzer(Version);
        var directory = new RAMDirectory();
        var config = new IndexWriterConfig(Version, analyzer)
        {
            OpenMode = OpenMode.CREATE,
            Similarity = new BM25Similarity()
        };

        using (var writer = new IndexWriter(directory, config))
        {
            foreach (var section in LoadSections())
            {
                var document = new Document
                {
                    new StringField("FileName", section.FileName, Field.Store.YES),
                    new TextField("DocumentTitle", section.DocumentTitle, Field.Store.YES),
                    new TextField("HeadingPath", section.HeadingPath, Field.Store.YES),
                    new TextField("Content", section.Content, Field.Store.YES)
                };
                writer.AddDocument(document);
            }
            writer.Commit();
        }

        var reader = DirectoryReader.Open(directory);
        var searcher = new IndexSearcher(reader)
        {
            Similarity = new BM25Similarity()
        };
        return new SearchIndex(directory, analyzer, reader, searcher);
    }

    private static IEnumerable<KnowledgeSection> LoadSections()
    {
        foreach (var file in System.IO.Directory
                     .GetFiles(KnowledgeFolder, "*.md")
                     .OrderBy(path => path))
        {
            string text = File.ReadAllText(file);
            string fileName = Path.GetFileName(file);
            string documentTitle = Path.GetFileNameWithoutExtension(file);
            var headings = Regex.Matches(
                text, @"(?m)^(#{1,6})[ \t]+(.+?)\s*$");

            if (headings.Count == 0)
            {
                if (!string.IsNullOrWhiteSpace(text))
                    yield return new KnowledgeSection(
                        fileName, documentTitle, documentTitle, text.Trim());
                continue;
            }

            var stack = new List<(int Level, string Title)>();
            string preamble = text[..headings[0].Index].Trim();
            if (!string.IsNullOrWhiteSpace(preamble))
                yield return new KnowledgeSection(
                    fileName, documentTitle, documentTitle, preamble);

            for (int i = 0; i < headings.Count; i++)
            {
                var match = headings[i];
                int level = match.Groups[1].Value.Length;
                string title = match.Groups[2].Value.Trim();

                while (stack.Count > 0 && stack[^1].Level >= level)
                    stack.RemoveAt(stack.Count - 1);
                stack.Add((level, title));
                if (level == 1) documentTitle = title;

                int bodyStart = match.Index + match.Length;
                int bodyEnd = i + 1 < headings.Count
                    ? headings[i + 1].Index : text.Length;
                string body = text[bodyStart..bodyEnd].Trim();
                if (string.IsNullOrWhiteSpace(body)) continue;

                var headingPath = string.Join(" > ", stack.Select(h => h.Title));
                yield return new KnowledgeSection(
                    fileName, documentTitle, headingPath, body);
            }
        }
    }

    private sealed record KnowledgeSection(
        string FileName,
        string DocumentTitle,
        string HeadingPath,
        string Content);

    private sealed record SearchIndex(
        RAMDirectory Directory,
        StandardAnalyzer Analyzer,
        DirectoryReader Reader,
        IndexSearcher Searcher);
}
```

### 2.4 Update `Program.cs` to include `TextSearchProvider`

Replace `Program.cs` with the following. The `SearchKnowledge` function is exposed through on-demand context retrieval and the second user message reuses the same `AgentSession`.

```csharp
using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI.Chat;

var endpoint = new Uri(
    Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT")
    ?? throw new InvalidOperationException("Set AZURE_OPENAI_ENDPOINT."));
var deploymentName = Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT")
    ?? throw new InvalidOperationException("Set AZURE_OPENAI_DEPLOYMENT.");

var azureOpenAIClient = new AzureOpenAIClient(
    endpoint,
    new AzureCliCredential());

var searchOptions = new TextSearchProviderOptions
{
    SearchTime =
        TextSearchProviderOptions.TextSearchBehavior
            .OnDemandFunctionCalling,
    FunctionToolName = "SearchKnowledge",
    FunctionToolDescription =
        "Search Contoso conference details, recommended hotels, " +
        "corporate travel policies, and travel checklists."
};

var searchProvider = new TextSearchProvider(
    (query, cancellationToken) =>
        Task.FromResult(KnowledgeSearch.Search(query)),
    searchOptions);

AIAgent agent = azureOpenAIClient
    .GetChatClient(deploymentName)
    .AsAIAgent(new ChatClientAgentOptions
    {
        Name = "ConferencePrepAgent",
        ChatOptions = new()
        {
            Instructions = """
                You are a conference travel assistant for Contoso employees.
                Help employees prepare for conferences and business travel.

                When answering:
                - Provide practical, concise guidance.
                - When company-specific policy or conference facts are needed,
                  use SearchKnowledge to verify them.
                  Do not assume those facts are unavailable without searching.
                - Distinguish verified company information from general advice.
                - Do not invent policies, rates, or approval requirements.
                - Do not claim to approve travel or make reservations.
                - Use these headings for ordinary answers:
                  What I can confirm; What I cannot verify;
                  What you should do next.
                """
        },
        AIContextProviders = [searchProvider]
    });

var session = await agent.CreateSessionAsync();

// Turn 1: model-initiated search across conference and policy documents.
var firstQuestion =
    "I'm attending the Contoso Dev Conference in Chicago. " +
    "Which recommended hotels are within our company's nightly hotel limit?";
Console.WriteLine($"[USER] {firstQuestion}");
var firstResponse = await agent.RunAsync(firstQuestion, session);
Console.WriteLine($"\n[AGENT RESPONSE — FIRST QUESTION]\n{firstResponse.Text}");

// Turn 2: the rate was mentioned earlier; this prompt does not repeat it.
var followUp =
    "Let's use the West Loop Hotel. I'll check in on October 12 " +
    "and check out on October 16, 2027. " +
    "What will the hotel stay cost?";
Console.WriteLine($"\n[USER] {followUp}");
var secondResponse = await agent.RunAsync(followUp, session);
Console.WriteLine($"\n[AGENT RESPONSE — FOLLOW-UP]\n{secondResponse.Text}");
```

```powershell
dotnet build
dotnet run
```

**Check:** The first turn should identify West Loop ($225) and Lakeshore ($250) as under the $275 base nightly rate limit; Magnificent Mile Suites ($310) exceeds it. The follow-up selects West Loop for October 12–16, 2027: **four nights × $225 = $900 USD**, before taxes and fees. Look for `[KNOWLEDGE SEARCH]` output and review the answer against the source. A second retrieval on the follow-up is optional; continuing the same `AgentSession` demonstrates conversation context, **not** persistent memory.

**If retrieval looks wrong:** Check that the three files were copied to `bin/.../Knowledge`; restart the app after changing a Markdown file because the search index is cached. Lucene can only rank the query the model supplied. Retrieved passages may omit relevant facts, and the model can still make mistakes.

## Demo 3 — A direct business tool

**Goal:** Combine company-wide policy facts from `SearchKnowledge` with employee-specific fictional data returned from a normal C# method. This is an example of a *read-only application function*, not an authenticated business system.

### 3.1 Add `TravelSpendingTools.cs`

```csharp
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
```

The employee ID `EMP001` is a fictional string, not an authenticated identity. This sample returns $2,800 for every input; real code must enforce authorization and retrieve a trustworthy record.

### 3.2 Replace `Program.cs`

```csharp
using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI.Chat;

var endpoint = new Uri(
    Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT")
    ?? throw new InvalidOperationException("Set AZURE_OPENAI_ENDPOINT."));
var deploymentName = Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT")
    ?? throw new InvalidOperationException("Set AZURE_OPENAI_DEPLOYMENT.");

var azureOpenAIClient = new AzureOpenAIClient(
    endpoint,
    new AzureCliCredential());

var searchOptions = new TextSearchProviderOptions
{
    SearchTime =
        TextSearchProviderOptions.TextSearchBehavior
            .OnDemandFunctionCalling,
    FunctionToolName = "SearchKnowledge",
    FunctionToolDescription =
        "Search Contoso conference details, recommended hotels, " +
        "corporate travel policies, and travel checklists."
};

var searchProvider = new TextSearchProvider(
    (query, cancellationToken) =>
        Task.FromResult(KnowledgeSearch.Search(query)),
    searchOptions);

var spendingTool = AIFunctionFactory.Create(
    TravelSpendingTools.GetAnnualTravelSpend);

AIAgent agent = azureOpenAIClient
    .GetChatClient(deploymentName)
    .AsAIAgent(new ChatClientAgentOptions
    {
        Name = "ConferencePrepAgent",
        ChatOptions = new()
        {
            Instructions = """
                You are a conference travel assistant for Contoso employees.
                Help employees prepare for conferences and business travel.

                The current employee's identifier is EMP001.
                This is fictional application context, not authenticated
                identity or authorization.

                When answering:
                - Provide practical, concise guidance.
                - When a question requires company-specific policy
                  information, use SearchKnowledge to retrieve it.
                  Do not assume the information is unavailable
                  without searching first.
                - When a question requires employee-specific spending
                  information, use GetAnnualTravelSpend to retrieve it.
                - When the answer requires both company policy and
                  employee spending, use both sources.
                - Distinguish verified information from general advice.
                - Do not invent policies, rates, spending amounts,
                  or approval requirements.
                - Do not claim to approve travel or make reservations.
                - Use these headings for ordinary answers:
                  What I can confirm; What I cannot verify;
                  What you should do next.
                """,
            Tools = [spendingTool]
        },
        AIContextProviders = [searchProvider]
    });

var prompt = "What is my remaining conference travel allowance for 2027?";
Console.WriteLine($"[USER] {prompt}");
var response = await agent.RunAsync(prompt);
Console.WriteLine($"\n[AGENT RESPONSE]\n{response.Text}");
```

```powershell
dotnet build
dotnet run
```

**Check:** The console should show a `GetAnnualTravelSpend` invocation and a knowledge search. The agent should combine the policy's **$7,500 annual allowance** with **$2,800 previous spending** to report **$4,700 remaining before the proposed trip**. Tool invocation order and search wording can vary. The application has not authorized or approved spending.

## Demo 4 — A reusable worksheet skill

**Goal:** Add a task-specific, file-based skill so the agent can produce a consistent **Conference Travel Approval Worksheet**. The worksheet procedure and output sections live in `SKILL.md`, not in the agent's permanent instructions.

### 4.1 Create the skill directory

Create `Skills/conference-travel-worksheet/SKILL.md`. The file name must be **uppercase `SKILL.md`**, and the very first characters must be YAML frontmatter `---`, with no blank line above it.

### 4.2 Update `AnatomyOfAnAgent.csproj`

Extend the existing item group to copy both folders into the output directory (do not duplicate the original `Knowledge` entry):

```xml
<ItemGroup>
  <Content Include="Knowledge\**\*.md">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
  </Content>
  <Content Include="Skills\**\*">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
  </Content>
</ItemGroup>
```

### 4.3 Add the full skill definition

`Skills/conference-travel-worksheet/SKILL.md`:

```markdown
---
name: conference-travel-worksheet
description: Prepare a structured Conference Travel Approval Worksheet for a Contoso employee planning conference travel. Use when the employee requests a travel approval worksheet or a structured travel cost and policy assessment.
---

# Conference Travel Approval Worksheet

## Purpose

Prepare a consistent worksheet that employees can use when requesting approval for conference travel.

The worksheet is advisory. It does not submit a travel request, grant approval, or make reservations.

## Procedure

When asked to prepare a Conference Travel Approval Worksheet:

1. Identify the conference, destination, and travel dates.
2. Identify the proposed hotel, check-in and checkout dates, and nightly rate.
3. Calculate the number of hotel nights and the base accommodation cost.
4. Retrieve the applicable corporate travel policy and identify relevant spending limits and approval requirements.
5. Retrieve the employee's recorded annual conference travel spending using the available business tool.
6. Calculate the employee's remaining annual conference travel allowance.
7. Include any available airfare, registration, and ground transportation estimates.
8. Compare known costs against the applicable individual limits and remaining annual allowance.
9. Identify missing information and any policy issues requiring attention.
10. Explain the steps needed before travel can be approved or booked.

Use information already established in the conversation when appropriate. Retrieve additional information when necessary.

Do not invent missing prices, expenses, approvals, or reservation details.

## Verification Before Producing the Worksheet

Before producing the final worksheet:

1. Check that each required worksheet field has been populated
   using information provided by the employee, retrieved knowledge,
   or business tool results.

2. If a required value is missing, retrieve it using the
   appropriate available knowledge source or business tool.

3. Use the actual values returned by business tools.
   Never substitute an assumed value for an available tool result.

4. Check that calculated values use the retrieved inputs.

5. If information remains unavailable after retrieval,
   identify it as missing rather than inventing a value.

## Required Output

Produce a document titled:

# Conference Travel Approval Worksheet

### 1. Conference Details

Include:
- Conference name
- Destination
- Conference dates
- Business purpose, if known

### 2. Accommodation

Include:
- Selected hotel
- Check-in and checkout dates
- Number of nights
- Base nightly rate
- Calculated base accommodation cost
- Applicable nightly hotel limit
- Whether the selected rate meets that limit

### 3. Other Travel Expenses

Include:
- Round-trip airfare
- Conference registration
- Ground transportation

Mark unavailable estimates as **Not yet determined**.

Do not treat an unquoted expense as zero.

When flight search results are available, include:
- Selected flight identifier
- Origin and destination
- Departure and return dates
- Travel class
- Number of stops
- Quoted base round-trip airfare
- Whether the selected fare complies with the corporate airfare limit

If the employee specifies flight selection criteria, apply
those criteria when selecting an option.

Use the actual information returned by the flight search tool.
Do not invent flight identifiers, prices, or availability.

### 4. Annual Conference Travel Allowance

Include:
- Annual allowance
- Recorded spending to date
- Remaining allowance before this trip
- Known estimated trip costs
- Provisional balance after known trip costs

If required costs are unknown, explicitly state that the final affordability assessment is incomplete.

### 5. Policy and Approval Review

Identify:
- Whether known hotel and airfare prices meet their respective limits
- Any known policy exceptions that would be required
- Outstanding information needed for the travel request
- Whether manager approval has been obtained

If approval has not been verified, state **Not confirmed**.

### 6. Next Steps

List the specific actions the employee must complete before booking.

Do not claim that a travel request has been submitted or approved.

## Calculation Rules

- All monetary amounts are in USD.
- Use the rates and limits from retrieved company information.
- Calculate hotel cost as number of nights multiplied by base nightly rate.
- Calculate remaining annual allowance as annual allowance minus recorded spending.
- Calculate the provisional balance by subtracting known proposed trip costs from the remaining allowance.
- Do not subtract expenses that have no known amount.
- Do not interpret a positive provisional balance as final budget approval.
- For this fictional demonstration, exclude taxes and fees from the budget calculations, but identify them as excluded.

## Important Restrictions

- Never invent a missing flight quote or registration cost.
- Never assume that a hotel appearing on the recommended list is within policy.
- Never imply that a completed worksheet constitutes manager approval.
- Never claim to have submitted a request or booked travel.
- If required information is unavailable, identify what is missing and how the employee can obtain it.
```

### 4.4 Update `Program.cs`

The next version creates `AgentSkillsProvider` and adds it alongside `TextSearchProvider`. It also retains the direct spending function. The two skill-related approval flags shown here are specific to loading and reading the **known local skill**; they do not authorize arbitrary scripts or other external operations.

```csharp
using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI.Chat;

var endpoint = new Uri(
    Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT")
    ?? throw new InvalidOperationException("Set AZURE_OPENAI_ENDPOINT."));
var deploymentName = Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT")
    ?? throw new InvalidOperationException("Set AZURE_OPENAI_DEPLOYMENT.");

var azureOpenAIClient = new AzureOpenAIClient(
    endpoint,
    new AzureCliCredential());

var searchOptions = new TextSearchProviderOptions
{
    SearchTime =
        TextSearchProviderOptions.TextSearchBehavior
            .OnDemandFunctionCalling,
    FunctionToolName = "SearchKnowledge",
    FunctionToolDescription =
        "Search Contoso conference details, recommended hotels, " +
        "corporate travel policies, and travel checklists."
};

var searchProvider = new TextSearchProvider(
    (query, cancellationToken) =>
        Task.FromResult(KnowledgeSearch.Search(query)),
    searchOptions);

var spendingTool = AIFunctionFactory.Create(
    TravelSpendingTools.GetAnnualTravelSpend);

var skillsPath = Path.Combine(AppContext.BaseDirectory, "Skills");
var skillsProvider = new AgentSkillsProvider(
    skillsPath,
    options: new AgentSkillsProviderOptions
    {
        DisableLoadSkillApproval = true,
        DisableReadSkillResourceApproval = true
    });

AIAgent agent = azureOpenAIClient
    .GetChatClient(deploymentName)
    .AsAIAgent(new ChatClientAgentOptions
    {
        Name = "ConferencePrepAgent",
        ChatOptions = new()
        {
            Instructions = """
                You are a conference travel assistant for Contoso employees.

                Help employees prepare for conferences and business travel.

                The current employee's identifier is EMP001.
                This is fictional application context, not
                authenticated identity or authorization.

                When answering:
                - Provide practical, concise guidance.
                - When a question requires company-specific policy
                  information, use SearchKnowledge to retrieve it.
                  Do not assume the information is unavailable
                  without searching first.
                - When a question requires employee-specific
                  spending information, use GetAnnualTravelSpend
                  to retrieve it.
                - When answering a question requires both company
                  policy and employee spending, use both sources.
                - Use an available skill when it matches the
                  user's task.
                - Distinguish verified company information from
                  general advice.
                - Do not invent company policies, rates,
                  spending amounts, or approval requirements.
                - Do not claim to approve travel or make reservations.

                For ordinary answers, use these headings:
                - What I can confirm
                - What I cannot verify
                - What you should do next

                When using a skill, follow its required
                output structure instead.

                When referencing knowledge documents, use their names.
                Do not generate hyperlinks unless an actual URL is
                provided in retrieved knowledge.
                """,
            Tools = [spendingTool]
        },
        AIContextProviders =
        [
            searchProvider,
            skillsProvider
        ]
    });

var response = await agent.RunAsync(
    """
    I'm attending the Contoso Dev Conference in Chicago,
    October 12–15, 2027.

    I'll be staying at the West Loop Hotel, checking in
    October 12 and checking out October 16.

    Please prepare a Conference Travel Approval Worksheet
    for this trip.
    """);

LogSkillActivity(response);

Console.WriteLine();
Console.WriteLine("[AGENT RESPONSE]");
Console.WriteLine(response.Text);

static void LogSkillActivity(AgentResponse response)
{
    var skillCallIds = new HashSet<string>();

    foreach (var message in response.Messages)
    {
        foreach (var content in message.Contents)
        {
            if (content is FunctionCallContent call && call.Name == "load_skill")
            {
                skillCallIds.Add(call.CallId);
                Console.WriteLine($"[SKILL] load_skill requested (CallId: {call.CallId})");
            }
            else if (content is FunctionResultContent result
                     && skillCallIds.Contains(result.CallId))
            {
                Console.WriteLine($"[SKILL] load_skill result received (CallId: {result.CallId})");
                if (result.Exception is not null)
                {
                    Console.WriteLine(
                        $"[SKILL] load_skill failed (CallId: {result.CallId}): {result.Exception.Message}");
                }
                else
                {
                    var resultText = result.Result?.ToString() ?? string.Empty;
                    Console.WriteLine(
                        $"[SKILL] Result: {resultText[..Math.Min(100, resultText.Length)]}");
                }
            }
        }
    }
}
```

```powershell
dotnet build
dotnet run
```

**Check:** The output should contain all six skill-defined sections. Expected known numbers are West Loop lodging **$900**, remaining allowance before trip **$4,700**, and provisional remaining **$3,800** after the known hotel charge. The policy's base airfare cap is **$1,250**, but the actual fare, conference registration, ground transportation, and taxes/fees are still unknown. Unknown costs are **not zero**. Manager approval remains unconfirmed, and nothing has been submitted or booked.

A formatted worksheet *alone* does not prove that the skill was loaded. `LogSkillActivity` inspects `response.Messages` for `FunctionCallContent` and `FunctionResultContent` items. It matches results to their `load_skill` call by `CallId`, so other tool calls such as `SearchKnowledge` or `GetAnnualTravelSpend` do not produce false positives. With the skill correctly configured you should see:

```text
[SKILL] load_skill requested (CallId: call_...)
[SKILL] load_skill result received (CallId: call_...)
[SKILL] Result: ---
name: conference-travel-worksheet
...
```

If neither `[SKILL]` line appears, either the model chose not to invoke the skill or the skill was not discovered — confirm that `Skills/conference-travel-worksheet/SKILL.md` starts at byte one with `---` and is copied to the output folder. If the agent returns no final text, a pending skill-load approval may have interrupted the run; this can occur even when knowledge retrieval works.

## Demo 5 — MCP flight search

**Goal:** Connect the existing agent to a separately hosted MCP server and use its fictional flight quotes in a revised worksheet. **MCP discovery and actual invocation are distinct events.**

### 5.1 Start the separate MCP server

Complete the separate **`AnatomyOfAnAgentMcp`** server project and its setup guide first. The server is a **.NET 10 isolated-worker Azure Functions application**, not another class in this console project. For the tested local configuration, start **Azurite** (`AzureWebJobsStorage=UseDevelopmentStorage=true`) before starting the Functions host. It exposes one MCP tool named `SearchFlights` at:

```text
http://localhost:7071/runtime/webhooks/mcp
```

From the server project directory, run `dotnet run` and keep that terminal open. Wait until the Functions host reports it has started before running the console client. This was the tested launch method; the host may display warnings if started differently. The endpoint uses **Streamable HTTP**, not a normal `/api/` HTTP trigger or the older `/sse` transport. The main app is the MCP **client**; building the server is covered in the separate server guide.

### 5.2 Confirm the client package

If you installed all packages in Section 0, this dependency is already present. Otherwise, add it now:

```powershell
dotnet add package ModelContextProtocol.Core --version 2.3.1
```

### 5.3 Replace `Program.cs` with the combined client

The code connects to the MCP server, lists its tools, and registers them alongside the existing direct spending tool using `Tools = [spendingTool, .. mcpTools]`. Keep the client alive for the entire agent invocation.

```csharp
using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Client;
using OpenAI.Chat;

var endpoint = new Uri(
    Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT")
    ?? throw new InvalidOperationException("Set AZURE_OPENAI_ENDPOINT."));
var deploymentName = Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT")
    ?? throw new InvalidOperationException("Set AZURE_OPENAI_DEPLOYMENT.");

var azureOpenAIClient = new AzureOpenAIClient(
    endpoint,
    new AzureCliCredential());

// Discover independently hosted MCP tools before configuring the agent.
var mcpTransport = new HttpClientTransport(
    new HttpClientTransportOptions
    {
        Endpoint = new Uri(
            "http://localhost:7071/runtime/webhooks/mcp"),
        TransportMode = HttpTransportMode.StreamableHttp
    });

await using var mcpClient =
    await McpClient.CreateAsync(mcpTransport);

var mcpTools = await mcpClient.ListToolsAsync();
Console.WriteLine("[MCP] Connected to Azure Functions MCP server");
foreach (var tool in mcpTools)
{
    Console.WriteLine($"[MCP] Discovered tool: {tool.Name}");
}

// On-demand knowledge retrieval: indexed Markdown sections, not whole files.
var searchOptions = new TextSearchProviderOptions
{
    SearchTime =
        TextSearchProviderOptions.TextSearchBehavior
            .OnDemandFunctionCalling,
    FunctionToolName = "SearchKnowledge",
    FunctionToolDescription =
        "Search Contoso conference details, recommended hotels, " +
        "corporate travel policies, and travel checklists."
};

var searchProvider = new TextSearchProvider(
    (query, cancellationToken) =>
        Task.FromResult(KnowledgeSearch.Search(query)),
    searchOptions);

var spendingTool = AIFunctionFactory.Create(
    TravelSpendingTools.GetAnnualTravelSpend);

var skillsPath = Path.Combine(AppContext.BaseDirectory, "Skills");
var skillsProvider = new AgentSkillsProvider(
    skillsPath,
    options: new AgentSkillsProviderOptions
    {
        DisableLoadSkillApproval = true,
        DisableReadSkillResourceApproval = true
    });

AIAgent agent = azureOpenAIClient
    .GetChatClient(deploymentName)
    .AsAIAgent(new ChatClientAgentOptions
    {
        Name = "ConferencePrepAgent",
        ChatOptions = new()
        {
            Instructions = """
                You are a conference travel assistant for Contoso employees.

                Help employees prepare for conferences and business travel.

                The current employee's identifier is EMP001.
                This is fictional application context, not
                authenticated identity or authorization.

                When answering:
                - Provide practical, concise guidance.
                - When a question requires company-specific policy
                  information, use SearchKnowledge to retrieve it.
                  Do not assume the information is unavailable
                  without searching first.
                - When a question requires employee-specific
                  spending information, use GetAnnualTravelSpend
                  to retrieve it.
                - When answering a question requires both company
                  policy and employee spending, use both sources.
                - Use an available skill when it matches the
                  user's task.
                - Distinguish verified company information from
                  general advice.
                - Do not invent company policies, rates,
                  spending amounts, or approval requirements.
                - Do not claim to approve travel or make reservations.

                For ordinary answers, use these headings:
                - What I can confirm
                - What I cannot verify
                - What you should do next

                When using a skill, follow its required
                output structure instead.

                When referencing knowledge documents, use their names.
                Do not generate hyperlinks unless an actual URL is
                provided in retrieved knowledge.
                """,
            Tools = [spendingTool, .. mcpTools]
        },
        AIContextProviders =
        [
            searchProvider,
            skillsProvider
        ]
    });

var response = await agent.RunAsync(
    """
    I'm attending the Contoso Dev Conference in Chicago,
    October 12–15, 2027.

    I'll be staying at the West Loop Hotel, checking in
    October 12 and checking out October 16.

    Search for round-trip flights from Toronto Pearson (YYZ)
    to Chicago O'Hare (ORD), departing October 12, 2027,
    and returning October 16, 2027.

    Use the lowest-priced nonstop economy flight option
    when estimating my travel costs.

    Please prepare a Conference Travel Approval Worksheet
    for this trip.
    """);

Console.WriteLine();
Console.WriteLine("[AGENT RESPONSE]");
Console.WriteLine(response.Text);
```

```powershell
dotnet build
dotnet run
```

**Check the console:** You should see `[MCP] Connected to Azure Functions MCP server` and `[MCP] Discovered tool: SearchFlights`. Then check the **Functions host terminal** for messages such as `Executing 'Functions.SearchFlightsTool'`, `SearchFlights called: YYZ to ORD, 2027-10-12 to 2027-10-16`, and `Executed 'Functions.SearchFlightsTool' (Succeeded, ...)`. If the tool is discovered but not invoked, the connection succeeded but the model did not use the tool. Tool discovery alone is *not* proof the remote function ran.

The selected flight should be **CF-101, nonstop economy, $690 USD**, even though **CF-102 one-stop economy is cheaper at $540 USD**; the request explicitly asks for the lowest-priced *nonstop* economy fare. The chosen flight meets the $1,250 base airfare cap. This example uses static mock quotes rather than live availability, and the flight selections cannot be booked.

### 5.4 Verify the completed worksheet

| Field | Expected value |
| --- | ---: |
| West Loop Hotel: four nights × $225 | $900 USD |
| CF-101 nonstop economy base round-trip airfare | $690 USD |
| **Known proposed trip costs** | **$1,590 USD** |
| Annual allowance | $7,500 USD |
| Prior spending returned by C# tool | $2,800 USD |
| Remaining before this trip | $4,700 USD |
| **Provisional remaining after known trip costs** | **$3,110 USD** |

The final result still has six sections; flight details include the quote ID, route YYZ–ORD, October 12–16 dates, economy class, nonstop routing, quoted base airfare, and policy comparison. Registration, ground transportation, taxes, and fees are not filled in by this fixture. The result is a **worksheet**, not a booking or approval.

### 5.5 Optional: test flight search separately from worksheet formatting

To isolate MCP tool use, temporarily replace the final `agent.RunAsync` prompt with:

```csharp
var response = await agent.RunAsync(
    """
    I'm attending the Contoso Dev Conference in Chicago.

    Find round-trip flights from Toronto Pearson (YYZ)
    to Chicago O'Hare (ORD), departing October 12, 2027,
    and returning October 16, 2027.

    Show me the available flight options, including
    their prices, and indicate which comply with
    Contoso's airfare limit.
    """);
```

This is an alternative test of the same MCP-capable project, not an additional stage. The fixture offers three itineraries, each with economy and business fares: CF-101 ($690/$1,450), CF-102 ($540/$1,180), and CF-103 ($820/$1,290), respectively. Under the policy's **$1,250 base airfare cap**, all three economy fares comply and only CF-102's business fare complies. Business class is not automatically disallowed; the price matters.

## Troubleshooting and validation

| Symptom | Check |
| --- | --- |
| Azure authentication or authorization fails | Run `az account show`, sign in with `az login`, verify inference permissions and the endpoint/deployment environment variables. |
| The agent cannot find knowledge files | Verify the `.csproj` copy entries and `bin/.../Knowledge` directory; rebuild and restart after modifying Markdown. |
| Search is called but relevant facts are missing | Inspect the model-generated search query; Lucene returns ranked **sections**, not an entire source document. |
| Skill isn't discovered | Confirm `Skills/conference-travel-worksheet/SKILL.md` starts at byte one with `---`, has valid frontmatter, and is copied into the output folder. |
| Worksheet has missing or contradictory spending numbers | Confirm `GetAnnualTravelSpend` executed; validate arithmetic against returned values rather than trusting model prose. |
| MCP connection fails | Start the independent Functions host and any required storage emulator, check port `7071` and the `/runtime/webhooks/mcp` URL. |
| Tool is discovered but never executed | Check the Functions execution logs and the prompt. Discovery does not imply invocation. |
| Output claims booking, approval, or live prices | Re-check the sample constraints. No real travel action exists in this project. |

The code and dependency versions are based on a previously exercised demonstration, with environment-specific Azure settings made configurable. **This attendee-oriented variant has not been freshly compiled or executed here**; confirm `dotnet build` at each stage, and allow for changes in package APIs or service configuration. Model outputs are nondeterministic: validate the underlying retrieval and tool results and the calculations on your own machine.

## Reference documentation

- [Microsoft Agent Framework](https://learn.microsoft.com/en-us/agent-framework)
- [Build your first C# agent](https://learn.microsoft.com/en-us/agent-framework/get-started/your-first-agent)
- [Agent Skills in Microsoft Agent Framework](https://learn.microsoft.com/en-us/agent-framework/agents/skills)
- [MCP specification and resources](https://modelcontextprotocol.io/)

