using VirtualController.Core.Gamepad;
using VirtualController.Core.Mapping;
using VirtualController.Core.Profiles;
using VirtualController.TestUtilities;
using Xunit;

namespace VirtualController.Core.Tests;

public class BindingEditorTests
{
    private static (BindingEditor Editor, InMemoryProfileRepository Repository, Profile Profile) Create()
    {
        var repository = new InMemoryProfileRepository();
        var profile = DefaultProfile.Create("Editable");
        repository.Save(profile);
        return (new BindingEditor(repository, profile), repository, profile);
    }

    private static Binding SpaceToA => new(
        PhysicalInput.FromKey(Key.Space),
        new VirtualOutput.DigitalButton(GamepadButton.A));

    [Fact]
    public void NewEditor_StartsWithProfileBindingsAndNoChanges()
    {
        var (editor, _, profile) = Create();

        Assert.Equal(profile.Bindings.Count, editor.Bindings.Count);
        Assert.False(editor.HasUnsavedChanges);
    }

    [Fact]
    public void Add_AppendsBindingAndMarksDirty()
    {
        var (editor, _, profile) = Create();

        editor.Add(new Binding(PhysicalInput.FromKey(Key.E), new VirtualOutput.DigitalButton(GamepadButton.B)));

        Assert.Equal(profile.Bindings.Count + 1, editor.Bindings.Count);
        Assert.True(editor.HasUnsavedChanges);
    }

    [Fact]
    public void RemoveAt_DeletesBinding()
    {
        var (editor, _, profile) = Create();

        editor.RemoveAt(0);

        Assert.Equal(profile.Bindings.Count - 1, editor.Bindings.Count);
        Assert.True(editor.HasUnsavedChanges);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(999)]
    public void RemoveAt_OutOfRange_Throws(int index)
    {
        var (editor, _, _) = Create();

        Assert.Throws<ArgumentOutOfRangeException>(() => editor.RemoveAt(index));
    }

    [Fact]
    public void Replace_SwapsBindingInPlace()
    {
        var (editor, _, _) = Create();

        editor.Replace(0, SpaceToA);

        Assert.Equal(SpaceToA, editor.Bindings[0]);
    }

    [Fact]
    public void Reset_DiscardsChanges()
    {
        var (editor, _, profile) = Create();
        editor.Add(SpaceToA);
        editor.RemoveAt(0);

        editor.Reset();

        Assert.Equal(profile.Bindings.Count, editor.Bindings.Count);
        Assert.False(editor.HasUnsavedChanges);
    }

    [Fact]
    public void Save_PersistsBindingsToRepository()
    {
        var (editor, repository, profile) = Create();
        editor.Add(new Binding(PhysicalInput.FromKey(Key.E), new VirtualOutput.DigitalButton(GamepadButton.B)));

        var saved = editor.Save();

        Assert.False(editor.HasUnsavedChanges);
        Assert.Equal(saved.Bindings.Count, repository.Load(profile.Id).Bindings.Count);
        Assert.Contains(repository.Load(profile.Id).Bindings, b => b.Input == PhysicalInput.FromKey(Key.E));
    }

    [Fact]
    public void Save_KeepsProfileIdentity()
    {
        var (editor, _, profile) = Create();
        editor.Add(SpaceToA);

        var saved = editor.Save();

        Assert.Equal(profile.Id, saved.Id);
        Assert.Equal(profile.Name, saved.Name);
    }

    [Fact]
    public void Validate_DetectsStickConflictBeforeSaving()
    {
        var (editor, _, _) = Create();

        // El mouse ya ocupa el stick derecho en el perfil por defecto; añadir una dirección al mismo
        // stick es el conflicto que ADR-006 punto 4 rechaza.
        editor.Add(new Binding(
            PhysicalInput.FromKey(Key.E),
            new VirtualOutput.StickDirection(Stick.Right, Direction.Up)));

        var error = editor.Validate();

        Assert.NotNull(error);
        Assert.Contains("Right", error);
    }

    [Fact]
    public void Validate_ReturnsNullWhenValid()
    {
        var (editor, _, _) = Create();

        editor.Add(new Binding(PhysicalInput.FromKey(Key.E), new VirtualOutput.DigitalButton(GamepadButton.B)));

        Assert.Null(editor.Validate());
    }

    [Fact]
    public void Save_WithConflict_ThrowsAndLeavesStoredProfileUntouched()
    {
        var (editor, repository, profile) = Create();
        var originalCount = repository.Load(profile.Id).Bindings.Count;

        editor.Add(new Binding(
            PhysicalInput.FromKey(Key.E),
            new VirtualOutput.StickDirection(Stick.Right, Direction.Up)));

        Assert.Throws<ProfileValidationException>(() => editor.Save());

        // Lo importante: el perfil en disco sigue siendo el último que funcionaba.
        Assert.Equal(originalCount, repository.Load(profile.Id).Bindings.Count);
        Assert.True(editor.HasUnsavedChanges);
    }

    [Fact]
    public void DuplicateInputToSameOutput_IsAllowed()
    {
        var (editor, _, _) = Create();

        // Dos teclas al mismo botón es la semántica OR que ADR-006 punto 3 soporta a propósito.
        editor.Add(new Binding(PhysicalInput.FromKey(Key.E), new VirtualOutput.DigitalButton(GamepadButton.A)));
        editor.Add(new Binding(PhysicalInput.FromKey(Key.R), new VirtualOutput.DigitalButton(GamepadButton.A)));

        Assert.Null(editor.Validate());
        editor.Save();
    }

    [Fact]
    public void RemovingMouseVector_FreesStickForDirections()
    {
        var (editor, _, _) = Create();
        var mouseIndex = editor.Bindings
            .Select((b, i) => (b, i))
            .First(x => x.b.Output is VirtualOutput.StickVector).i;

        editor.RemoveAt(mouseIndex);
        editor.Add(new Binding(
            PhysicalInput.FromKey(Key.E),
            new VirtualOutput.StickDirection(Stick.Right, Direction.Up)));

        Assert.Null(editor.Validate());
    }

    [Fact]
    public void EmptyBindingSet_IsValid()
    {
        var (editor, _, _) = Create();

        while (editor.Bindings.Count > 0)
        {
            editor.RemoveAt(0);
        }

        Assert.Null(editor.Validate());
        editor.Save();
    }
}

public class BindingCatalogTests
{
    [Fact]
    public void AvailableInputs_CoverKeysMouseButtonsAndMovement()
    {
        var inputs = BindingCatalog.AvailableInputs;

        Assert.Contains(PhysicalInput.FromKey(Key.W), inputs);
        Assert.Contains(PhysicalInput.FromMouseButton(MouseButton.Left), inputs);
        Assert.Contains(PhysicalInput.MouseMovement, inputs);
        Assert.Equal(inputs.Distinct().Count(), inputs.Count);
    }

    [Fact]
    public void AvailableOutputs_CoverButtonsTriggersDirectionsAndVectors()
    {
        var outputs = BindingCatalog.AvailableOutputs;

        Assert.Contains(outputs, o => o is VirtualOutput.DigitalButton { Button: GamepadButton.A });
        Assert.Contains(outputs, o => o is VirtualOutput.AnalogValue { Axis: GamepadAxis.RightTrigger });
        Assert.Contains(outputs, o => o is VirtualOutput.StickDirection { Stick: Stick.Left, Direction: Direction.Up });
        Assert.Contains(outputs, o => o is VirtualOutput.StickVector { Stick: Stick.Right });
    }

    [Fact]
    public void EveryCatalogEntryHasAReadableName()
    {
        foreach (var input in BindingCatalog.AvailableInputs)
        {
            var text = BindingCatalog.Describe(input);
            Assert.False(string.IsNullOrWhiteSpace(text));
            Assert.DoesNotContain(".", text); // no debe filtrarse el formato técnico "Keyboard.W"
        }

        foreach (var output in BindingCatalog.AvailableOutputs)
        {
            Assert.False(string.IsNullOrWhiteSpace(BindingCatalog.Describe(output)));
        }
    }

    [Fact]
    public void TriggersAreOfferedFullyPressed()
    {
        var trigger = BindingCatalog.AvailableOutputs
            .OfType<VirtualOutput.AnalogValue>()
            .First(a => a.Axis == GamepadAxis.RightTrigger);

        Assert.Equal(1f, trigger.Value);
    }
}
