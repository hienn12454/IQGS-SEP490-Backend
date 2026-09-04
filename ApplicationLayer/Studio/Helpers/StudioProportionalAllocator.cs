namespace ApplicationLayer.Studio.Helpers;

/// <summary>
/// SCRUM-435: Phân bổ số nguyên theo trọng số (largest remainder), cho phép 0 khi total &lt; n.
/// Dùng cho focus → coverage / outline slots.
/// </summary>
public static class StudioProportionalAllocator
{
    /// <summary>
    /// Trả về n số nguyên tổng = total, tỷ lệ theo weights; weight ≤ 0 → có thể nhận 0.
    /// </summary>
    public static IReadOnlyList<int> LargestRemainder(IReadOnlyList<int> weights, int total)
    {
        var n = weights.Count;
        var result = new int[n];
        if (n == 0 || total <= 0) return result;

        var sumW = weights.Sum(w => Math.Max(0, w));
        if (sumW <= 0)
        {
            // Tất cả 0 → chia đều (rem từ đầu)
            var bas = total / n;
            var rem = total % n;
            for (var i = 0; i < n; i++)
                result[i] = bas + (i < rem ? 1 : 0);
            return result;
        }

        var floors = new int[n];
        var frac = new (int Index, double Frac)[n];
        var assigned = 0;
        for (var i = 0; i < n; i++)
        {
            var w = Math.Max(0, weights[i]);
            var exact = total * (w / (double)sumW);
            floors[i] = (int)Math.Floor(exact);
            assigned += floors[i];
            frac[i] = (i, exact - floors[i]);
        }

        var left = total - assigned;
        foreach (var (index, _) in frac.OrderByDescending(x => x.Frac).ThenBy(x => x.Index))
        {
            if (left <= 0) break;
            floors[index]++;
            left--;
        }

        return floors;
    }

    /// <summary>Queue tên skill dài đúng total — theo weight % / count.</summary>
    public static IReadOnlyList<string> BuildNameQueue(
        IReadOnlyList<(string Name, int Weight)> items,
        int total)
    {
        if (items.Count == 0 || total <= 0) return Array.Empty<string>();
        var ordered = items
            .Where(x => !string.IsNullOrWhiteSpace(x.Name))
            .ToList();
        if (ordered.Count == 0) return Array.Empty<string>();

        var weights = ordered.Select(x => Math.Max(0, x.Weight)).ToList();
        var counts = LargestRemainder(weights, total);
        var list = new List<string>(total);
        for (var i = 0; i < ordered.Count; i++)
        {
            var n = i < counts.Count ? counts[i] : 0;
            for (var k = 0; k < n; k++)
                list.Add(ordered[i].Name.Trim());
        }

        // Pad nếu thiếu (edge) — ưu tiên item weight cao nhất / đầu tiên
        while (list.Count < total)
            list.Add(ordered[0].Name.Trim());
        if (list.Count > total)
            list.RemoveRange(total, list.Count - total);
        return list;
    }
}
