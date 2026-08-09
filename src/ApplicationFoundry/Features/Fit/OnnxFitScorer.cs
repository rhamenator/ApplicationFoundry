using ApplicationFoundry.Data;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using System.Text.RegularExpressions;

namespace ApplicationFoundry.Features.Fit;

public sealed record FitResult(float Score, string Explanation, float[] Features);

public interface IFitScorer
{
    FitResult Score(CandidateProfile profile, JobOpportunity job);
}

public sealed partial class OnnxFitScorer : IFitScorer, IDisposable
{
    private readonly InferenceSession session = new(TinyOnnxModel.Build());

    public FitResult Score(CandidateProfile profile, JobOpportunity job)
    {
        var descriptionTerms = Terms(job.Description);
        var evidenceTerms = Terms(string.Join(' ', profile.Evidence.Select(item => $"{item.Claim} {item.Details} {item.Keywords}")));
        var profileTerms = Terms($"{profile.Summary} {string.Join(' ', profile.Evidence.Select(item => item.Claim))}");
        var titleTerms = Terms(job.RoleTitle);
        var features = new[]
        {
            Coverage(descriptionTerms, evidenceTerms),
            Coverage(descriptionTerms, profileTerms),
            Coverage(titleTerms, profileTerms),
            Math.Clamp(profile.Evidence.Count / 8f, 0f, 1f)
        };
        var tensor = new DenseTensor<float>(features, [1, 4]);
        using var results = session.Run([NamedOnnxValue.CreateFromTensor("features", tensor)]);
        var score = results.Single(result => result.Name == "score").AsEnumerable<float>().Single();
        var strongest = features
            .Select((value, index) => (value, index))
            .OrderByDescending(item => item.value)
            .First();
        string[] labels = ["job-keyword evidence", "profile coverage", "role-title alignment", "evidence depth"];
        var missing = descriptionTerms.Except(evidenceTerms).Take(5).ToArray();
        var explanation = $"Strongest signal: {labels[strongest.index]} ({strongest.value:P0}). " +
            (missing.Length == 0 ? "No major unmatched terms were detected." : $"Review unmatched terms: {string.Join(", ", missing)}.");
        return new FitResult(score, explanation, features);
    }

    private static HashSet<string> Terms(string text) => WordPattern()
        .Matches(text.ToLowerInvariant())
        .Select(match => match.Value)
        .Where(value => value.Length > 2 && !StopWords.Contains(value))
        .ToHashSet(StringComparer.Ordinal);

    private static float Coverage(HashSet<string> requested, HashSet<string> available) =>
        requested.Count == 0 ? 0 : (float)requested.Count(available.Contains) / requested.Count;

    private static readonly HashSet<string> StopWords = new(["and", "the", "with", "for", "from", "that", "this", "you", "your", "our", "are"], StringComparer.Ordinal);

    [GeneratedRegex("[a-z0-9+#.]+", RegexOptions.CultureInvariant)]
    private static partial Regex WordPattern();

    public void Dispose() => session.Dispose();
}
