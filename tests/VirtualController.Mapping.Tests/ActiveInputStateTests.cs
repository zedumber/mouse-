using VirtualController.Core.Mapping;
using Xunit;

namespace VirtualController.Mapping.Tests;

public class ActiveInputStateTests
{
    [Fact]
    public void NewState_HasNothingActive()
    {
        var state = new ActiveInputState();

        Assert.Empty(state.Active);
        Assert.False(state.IsActive(PhysicalInput.FromKey(Key.W)));
    }

    [Fact]
    public void SetActive_True_MarksInputAsActive()
    {
        var state = new ActiveInputState();
        var w = PhysicalInput.FromKey(Key.W);

        state.SetActive(w, active: true);

        Assert.True(state.IsActive(w));
        Assert.Contains(w, state.Active);
    }

    [Fact]
    public void SetActive_False_RemovesInput()
    {
        var state = new ActiveInputState();
        var w = PhysicalInput.FromKey(Key.W);
        state.SetActive(w, active: true);

        state.SetActive(w, active: false);

        Assert.False(state.IsActive(w));
        Assert.DoesNotContain(w, state.Active);
    }

    [Fact]
    public void MultipleInputs_AreTrackedIndependently()
    {
        var state = new ActiveInputState();
        var w = PhysicalInput.FromKey(Key.W);
        var d = PhysicalInput.FromKey(Key.D);

        state.SetActive(w, active: true);
        state.SetActive(d, active: true);
        state.SetActive(w, active: false);

        Assert.False(state.IsActive(w));
        Assert.True(state.IsActive(d));
    }
}
