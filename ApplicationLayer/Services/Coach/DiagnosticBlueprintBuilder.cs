using ApplicationLayer.DTOs.Coach;
using DomainLayer.Constants;
using DomainLayer.Entities;

namespace ApplicationLayer.Services.Coach;

/// <summary>
/// SCRUM-452: dựng blueprint đề diagnostic/drill từ Competency Framework.
/// Hoàn toàn data-driven: mọi quyết định skill/độ khó/topic đều đọc từ framework,
/// không có nhánh riêng cho .NET/Java/Python hay bất kỳ stack nào.
/// </summary>
public static class DiagnosticBlueprintBuilder
{
    /// <summary>
    /// Tham số dựng blueprint. Để ở một chỗ duy nhất thay vì rải số trong service;
    /// test có thể truyền options khác, sau này có thể nạp từ config/DB mà không sửa logic.
    /// </summary>
    public sealed record Options(
        int QuestionsPerSkill = 3,
        double EasyRatio = 0.30,
        double HardRatio = 0.30)
    {
        public static readonly Options Default = new();
    }

    public sealed record BlueprintQuestion(
        int Order,
        string Skill,
        string Topic,
        string Difficulty);

    /// <summary>
    /// Blueprint diagnostic: mỗi core skill có đủ evidence để suy ra level.
    /// Mỗi skill được cấp <see cref="Options.QuestionsPerSkill"/> câu, trong đó bắt buộc:
    /// 1 câu đúng RequiredDifficulty (để xét "đạt target ở đúng độ khó") và
    /// 1 câu ở bậc cao hơn (để có evidence cho level cao hơn — vd. Hard cho Middle).
    /// Slot còn lại lấp theo tỉ lệ Easy/Hard mong muốn của cả đề.
    /// </summary>
    public static object BuildDiagnostic(
        CompetencyFramework framework,
        IReadOnlyList<CompetencyFrameworkSkill> coreSkills,
        Func<CompetencyFrameworkSkill, IReadOnlyList<string>> topicsResolver,
        Options? options = null)
    {
        var opt = options ?? Options.Default;
        var questions = BuildDiagnosticQuestions(coreSkills, topicsResolver, opt);
        return BuildPlanObject(
            roleTitle: $"{framework.DisplayRole} — {framework.TargetLevel} diagnostic",
            displayRole: framework.DisplayRole,
            targetLevel: framework.TargetLevel,
            questions,
            coreSkills.Select(s => s.Skill).ToList());
    }

    /// <summary>SCRUM-457: diagnostic từ CompetencyBlueprint — FRAMEWORK và ADAPTIVE dùng chung.</summary>
    public static object BuildDiagnostic(CompetencyBlueprint blueprint, Options? options = null)
    {
        var skills = FrameworkBlueprintBuilder.ToScoringSkills(blueprint);
        var questions = BuildDiagnosticQuestions(
            skills,
            s =>
            {
                var item = blueprint.Competencies.FirstOrDefault(c =>
                    string.Equals(c.SkillName, s.Skill, StringComparison.OrdinalIgnoreCase));
                return (IReadOnlyList<string>)(item?.Topics.Count > 0 ? item.Topics : new List<string> { s.Skill });
            },
            options);
        return BuildPlanObject(
            roleTitle: $"{blueprint.TargetRole} — {blueprint.TargetLevel} diagnostic",
            displayRole: blueprint.TargetRole,
            targetLevel: blueprint.TargetLevel,
            questions,
            blueprint.Competencies.Select(c => c.SkillName).ToList());
    }

    private static object BuildPlanObject(
        string roleTitle,
        string displayRole,
        string targetLevel,
        List<BlueprintQuestion> questions,
        List<string> skills)
    {
        return new
        {
            roleTitle,
            summary = "Competency diagnostic blueprint sinh từ CompetencyBlueprint (không phải đề tự do).",
            difficulty = QuestionDifficultyLevel.Medium,
            experienceLevel = MapExperience(targetLevel),
            level = QuestionDifficultyLevel.Medium,
            totalQuestions = questions.Count,
            skills,
            questionTypeDistribution = new[]
            {
                new { type = "technical", count = questions.Count, reason = "Competency evidence" }
            },
            difficultyDistribution = BuildDifficultyDistribution(questions),
            recommendedQuestionOutline = questions
                .Select(q => (object)new
                {
                    order = q.Order,
                    type = "technical",
                    difficulty = q.Difficulty,
                    skill = q.Skill,
                    focusArea = q.Topic,
                    topic = q.Topic,
                    evaluationFocus = $"Bằng chứng {q.Skill} / {q.Topic} ở mức {q.Difficulty}",
                    goal = $"Assess {q.Skill} ({q.Topic}) at {q.Difficulty} for {displayRole} {targetLevel}"
                })
                .ToList(),
            notes = $"Target level={targetLevel}; self-assessed chỉ là context. "
                    + "Sinh ĐÚNG số câu và ĐÚNG skill/topic/difficulty theo từng dòng outline. "
                    + "Không thêm skill ngoài danh sách."
        };
    }

    /// <summary>
    /// Danh sách câu hỏi của blueprint diagnostic — tách riêng để unit test kiểm tra
    /// phân bố độ khó và evidence từng skill mà không cần dựng cả service.
    /// </summary>
    public static List<BlueprintQuestion> BuildDiagnosticQuestions(
        IReadOnlyList<CompetencyFrameworkSkill> coreSkills,
        Func<CompetencyFrameworkSkill, IReadOnlyList<string>> topicsResolver,
        Options? options = null)
    {
        var opt = options ?? Options.Default;
        var perSkill = Math.Max(2, opt.QuestionsPerSkill);
        var total = coreSkills.Count * perSkill;

        // Quota easy/hard của cả đề (làm tròn away-from-zero để 4.5 -> 5, tránh lệch xuống 0 khi đề nhỏ).
        var easyQuota = (int)Math.Round(total * opt.EasyRatio, MidpointRounding.AwayFromZero);
        var hardQuota = (int)Math.Round(total * opt.HardRatio, MidpointRounding.AwayFromZero);

        // Bước 1: slot bắt buộc cho từng skill (required + một bậc cao hơn).
        var slots = new List<(CompetencyFrameworkSkill Skill, string? Difficulty)>();
        foreach (var skill in coreSkills)
        {
            var required = NormalizeDifficulty(skill.RequiredDifficulty);
            var above = StepUp(required);
            slots.Add((skill, required));
            slots.Add((skill, above));
            for (var i = 2; i < perSkill; i++)
                slots.Add((skill, null)); // slot linh hoạt
        }

        var usedEasy = slots.Count(s => s.Difficulty == QuestionDifficultyLevel.Easy);
        var usedHard = slots.Count(s => s.Difficulty == QuestionDifficultyLevel.Hard);

        // Bước 2: lấp slot linh hoạt — ưu tiên easy cho tới quota, rồi hard, cuối cùng medium.
        for (var i = 0; i < slots.Count; i++)
        {
            if (slots[i].Difficulty is not null) continue;
            string fill;
            if (usedEasy < easyQuota)
            {
                fill = QuestionDifficultyLevel.Easy;
                usedEasy++;
            }
            else if (usedHard < hardQuota)
            {
                fill = QuestionDifficultyLevel.Hard;
                usedHard++;
            }
            else
            {
                fill = QuestionDifficultyLevel.Medium;
            }
            slots[i] = (slots[i].Skill, fill);
        }

        // Bước 3: sắp xếp tăng dần độ khó (skill quan trọng hơn hỏi trước trong cùng bậc),
        // gán topic round-robin theo skill để không hỏi trùng một topic.
        var topicCursor = new Dictionary<Guid, int>();
        var ordered = slots
            .OrderBy(s => DifficultyRank(s.Difficulty))
            .ThenByDescending(s => s.Skill.ImportanceWeight)
            .ThenBy(s => s.Skill.SortOrder)
            .ToList();

        var result = new List<BlueprintQuestion>(ordered.Count);
        var order = 1;
        foreach (var (skill, difficulty) in ordered)
        {
            var topics = topicsResolver(skill);
            string topic;
            if (topics.Count == 0)
            {
                topic = skill.Skill;
            }
            else
            {
                var idx = topicCursor.TryGetValue(skill.Id, out var c) ? c : 0;
                topic = topics[idx % topics.Count];
                topicCursor[skill.Id] = idx + 1;
            }
            result.Add(new BlueprintQuestion(order++, skill.Skill, topic, difficulty!));
        }
        return result;
    }

    /// <summary>
    /// Blueprint drill: độ khó theo competency hiện tại của skill, không theo level cố định.
    /// Skill đã đạt target vẫn luyện được (band cao -> câu nâng cao).
    /// </summary>
    public static object BuildDrill(string skill, string topic, double? currentScore, double targetScore)
    {
        var band = currentScore ?? 0;
        string[] diffs;
        if (band < targetScore * 0.6)
            diffs = [QuestionDifficultyLevel.Easy, QuestionDifficultyLevel.Easy, QuestionDifficultyLevel.Medium];
        else if (band < targetScore)
            diffs = [QuestionDifficultyLevel.Easy, QuestionDifficultyLevel.Medium, QuestionDifficultyLevel.Medium, QuestionDifficultyLevel.Hard];
        else
            diffs = [QuestionDifficultyLevel.Medium, QuestionDifficultyLevel.Hard, QuestionDifficultyLevel.Hard];

        var outline = diffs
            .Select((d, i) => (object)new
            {
                order = i + 1,
                type = "technical",
                difficulty = d,
                skill,
                focusArea = topic,
                topic,
                evaluationFocus = $"Bằng chứng {skill} / {topic} ở mức {d}",
                goal = $"Drill {topic} ({skill})"
            })
            .ToList();

        return new
        {
            roleTitle = $"Drill — {skill} / {topic}",
            summary = "Targeted skill drill; điểm drill không cập nhật competency chính thức.",
            difficulty = QuestionDifficultyLevel.Medium,
            experienceLevel = "mid",
            level = QuestionDifficultyLevel.Medium,
            totalQuestions = outline.Count,
            skills = new[] { skill },
            questionTypeDistribution = new[]
            {
                new { type = "technical", count = outline.Count, reason = "Drill" }
            },
            difficultyDistribution = diffs
                .GroupBy(x => x)
                .Select(g => new { difficulty = g.Key, count = g.Count() })
                .ToList(),
            recommendedQuestionOutline = outline,
            notes = $"Chỉ hỏi skill \"{skill}\", tập trung topic \"{topic}\". "
                    + "Sinh ĐÚNG số câu theo outline, không lặp đề cũ, không bịa JD."
        };
    }

    public static List<object> BuildDifficultyDistribution(IEnumerable<BlueprintQuestion> questions)
        => questions
            .GroupBy(q => q.Difficulty)
            .OrderBy(g => DifficultyRank(g.Key))
            .Select(g => (object)new { difficulty = g.Key, count = g.Count() })
            .ToList();

    /// <summary>Map level nghiệp vụ (Fresher/Junior/...) sang experienceLevel mà RAG hiểu.</summary>
    public static string MapExperience(string? targetLevel)
    {
        var t = (targetLevel ?? CoachSeniorityLevel.Junior).Trim().ToLowerInvariant();
        return t switch
        {
            "fresher" or "intern" => "intern",
            "middle" or "mid" => "mid",
            "senior" => "senior",
            _ => "junior"
        };
    }

    public static string NormalizeDifficulty(string? difficulty)
    {
        var d = (difficulty ?? "").Trim().ToLowerInvariant();
        return d switch
        {
            QuestionDifficultyLevel.Easy => QuestionDifficultyLevel.Easy,
            QuestionDifficultyLevel.Hard => QuestionDifficultyLevel.Hard,
            _ => QuestionDifficultyLevel.Medium
        };
    }

    /// <summary>Bậc khó ngay trên mức yêu cầu; hard đã là cao nhất nên giữ hard.</summary>
    public static string StepUp(string difficulty)
        => NormalizeDifficulty(difficulty) switch
        {
            QuestionDifficultyLevel.Easy => QuestionDifficultyLevel.Medium,
            QuestionDifficultyLevel.Medium => QuestionDifficultyLevel.Hard,
            _ => QuestionDifficultyLevel.Hard
        };

    private static int DifficultyRank(string? difficulty)
        => NormalizeDifficulty(difficulty) switch
        {
            QuestionDifficultyLevel.Easy => 1,
            QuestionDifficultyLevel.Hard => 3,
            _ => 2
        };
}
