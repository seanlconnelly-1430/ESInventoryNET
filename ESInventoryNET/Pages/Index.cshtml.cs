using Elasticsearch.Net;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ESInventoryNET.Pages;

public class IndexModel : PageModel
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<IndexModel> _logger;

    public List<ElasticsearchIndex> Indexes { get; set; } = new();
    public string? ErrorMessage { get; set; }

    public IndexModel(IConfiguration configuration, ILogger<IndexModel> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task OnGet()
    {
        try
        {
            var endpoint = _configuration["Elasticsearch:Endpoint"];
            var apiKey = _configuration["Elasticsearch:ApiKey"];

            if (string.IsNullOrEmpty(endpoint) || string.IsNullOrEmpty(apiKey))
            {
                ErrorMessage = "Elasticsearch configuration is missing. Please check appsettings.json";
                return;
            }

            var settings = new ConnectionConfiguration(new Uri(endpoint))
                .ApiKeyAuthentication(new ApiKeyAuthenticationCredentials(apiKey))
                .RequestTimeout(TimeSpan.FromSeconds(30));

            var client = new ElasticLowLevelClient(settings);

            // Get all indexes using the _cat/indices API with JSON format
            //"/_cat/indices?format=json&h=index,docs.count,store.size,health,status",
            var response = await client.DoRequestAsync<StringResponse>(
                Elasticsearch.Net.HttpMethod.GET, 
                "/_cat/indices",
                CancellationToken.None,
                PostData.Empty
            );

            if (response.Success)
            {
                var indices = response.Body;
                _logger.LogInformation("Successfully retrieved indexes from Elasticsearch");
                
                // Parse the JSON response
                var jsonResponse = System.Text.Json.JsonDocument.Parse(indices);
                
                foreach (var element in jsonResponse.RootElement.EnumerateArray())
                {
                    Indexes.Add(new ElasticsearchIndex
                    {
                        Name = element.GetProperty("index").GetString() ?? "",
                        DocumentCount = element.TryGetProperty("docs.count", out var docCount) ? docCount.GetString() ?? "0" : "0",
                        StoreSize = element.TryGetProperty("store.size", out var storeSize) ? storeSize.GetString() ?? "N/A" : "N/A",
                        Health = element.TryGetProperty("health", out var health) ? health.GetString() ?? "N/A" : "N/A",
                        Status = element.TryGetProperty("status", out var status) ? status.GetString() ?? "N/A" : "N/A"
                    });
                }
            }
            else
            {
                ErrorMessage = $"Failed to retrieve indexes: {response.DebugInformation}";
                _logger.LogError("Failed to retrieve indexes: {DebugInfo}", response.DebugInformation);
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error connecting to Elasticsearch: {ex.Message}";
            _logger.LogError(ex, "Error connecting to Elasticsearch");
        }
    }
}

public class ElasticsearchIndex
{
    public string Name { get; set; } = "";
    public string DocumentCount { get; set; } = "";
    public string StoreSize { get; set; } = "";
    public string Health { get; set; } = "";
    public string Status { get; set; } = "";
}
