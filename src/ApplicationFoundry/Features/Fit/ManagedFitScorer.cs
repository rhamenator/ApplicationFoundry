using ApplicationFoundry.Data;

namespace ApplicationFoundry.Features.Fit;

public sealed class ManagedFitScorer : IFitScorer
{
    public FitResult Score(CandidateProfile profile, JobOpportunity job)
    {
        var (features, explanation) = OnnxFitScorer.Analyze(profile, job);
        // These are the Gemm weights and bias in TinyOnnxModel, followed by Sigmoid.
        var logit = -1.65f + 1.35f * features[0] + 1.1f * features[1]
            + 0.75f * features[2] + 0.8f * features[3];
        var score = 1f / (1f + MathF.Exp(-logit));
        return new FitResult(score, explanation, features);
    }
}
