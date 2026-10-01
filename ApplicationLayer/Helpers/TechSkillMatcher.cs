namespace ApplicationLayer.Helpers;

/// <summary>
/// SCRUM-503: so khớp tên công nghệ theo TOKEN, không dùng substring thô.
/// Lý do: cách cũ (<c>value.Contains(allowed)</c>) làm "JavaScript" khớp slot "Java",
/// "Django"/"MongoDB" khớp "Go" — câu hỏi lạc đề bị dán nhãn đúng skill rồi pass validator.
/// Ở đây token là đơn vị nhỏ nhất: "java" ≠ "javascript", nhưng "ASP.NET Core Web API" vẫn
/// khớp "ASP.NET Core" vì chứa đúng dãy token liền kề.
/// </summary>
public static class TechSkillMatcher
{
    /// <summary>Mức độ khớp — dùng để chọn ứng viên tốt nhất khi có nhiều skill hợp lệ.</summary>
    public enum MatchKind
    {
        None = 0,
        /// <summary>Chứa dãy token của nhau (vd. "ASP.NET Core Web API" ⊇ "ASP.NET Core").</summary>
        TokenSubset = 1,
        /// <summary>Trùng sau khi quy đổi alias (vd. "golang" → "go").</summary>
        Canonical = 2,
        /// <summary>Trùng tuyệt đối sau khi bỏ dấu cách / gạch (vd. "EF Core" == "ef-core").</summary>
        Exact = 3
    }

    /// <summary>
    /// Biến thể phổ biến → tên chuẩn. Chỉ ghi những cặp thật sự đồng nghĩa;
    /// cặp dễ nhầm (java/javascript, go/golang vs django) KHÔNG được đưa vào đây.
    /// </summary>
    private static readonly Dictionary<string, string> TokenAliases = new(StringComparer.Ordinal)
    {
        ["csharp"] = "c#",
        ["csharp.net"] = "c#",
        ["cpp"] = "c++",
        ["cplusplus"] = "c++",
        ["golang"] = "go",
        ["dotnet"] = ".net",
        ["dotnetcore"] = ".net",
        ["js"] = "javascript",
        ["ts"] = "typescript",
        ["reactjs"] = "react",
        ["react.js"] = "react",
        ["vuejs"] = "vue",
        ["vue.js"] = "vue",
        ["nextjs"] = "next.js",
        ["nestjs"] = "nest.js",
        ["nodejs"] = "node.js",
        ["node"] = "node.js",
        ["angularjs"] = "angular",
        ["postgres"] = "postgresql",
        ["psql"] = "postgresql",
        ["k8s"] = "kubernetes",
        ["ef"] = "entityframework",
        ["efcore"] = "entityframework",
        ["mssql"] = "sqlserver",
        ["restful"] = "rest"
    };

    /// <summary>
    /// Tên ghép buộc khớp tuyệt đối — không cho luật TokenSubset kéo về skill cha.
    /// Vd. "React Native" là stack khác "React", không được tính là câu hỏi React.
    /// </summary>
    private static readonly HashSet<string> ExactOnly = new(StringComparer.Ordinal)
    {
        "reactnative", "springbatch", "nosql", "javascript", "typescript"
    };

    /// <summary>Ngôn ngữ lập trình — dùng để phát hiện câu hỏi "nhảy ngôn ngữ".</summary>
    private static readonly HashSet<string> ProgrammingLanguages = new(StringComparer.Ordinal)
    {
        "c#", "java", "javascript", "typescript", "python", "go", "rust", "kotlin", "swift",
        "php", "ruby", "scala", "dart", "c", "c++", "sql"
    };

    /// <summary>
    /// Cặp dễ nhầm theo tên — dùng cho hậu kiểm nội dung câu hỏi.
    /// Key/value đều ở dạng compact (bỏ dấu cách / gạch, chữ thường).
    /// </summary>
    private static readonly Dictionary<string, string[]> Confusables = new(StringComparer.Ordinal)
    {
        ["java"] = ["javascript", "typescript"],
        ["javascript"] = ["java"],
        ["go"] = ["django", "mongodb", "golang"],
        ["c"] = ["c#", "c++"],
        ["c#"] = ["c", "c++"],
        ["c++"] = ["c", "c#"],
        ["sql"] = ["nosql", "mysql", "postgresql", "sqlserver"],
        ["react"] = ["reactnative", "angular", "vue"],
        ["vue"] = ["react", "angular"],
        ["angular"] = ["react", "angularjs", "vue"]
    };

    /// <summary>Bỏ dấu cách / gạch / ký tự phụ: "EF Core" → "efcore" (giữ # + .).</summary>
    public static string Compact(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var chars = value
            .Trim()
            .ToLowerInvariant()
            .Where(c => char.IsLetterOrDigit(c) || c is '#' or '+' or '.')
            .ToArray();
        // Chỉ bỏ dấu chấm cuối (dấu câu); dấu chấm đầu phải giữ cho ".NET".
        return new string(chars).TrimEnd('.');
    }

    /// <summary>
    /// Tách thành token đã quy đổi alias. Dấu cách, gạch, dấu phẩy… đều là ranh giới token;
    /// '#', '+', '.' giữ trong token để không phá "C#", "C++", ".NET", "Node.js".
    /// </summary>
    public static List<string> Tokens(string? value)
    {
        var result = new List<string>();
        if (string.IsNullOrWhiteSpace(value)) return result;

        var buffer = new System.Text.StringBuilder();
        foreach (var c in value.ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(c) || c is '#' or '+' or '.')
                buffer.Append(c);
            else
                Flush(buffer, result);
        }
        Flush(buffer, result);
        return result;
    }

    private static void Flush(System.Text.StringBuilder buffer, List<string> result)
    {
        if (buffer.Length == 0) return;
        // Dấu chấm cuối câu không thuộc tên công nghệ ("dùng Java." → "java"),
        // nhưng dấu chấm đầu phải giữ cho ".NET".
        var token = buffer.ToString().TrimEnd('.');
        buffer.Clear();
        if (token.Length == 0) return;
        result.Add(TokenAliases.TryGetValue(token, out var canonical) ? canonical : token);
    }

    /// <summary>So hai tên công nghệ; <see cref="MatchKind.None"/> nghĩa là khác skill.</summary>
    public static MatchKind Compare(string? candidate, string? allowed)
    {
        var candidateCompact = Compact(candidate);
        var allowedCompact = Compact(allowed);
        if (candidateCompact.Length == 0 || allowedCompact.Length == 0) return MatchKind.None;
        if (candidateCompact == allowedCompact) return MatchKind.Exact;

        var candidateTokens = Tokens(candidate);
        var allowedTokens = Tokens(allowed);
        if (candidateTokens.Count == 0 || allowedTokens.Count == 0) return MatchKind.None;

        if (SameSequence(candidateTokens, allowedTokens)) return MatchKind.Canonical;

        // Tên ghép riêng biệt (React Native…) chỉ được khớp tuyệt đối.
        if (ExactOnly.Contains(candidateCompact) || ExactOnly.Contains(allowedCompact))
            return MatchKind.None;

        if (ContainsSequence(candidateTokens, allowedTokens)
            || ContainsSequence(allowedTokens, candidateTokens))
            return MatchKind.TokenSubset;

        return MatchKind.None;
    }

    /// <summary>
    /// Map tên skill LLM trả về sang đúng skill trong danh sách cho phép.
    /// Ưu tiên mức khớp cao nhất, sau đó tên dài nhất cho ổn định.
    /// </summary>
    public static string? MapToAllowed(string? raw, IReadOnlyList<string> allowedSkills)
    {
        string? best = null;
        var bestKind = MatchKind.None;
        var bestLen = 0;

        foreach (var allowed in allowedSkills)
        {
            var kind = Compare(raw, allowed);
            if (kind == MatchKind.None) continue;
            var len = Compact(allowed).Length;
            if (kind > bestKind || (kind == bestKind && len > bestLen))
            {
                best = allowed;
                bestKind = kind;
                bestLen = len;
            }
        }
        return best;
    }

    /// <summary>Đoạn text (câu hỏi, focus area…) có nhắc tới skill này hay không.</summary>
    public static bool MentionsSkill(string? text, string? skill)
    {
        var skillTokens = Tokens(skill);
        if (skillTokens.Count == 0) return false;
        var textTokens = Tokens(text);
        if (textTokens.Count == 0) return false;
        return ContainsSequence(textTokens, skillTokens);
    }

    /// <summary>
    /// Skill dễ bị nhầm với <paramref name="skill"/> mà text có nhắc tới.
    /// Dùng cho hậu kiểm: câu hỏi nói về JavaScript trong khi slot là Java.
    /// </summary>
    public static string? FindConflictingSkill(string? text, string? skill)
    {
        var skillCompact = Compact(skill);
        if (skillCompact.Length == 0) return null;

        if (Confusables.TryGetValue(skillCompact, out var siblings))
        {
            foreach (var sibling in siblings)
            {
                if (sibling == skillCompact) continue;
                if (MentionsSkill(text, sibling)) return sibling;
            }
        }

        // Slot là ngôn ngữ nhưng câu hỏi lại nói về ngôn ngữ khác.
        if (ProgrammingLanguages.Contains(skillCompact))
        {
            foreach (var language in ProgrammingLanguages)
            {
                if (language == skillCompact) continue;
                if (Compare(language, skill) != MatchKind.None) continue;
                if (MentionsSkill(text, language)) return language;
            }
        }
        return null;
    }

    private static bool SameSequence(List<string> left, List<string> right)
    {
        if (left.Count != right.Count) return false;
        for (var i = 0; i < left.Count; i++)
            if (left[i] != right[i]) return false;
        return true;
    }

    /// <summary><paramref name="needle"/> là dãy token liền kề nằm trong <paramref name="haystack"/>.</summary>
    private static bool ContainsSequence(List<string> haystack, List<string> needle)
    {
        if (needle.Count == 0 || needle.Count > haystack.Count) return false;
        for (var i = 0; i + needle.Count <= haystack.Count; i++)
        {
            var ok = true;
            for (var j = 0; j < needle.Count; j++)
            {
                if (haystack[i + j] == needle[j]) continue;
                ok = false;
                break;
            }
            if (ok) return true;
        }
        return false;
    }
}
