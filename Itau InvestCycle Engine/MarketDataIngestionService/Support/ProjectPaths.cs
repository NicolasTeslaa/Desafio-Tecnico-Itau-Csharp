namespace MarketDataIngestionService.Support;

internal static class ProjectPaths
{
    public static string GetCotacoesDirectory()
    {
        foreach (var root in GetCandidateRoots())
        {
            var resolved = ResolveFromRoot(root);
            if (resolved is null)
            {
                continue;
            }

            Directory.CreateDirectory(resolved);
            return resolved;
        }

        var fallback = Path.Combine(Directory.GetCurrentDirectory(), "cotacoes");
        Directory.CreateDirectory(fallback);
        return fallback;
    }

    private static IEnumerable<string> GetCandidateRoots()
    {
        yield return Directory.GetCurrentDirectory();
        yield return AppContext.BaseDirectory;
    }

    private static string? ResolveFromRoot(string root)
    {
        var current = new DirectoryInfo(root);

        while (current is not null)
        {
            var existingCotacoes = Path.Combine(current.FullName, "cotacoes");
            if (Directory.Exists(existingCotacoes))
            {
                return existingCotacoes;
            }

            var solutionPath = Path.Combine(current.FullName, "Itau.InvestCycleEngine.slnx");
            if (File.Exists(solutionPath))
            {
                return existingCotacoes;
            }

            current = current.Parent;
        }

        return null;
    }
}
