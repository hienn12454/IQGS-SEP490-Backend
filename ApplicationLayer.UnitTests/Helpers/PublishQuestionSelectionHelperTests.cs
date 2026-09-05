using ApplicationLayer.Helpers;
using Xunit;

namespace ApplicationLayer.UnitTests.Helpers;

public class PublishQuestionSelectionHelperTests
{
    [Fact]
    public void ApplySelection_SoftDeactivatesUnselected()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var c = Guid.NewGuid();
        var state = new Dictionary<Guid, bool> { [a] = true, [b] = true, [c] = true };

        var active = PublishQuestionSelectionHelper.ApplySelection(
            state.Select(kv => (kv.Key, kv.Value)),
            new[] { a, c },
            (id, on) => state[id] = on);

        Assert.Equal(2, active);
        Assert.True(state[a]);
        Assert.False(state[b]);
        Assert.True(state[c]);
    }

    [Fact]
    public void ApplySelection_EmptySelected_ReturnsCurrentActiveCount()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var state = new Dictionary<Guid, bool> { [a] = true, [b] = false };
        var calls = 0;

        var active = PublishQuestionSelectionHelper.ApplySelection(
            state.Select(kv => (kv.Key, kv.Value)),
            Array.Empty<Guid>(),
            (_, _) => calls++);

        Assert.Equal(1, active);
        Assert.Equal(0, calls);
    }

    [Fact]
    public void ApplySelection_UnknownId_Throws()
    {
        var a = Guid.NewGuid();
        Assert.Throws<ArgumentException>(() =>
            PublishQuestionSelectionHelper.ApplySelection(
                new[] { (a, true) },
                new[] { Guid.NewGuid() },
                (_, _) => { }));
    }

    [Fact]
    public void MapInterviewSelection_PrefersContentThenOrder()
    {
        var iq1 = Guid.NewGuid();
        var iq2 = Guid.NewGuid();
        var sq1 = Guid.NewGuid();
        var sq2 = Guid.NewGuid();
        var sq3 = Guid.NewGuid();

        var mapped = PublishQuestionSelectionHelper.MapInterviewSelectionToSetQuestionIds(
            new[] { (iq1, "What is React?", 1), (iq2, "Explain SQL join", 2) },
            new[] { (sq1, "What is React?", 9), (sq2, "Other", 2), (sq3, "Explain SQL join", 3) });

        Assert.Equal(new[] { sq1, sq3 }, mapped);
    }
}
