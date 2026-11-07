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
