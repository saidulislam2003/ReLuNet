using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using ReLuNet.Core.Entities;
using ReLuNet.Core.Interfaces;
using ReLuNet.Infrastructure.Data;

namespace ReLuNet.Infrastructure.Services;

public class AIService : IAIService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly AppDbContext _context;

    public AIService(IHttpClientFactory httpClientFactory, IConfiguration config, AppDbContext context)
    {
        _httpClient = httpClientFactory.CreateClient();
        _apiKey = config["Groq:ApiKey"]!;
        _context = context;
        _httpClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _apiKey);
        Console.WriteLine($"AIService initialized with Groq. Key prefix: {_apiKey?[..10]}");
    }

    public async Task EmbedArticleAsync(Article article)
    {
        try
        {
            // Store plain text for keyword-based search
            var plainText = System.Text.RegularExpressions.Regex.Replace(article.Content, "<.*?>", "");
            var textToEmbed = $"{article.Title} {article.Summary ?? ""} {plainText}".ToLower();

            var existing = await _context.ArticleEmbeddings
                .FirstOrDefaultAsync(e => e.ArticleId == article.Id);

            if (existing != null)
            {
                existing.Embedding = textToEmbed;
                existing.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                _context.ArticleEmbeddings.Add(new ArticleEmbedding
                {
                    ArticleId = article.Id,
                    Embedding = textToEmbed,
                    UpdatedAt = DateTime.UtcNow
                });
            }

            article.IsEmbedded = true;
            _context.Articles.Update(article);
            await _context.SaveChangesAsync();
            Console.WriteLine($"Article embedded: {article.Title}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Embedding error: {ex.Message}");
        }
    }

    public async Task<List<Article>> SemanticSearchAsync(string query)
    {
        try
        {
            var queryWords = query.ToLower()
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Where(w => w.Length > 2)
                .ToList();

            var embeddings = await _context.ArticleEmbeddings
                .Include(e => e.Article).ThenInclude(a => a.Author)
                .Include(e => e.Article).ThenInclude(a => a.ArticleTags).ThenInclude(at => at.Tag)
                .Where(e => e.Article.Status == ArticleStatus.Published)
                .ToListAsync();

            var scored = embeddings
                .Select(e => new
                {
                    Article = e.Article,
                    Score = queryWords.Count(w => e.Embedding.Contains(w))
                })
                .Where(x => x.Score > 0)
                .OrderByDescending(x => x.Score)
                .Take(5)
                .ToList();

            Console.WriteLine($"Semantic search found {scored.Count} articles for: {query}");
            return scored.Select(x => x.Article).ToList();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Semantic search error: {ex.Message}");
            return new List<Article>();
        }
    }

    public async Task<string> GenerateRAGResponseAsync(string userQuery)
    {
        try
        {
            Console.WriteLine($"RAG called: {userQuery}");
            var relevantArticles = await SemanticSearchAsync(userQuery);
            Console.WriteLine($"Found {relevantArticles.Count} relevant articles");

            var context = relevantArticles.Any()
                ? string.Join("\n\n", relevantArticles.Select(a =>
                    $"Title: {a.Title}\nAuthor: {a.Author.DisplayName}\nSummary: {a.Summary}\nURL: /Article/Read/{a.Slug}"))
                : "No specific articles found in the library yet.";

            var prompt = $"""
                You are ReLuNet AI Assistant, a helpful guide for a tech blogging platform focused on AI, ML, and web development.

                User question: {userQuery}

                Relevant articles from ReLuNet:
                {context}

                Provide a helpful, friendly response:
                - Give a clear answer or learning path
                - If articles are available, recommend them by title with their URLs
                - Use markdown formatting
                - Keep it concise and practical
                """;

            return await GenerateTextAsync(prompt);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"RAG error: {ex.Message}");
            return "Sorry, I encountered an error. Please try again.";
        }
    }

    public async Task<string> SummarizeArticleAsync(Article article)
    {
        try
        {
            var plainText = System.Text.RegularExpressions.Regex.Replace(article.Content, "<.*?>", "");
            var prompt = $"Summarize this article in 3-4 sentences:\n\nTitle: {article.Title}\n\n{plainText}";
            return await GenerateTextAsync(prompt);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Summary error: {ex.Message}");
            return "Could not generate summary.";
        }
    }

    private async Task<string> GenerateTextAsync(string prompt)
    {
        var url = "https://api.groq.com/openai/v1/chat/completions";

        var payload = new
        {
            model = "groq/compound",
            messages = new[]
            {
                new { role = "user", content = prompt }
            },
            max_tokens = 1024
        };

        Console.WriteLine("Calling Groq API...");
        var response = await _httpClient.PostAsJsonAsync(url, payload);
        var json = await response.Content.ReadAsStringAsync();
        Console.WriteLine($"Groq status: {response.StatusCode}");

        if (!response.IsSuccessStatusCode)
        {
            Console.WriteLine($"Groq error: {json[..Math.Min(300, json.Length)]}");
            return "Could not generate response.";
        }

        try
        {
            var doc = JsonDocument.Parse(json);
            return doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString() ?? "No response.";
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Parse error: {ex.Message}");
            return "Could not parse response.";
        }
    }
}
