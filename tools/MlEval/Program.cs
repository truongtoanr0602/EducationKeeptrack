// Offline evaluation of the personalisation model on simulated students.
//   dotnet run --project tools/MlEval -c Release -- [output.md] [seeds]
// Real data are far too few to train or test anything (a pilot class has one or two students), so the model is judged on
// students drawn from a generator whose hidden truth is known. Two generators are used so the result is not only
// "BKT recovers data that BKT produced": a well-specified one (two-state learning) and a misspecified one (gradual growth
// with forgetting, no discrete state). Every number is a mean over independent seeds; nothing here is real student data.
using System.Globalization;
using System.Text;
using EducationKeeptrack.Learning.Domain;

const int Tasks = 5, Length = 6, Skills = 8;
const double PassShare = 0.8;
var outputPath = args.Length > 0 ? args[0] : null;
var seeds = args.Length > 1 ? int.Parse(args[1], CultureInfo.InvariantCulture) : 30;
var inv = CultureInfo.InvariantCulture;
var rule = new MasteryRule(80, 5, 2, 1);

// ---------- generators ----------
// A student-skill history: attempts of 5 tasks, plus the realised success rate of the attempt after the last one
// (used only as ground truth for the prioritisation task).
IReadOnlyList<(Attempt[] Attempts, double[] Rates)> Generate(string kind, int count, Random random)
{
    var list = new List<(Attempt[], double[])>();
    for (var i = 0; i < count; i++)
    {
        var attempts = new Attempt[Length]; var rates = new double[Length];
        if (kind == "well-specified")
        {
            var pInit = 0.1 + 0.4 * random.NextDouble(); var learn = 0.1 + 0.3 * random.NextDouble();
            var guess = 0.1 + 0.2 * random.NextDouble(); var slip = 0.05 + 0.10 * random.NextDouble();
            var known = random.NextDouble() < pInit;
            for (var t = 0; t < Length; t++)
            {
                rates[t] = known ? 1 - slip : guess;
                attempts[t] = new(Tasks, Enumerable.Range(0, Tasks).Count(_ => random.NextDouble() < rates[t]));
                if (!known && random.NextDouble() < learn) known = true;
            }
        }
        else
        {
            // Gradual growth with noise and occasional forgetting; success rate is a smooth function of a latent level.
            var level = -0.8 + 1.2 * Gaussian(random); var gain = 0.25 + 0.15 * Gaussian(random);
            for (var t = 0; t < Length; t++)
            {
                rates[t] = 0.15 + 0.80 / (1 + Math.Exp(-1.8 * level));
                attempts[t] = new(Tasks, Enumerable.Range(0, Tasks).Count(_ => random.NextDouble() < rates[t]));
                level += gain + 0.2 * Gaussian(random);
                if (random.NextDouble() < 0.10) level -= 0.6;
            }
        }
        list.Add((attempts, rates));
    }
    return list;
}
static double Gaussian(Random r) => Math.Sqrt(-2 * Math.Log(1 - r.NextDouble())) * Math.Cos(2 * Math.PI * r.NextDouble());

// ---------- metrics ----------
static double Auc(IReadOnlyList<(double Score, bool Label)> data)
{
    var positives = data.Count(x => x.Label); var negatives = data.Count - positives;
    if (positives == 0 || negatives == 0) return double.NaN;
    var ordered = data.Select((x, i) => (x.Score, x.Label)).OrderBy(x => x.Score).ToList();
    double rankSum = 0; var i = 0;
    while (i < ordered.Count)
    {
        var j = i; while (j + 1 < ordered.Count && ordered[j + 1].Score == ordered[i].Score) j++;
        var rank = (i + j) / 2.0 + 1;
        for (var k = i; k <= j; k++) if (ordered[k].Label) rankSum += rank;
        i = j + 1;
    }
    return (rankSum - positives * (positives + 1) / 2.0) / ((double)positives * negatives);
}
static double LogLoss(IReadOnlyList<(double Score, bool Label)> d) =>
    d.Average(x => -(x.Label ? Math.Log(Math.Clamp(x.Score, 1e-6, 1 - 1e-6)) : Math.Log(1 - Math.Clamp(x.Score, 1e-6, 1 - 1e-6))));
static double Brier(IReadOnlyList<(double Score, bool Label)> d) => d.Average(x => Math.Pow(x.Score - (x.Label ? 1 : 0), 2));
static double Accuracy(IReadOnlyList<(double Score, bool Label)> d) => d.Average(x => (x.Score >= 0.5) == x.Label ? 1.0 : 0.0);

// ---------- predictors of "the next attempt reaches 80%" ----------
double Smooth(IEnumerable<Attempt> a) => (a.Sum(x => x.Correct) + 1.0) / (a.Sum(x => x.Tasks) + 2.0);
double PassOf(double rate) => KnowledgeTracing.PassProbability(Tasks, PassShare, rate);
string RuleState(IReadOnlyList<Attempt> history) => Competency.Evaluate(true, rule,
    history.Select((a, i) => new ConfirmedResult(Guid.NewGuid(), "", new DateOnly(2026, 1, 1).AddDays(7 * i), SkillResultStatus.Assessed, a.Tasks, a.Correct))).State;

IReadOnlyDictionary<string, Func<IReadOnlyList<Attempt>, double>> Predictors(IReadOnlyList<Attempt[]> train, BktParams fitted)
{
    // Constant and rule baselines need the training data to turn a verdict into a probability (fair calibration).
    var all = new List<(IReadOnlyList<Attempt> History, bool Pass)>();
    foreach (var seq in train) for (var t = 1; t < seq.Length; t++) all.Add((seq.Take(t).ToArray(), seq[t].Correct >= Math.Ceiling(PassShare * Tasks)));
    var prior = (all.Count(x => x.Pass) + 1.0) / (all.Count + 2.0);
    var byState = all.GroupBy(x => RuleState(x.History)).ToDictionary(g => g.Key, g => (g.Count(x => x.Pass) + 1.0) / (g.Count() + 2.0));
    return new Dictionary<string, Func<IReadOnlyList<Attempt>, double>>
    {
        ["Hằng số (tỷ lệ đạt chung)"] = _ => prior,
        ["Lần gần nhất"] = h => PassOf(Smooth(h.TakeLast(1))),
        ["Trung bình các lần"] = h => PassOf(Smooth(h)),
        ["Quy tắc mức đạt hiện tại (hiệu chỉnh)"] = h => byState.GetValueOrDefault(RuleState(h), prior),
        ["BKT, tham số mặc định"] = h => PassOf(KnowledgeTracing.ExpectedRate(KnowledgeTracing.Current(h, BktParams.Default), BktParams.Default)),
        ["BKT, tham số học từ dữ liệu huấn luyện"] = h => PassOf(KnowledgeTracing.ExpectedRate(KnowledgeTracing.Current(h, fitted), fitted)),
    };
}

IReadOnlyList<(double, bool)> Points(IReadOnlyList<Attempt[]> test, Func<IReadOnlyList<Attempt>, double> predict)
{
    var points = new List<(double, bool)>();
    foreach (var seq in test) for (var t = 1; t < seq.Length; t++) points.Add((predict(seq.Take(t).ToArray()), seq[t].Correct >= Math.Ceiling(PassShare * Tasks)));
    return points;
}

// ---------- prioritisation: which 2 of 8 skills to review ----------
// Truth: a skill is "weak" when the next attempt would reach 80% with probability under one half.
IReadOnlyDictionary<string, double> Prioritise(string kind, BktParams fitted, Random random, int students)
{
    var methods = new[] { "Chọn ngẫu nhiên", "Thấp nhất theo trung bình các lần", "Quy tắc + ngẫu nhiên", "Chỉ xác suất BKT thấp nhất", "Quy tắc + xếp hạng BKT (mặc định)", "Quy tắc + xếp hạng BKT (đã học tham số)" };
    var hits = methods.ToDictionary(x => x, _ => new List<double>());
    var today = new DateOnly(2026, 1, 1).AddDays(7 * 4);
    for (var s = 0; s < students; s++)
    {
        var skills = Generate(kind, Skills, random);
        const int seen = 3;
        var ids = Enumerable.Range(0, Skills).Select(i => Guid.Parse($"00000000-0000-0000-0000-{i:D12}")).ToArray();
        bool Weak(int i) => PassOf(skills[i].Rates[seen]) < 0.5;
        var history = skills.Select(x => x.Attempts.Take(seen).ToArray()).ToArray();
        var state = history.Select(RuleState).ToArray();
        SkillBelief Belief(int i, BktParams p) => new(ids[i], KnowledgeTracing.Current(history[i], p), seen, new DateOnly(2026, 1, 1).AddDays(7 * (seen - 1)), state[i]);
        double Precision(IEnumerable<int> chosen) { var c = chosen.ToList(); return c.Count == 0 ? double.NaN : c.Count(Weak) / (double)c.Count; }
        int[] Top(Func<int, double> key, IEnumerable<int> pool) => pool.OrderBy(key).ThenBy(i => i).Take(2).ToArray();
        var candidates = Enumerable.Range(0, Skills).Where(i => state[i] != CompetencyState.Mastered).ToArray();
        int[] Ranked(BktParams p) => SkillPriority.Rank(candidates.Select(i => Belief(i, p)), today, 2).Select(r => Array.IndexOf(ids, r.SkillId)).ToArray();

        hits[methods[0]].Add(Precision(Enumerable.Range(0, Skills).OrderBy(_ => random.Next()).Take(2)));
        hits[methods[1]].Add(Precision(Top(i => Smooth(history[i]), Enumerable.Range(0, Skills))));
        hits[methods[2]].Add(Precision(candidates.OrderBy(_ => random.Next()).Take(2)));
        hits[methods[3]].Add(Precision(Top(i => KnowledgeTracing.Current(history[i], BktParams.Default), Enumerable.Range(0, Skills))));
        hits[methods[4]].Add(Precision(Ranked(BktParams.Default)));
        hits[methods[5]].Add(Precision(Ranked(fitted)));
    }
    return methods.ToDictionary(m => m, m => hits[m].Where(x => !double.IsNaN(x)).DefaultIfEmpty(double.NaN).Average());
}

// ---------- run ----------
string Fmt(IEnumerable<double> v) { var l = v.Where(x => !double.IsNaN(x)).ToList(); return l.Count == 0 ? "—" : $"{l.Average().ToString("0.000", inv)} ± {Std(l).ToString("0.000", inv)}"; }
static double Std(IReadOnlyList<double> l) => l.Count < 2 ? 0 : Math.Sqrt(l.Sum(x => Math.Pow(x - l.Average(), 2)) / (l.Count - 1));

var report = new StringBuilder();
report.AppendLine("# Đánh giá ngoại tuyến mô hình cá nhân hoá").AppendLine();
report.AppendLine($"Sinh bởi `tools/MlEval`. Mỗi con số là trung bình ± độ lệch chuẩn trên **{seeds} hạt giống độc lập**. Dữ liệu hoàn toàn **mô phỏng**, không có học sinh thật. Mỗi lịch sử gồm {Length} lần đánh giá, mỗi lần {Tasks} nhiệm vụ; \"đạt\" nghĩa là đúng từ {(int)(PassShare * 100)}% ({Math.Ceiling(PassShare * Tasks)}/{Tasks}).").AppendLine();

foreach (var kind in new[] { "well-specified", "misspecified" })
{
    var title = kind == "well-specified" ? "Bộ sinh đúng giả định (hai trạng thái biết/chưa biết, đúng dạng BKT)" : "Bộ sinh sai giả định (năng lực liên tục, tăng dần, có quên; không có trạng thái rời rạc)";
    report.AppendLine($"## {title}").AppendLine();
    var perMethod = new Dictionary<string, List<(double Auc, double Ll, double Brier, double Acc)>>();
    var priority = new Dictionary<string, List<double>>();
    var fittedAll = new List<BktParams>();
    for (var seed = 1; seed <= seeds; seed++)
    {
        var random = new Random(seed * 7919 + (kind == "well-specified" ? 1 : 2));
        var train = Generate(kind, 150, random).Select(x => x.Attempts).ToArray();
        var test = Generate(kind, 300, random).Select(x => x.Attempts).ToArray();
        var fitted = KnowledgeTracing.Fit(train); fittedAll.Add(fitted);
        foreach (var (name, predict) in Predictors(train, fitted))
        {
            var pts = Points(test, predict);
            if (!perMethod.TryGetValue(name, out var list)) perMethod[name] = list = [];
            list.Add((Auc(pts), LogLoss(pts), Brier(pts), Accuracy(pts)));
        }
        foreach (var (name, value) in Prioritise(kind, fitted, random, 200))
        {
            if (!priority.TryGetValue(name, out var list)) priority[name] = list = [];
            list.Add(value);
        }
    }
    report.AppendLine("### Nhiệm vụ A: dự đoán lần đánh giá kế tiếp có đạt hay không").AppendLine();
    report.AppendLine("| Phương pháp | AUC ↑ | Log loss ↓ | Brier ↓ | Độ chính xác ↑ |").AppendLine("|---|---|---|---|---|");
    foreach (var (name, rows) in perMethod)
        report.AppendLine($"| {name} | {Fmt(rows.Select(x => x.Auc))} | {Fmt(rows.Select(x => x.Ll))} | {Fmt(rows.Select(x => x.Brier))} | {Fmt(rows.Select(x => x.Acc))} |");
    report.AppendLine().AppendLine($"Tham số BKT học được (trung bình {seeds} lần): PInit {fittedAll.Average(x => x.PInit).ToString("0.00", inv)}, PLearn {fittedAll.Average(x => x.PLearn).ToString("0.00", inv)}, PGuess {fittedAll.Average(x => x.PGuess).ToString("0.00", inv)}, PSlip {fittedAll.Average(x => x.PSlip).ToString("0.00", inv)}.").AppendLine();
    report.AppendLine("### Nhiệm vụ B: chọn 2 trong 8 kỹ năng cần ôn (độ chính xác chọn đúng kỹ năng yếu, precision@2 ↑)").AppendLine();
    report.AppendLine("| Phương pháp | Precision@2 |").AppendLine("|---|---|");
    foreach (var (name, values) in priority) report.AppendLine($"| {name} | {Fmt(values)} |");
    report.AppendLine();
}

// Data scarcity: how many histories are needed before fitting beats the default?
report.AppendLine("## Ảnh hưởng của lượng dữ liệu huấn luyện (bộ sinh đúng giả định)").AppendLine();
report.AppendLine("AUC của nhiệm vụ A trên cùng 300 lịch sử kiểm tra, khi học tham số từ N lịch sử huấn luyện.").AppendLine();
report.AppendLine("| N lịch sử huấn luyện | BKT mặc định | BKT đã học tham số |").AppendLine("|---|---|---|");
foreach (var n in new[] { 5, 10, 20, 50, 100, 200 })
{
    var def = new List<double>(); var fit = new List<double>();
    for (var seed = 1; seed <= seeds; seed++)
    {
        var random = new Random(seed * 104729 + n);
        var train = Generate("well-specified", n, random).Select(x => x.Attempts).ToArray();
        var test = Generate("well-specified", 300, random).Select(x => x.Attempts).ToArray();
        var fitted = KnowledgeTracing.Fit(train);
        def.Add(Auc(Points(test, h => PassOf(KnowledgeTracing.ExpectedRate(KnowledgeTracing.Current(h, BktParams.Default), BktParams.Default)))));
        fit.Add(Auc(Points(test, h => PassOf(KnowledgeTracing.ExpectedRate(KnowledgeTracing.Current(h, fitted), fitted)))));
    }
    report.AppendLine($"| {n} | {Fmt(def)} | {Fmt(fit)} |");
}
report.AppendLine();
report.AppendLine("## Hạn chế").AppendLine();
report.AppendLine("- Dữ liệu mô phỏng; kết quả cho biết mô hình hoạt động ra sao khi giả định của bộ sinh đúng hoặc sai ở mức đã nêu, không phải bằng chứng về hiệu quả trên học sinh thật.");
report.AppendLine("- Bộ sinh \"đúng giả định\" có lợi cho BKT vì dữ liệu được tạo bởi chính dạng mô hình đó; bộ \"sai giả định\" là phép thử công bằng hơn nhưng vẫn do người viết tự chọn.");
report.AppendLine("- Mức \"đạt\" và nhãn \"kỹ năng yếu\" lấy theo ngưỡng 80% của dự án; đổi ngưỡng sẽ đổi số liệu.");
report.AppendLine("- Chưa đo tác động của kế hoạch ôn lên kết quả học sinh (cần thử nghiệm có đối chứng với lớp thật).");
report.AppendLine("- Ở nhiệm vụ B, nhãn thật là trạng thái của lần đánh giá kế tiếp, có yếu tố ngẫu nhiên (chuyển trạng thái, đoán, nhầm) nên không phương pháp nào đạt 1,0; mức trần thực tế thấp hơn.");
report.AppendLine("- Chênh lệch giữa các phương pháp mạnh (AUC, precision@2) nhiều chỗ nhỏ hơn độ lệch chuẩn giữa các hạt giống; chỉ các khác biệt lớn hơn rõ rệt (ví dụ so với ngẫu nhiên, hằng số hay quy tắc) mới nên được xem là có ý nghĩa. Chưa chạy kiểm định ghép cặp.");
report.AppendLine("- Lợi thế đo được của BKT là xác suất được hiệu chỉnh tốt hơn (log loss, Brier), không phải thứ hạng tốt hơn (AUC). Học tham số từ dữ liệu không cải thiện AUC ở mọi cỡ mẫu thử, và khi dữ liệu sai giả định còn làm nhiệm vụ B kém đi.");

var text = report.ToString();
if (outputPath is null) Console.Write(text);
else { Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath))!); File.WriteAllText(outputPath, text, new UTF8Encoding(false)); Console.WriteLine($"Wrote {outputPath}"); }
