using System.Security.Cryptography;
using System.Text;
using DomainLayer.Entities;

namespace ApplicationLayer.Helpers;

/// <summary>SHA-256 của JD + câu hỏi active — phát hiện review đã cũ khi nội dung đổi.</summary>
public static class JdFitContentHash
{
    public static string Compute(string jobDescription, IEnumerable<QuestionSetQuestion> activeQuestions)
    {
        var sb = new StringBuilder();
        sb.Append((jobDescription ?? string.Empty).Trim().Replace("\r\n", "\n"));
        sb.Append('\n');

        foreach (var q in activeQuestions.OrderBy(x => x.Order).ThenBy(x => x.Id))
        {
            sb.Append(q.Id.ToString("D"));
            sb.Append('|');
            sb.Append(q.Order);
            sb.Append('|');
            sb.Append(q.QuestionType ?? "");
            sb.Append('|');
            sb.Append(q.Skill ?? "");
            sb.Append('|');
            sb.Append((q.Question ?? "").Trim().Replace("\r\n", "\n"));
            sb.Append('\n');
        }

        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
