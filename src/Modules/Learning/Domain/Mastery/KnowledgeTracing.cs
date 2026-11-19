namespace EducationKeeptrack.Learning.Domain;

/// <summary>
/// Parameters of a two-state Bayesian Knowledge Tracing model for one skill: the student either knows it or does not.
/// PInit = P(known before the first attempt), PLearn = P(unknown -> known between attempts),
/// PGuess = P(a task is right | unknown), PSlip = P(a task is wrong | known). No forgetting.
/// </summary>
public sealed record BktParams(double PInit, double PLearn, double PGuess, double PSlip)
{
    /// <summary>Starting values for a class with too little data to fit its own.</summary>
    public static readonly BktParams Default = new(0.30, 0.20, 0.20, 0.10);

    /// <summary>Guess and slip below 0.5 together keep "known" meaning "better than chance" (identifiability).</summary>
    public bool IsValid => PInit is > 0 and < 1 && PLearn is > 0 and < 1 && PGuess is > 0 and < 1 && PSlip is > 0 and < 1 && PGuess + PSlip < 1;
}

/// <summary>One confirmed assessment of one skill: how many tasks, how many right.</summary>
public readonly record struct Attempt(int Tasks, int Correct);

/// <summary>
/// Bayesian Knowledge Tracing over confirmed skill results. Each attempt of n tasks with k right is a Binomial
/// observation whose success rate is (1 - PSlip) if the skill is known and PGuess if it is not, so the posterior
/// of "known" is updated by Bayes' rule and then moved by the learning transition. Parameters can be fitted by
/// maximum likelihood with the forward recursion. The model only ranks and explains; the mastery rule decides.
/// </summary>
public static class KnowledgeTracing
{
    private const double Eps = 1e-9;

    /// <summary>P(k right of n | success rate r), the Binomial pmf.</summary>
    public static double Likelihood(Attempt a, double rate)
    {
        var r = Math.Clamp(rate, Eps, 1 - Eps);
        return Math.Exp(LogChoose(a.Tasks, a.Correct) + a.Correct * Math.Log(r) + (a.Tasks - a.Correct) * Math.Log(1 - r));
    }

    /// <summary>Posterior P(known) right after seeing the attempt, given the prior P(known) just before it.</summary>
    public static double Update(double prior, Attempt a, BktParams p)
    {
        var known = prior * Likelihood(a, 1 - p.PSlip);
        var unknown = (1 - prior) * Likelihood(a, p.PGuess);
        var total = known + unknown;
        return total <= 0 ? prior : known / total;
    }

    /// <summary>The learning transition applied between two attempts.</summary>
    public static double Advance(double posterior, BktParams p) => posterior + (1 - posterior) * p.PLearn;

    /// <summary>P(known) after each attempt, in order (before the transition to the next one).</summary>
    public static IReadOnlyList<double> Trace(IEnumerable<Attempt> attempts, BktParams p)
    {
        var list = new List<double>();
        var belief = p.PInit;
        foreach (var attempt in attempts)
        {
            var posterior = Update(belief, attempt, p);
            list.Add(posterior);
            belief = Advance(posterior, p);
        }
        return list;
    }

    /// <summary>P(known) now: the belief that would apply to the next attempt. Without attempts it is the prior.</summary>
    public static double Current(IEnumerable<Attempt> attempts, BktParams p)
    {
        var belief = p.PInit;
        foreach (var attempt in attempts) belief = Advance(Update(belief, attempt, p), p);
        return belief;
    }

    /// <summary>Expected share of tasks right in the next attempt, given P(known) now.</summary>
    public static double ExpectedRate(double pKnown, BktParams p) => pKnown * (1 - p.PSlip) + (1 - pKnown) * p.PGuess;

    /// <summary>P(an attempt of n tasks reaches at least the given share right), for a success rate r.</summary>
    public static double PassProbability(int tasks, double threshold, double rate)
    {
        var need = (int)Math.Ceiling(threshold * tasks - 1e-9);
        double sum = 0;
        for (var k = Math.Max(need, 0); k <= tasks; k++) sum += Likelihood(new(tasks, k), rate);
        return Math.Clamp(sum, 0, 1);
    }

    /// <summary>Log-likelihood of the sequences under the parameters (forward recursion over the hidden known/unknown state).</summary>
    public static double LogLikelihood(IEnumerable<IReadOnlyList<Attempt>> sequences, BktParams p)
    {
        double total = 0;
        foreach (var sequence in sequences)
        {
            var belief = p.PInit;
            foreach (var attempt in sequence)
            {
                var known = belief * Likelihood(attempt, 1 - p.PSlip);
                var unknown = (1 - belief) * Likelihood(attempt, p.PGuess);
                var marginal = known + unknown;
                total += Math.Log(Math.Max(marginal, 1e-300));
                belief = Advance(marginal <= 0 ? belief : known / marginal, p);
            }
        }
        return total;
    }

    /// <summary>
    /// Maximum-likelihood fit by coordinate ascent on a grid. Guess and slip are capped at 0.4 so the "known" state
    /// stays meaningful. Needs many sequences to be stable; callers should keep the default when data are few.
    /// </summary>
    public static BktParams Fit(IReadOnlyList<IReadOnlyList<Attempt>> sequences, BktParams? start = null, int rounds = 5)
    {
        var best = start ?? BktParams.Default;
        if (sequences.Count == 0) return best;
        var bestLl = LogLikelihood(sequences, best);
        double[] Grid(double lo, double hi) => Enumerable.Range(0, 30).Select(i => lo + (hi - lo) * i / 29).ToArray();
        var init = Grid(0.02, 0.98); var learn = Grid(0.01, 0.70); var guess = Grid(0.01, 0.40); var slip = Grid(0.01, 0.40);
        for (var round = 0; round < rounds; round++)
        {
            foreach (var value in init) Try(best with { PInit = value });
            foreach (var value in learn) Try(best with { PLearn = value });
            foreach (var value in guess) Try(best with { PGuess = value });
            foreach (var value in slip) Try(best with { PSlip = value });
        }
        return best;

        void Try(BktParams candidate)
        {
            if (!candidate.IsValid) return;
            var ll = LogLikelihood(sequences, candidate);
            if (ll > bestLl) { bestLl = ll; best = candidate; }
        }
    }

    private static double LogChoose(int n, int k) => LogFactorial(n) - LogFactorial(k) - LogFactorial(n - k);
    private static double LogFactorial(int n)
    {
        double sum = 0;
        for (var i = 2; i <= n; i++) sum += Math.Log(i);
        return sum;
    }
}
