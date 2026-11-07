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