using VirtualController.App.ViewModels;
using VirtualController.Core.Gamepad;
using VirtualController.Core.Mapping;
using VirtualController.Core.Profiles;
using VirtualController.TestUtilities;

namespace VirtualController.App.Tests;

/// <summary>
/// Cubre el editor de bindings de la interfaz (requisitos 14 y 17). Los ViewModel no usan tipos de
/// WPF (ADR-001), así que se pueden ejercitar sin abrir ninguna ventana.
/// </summary>
public class BindingEditorViewModelTests
{
    private static (BindingEditorViewModel Vm, InMemoryProfileRepository Repository, Profile Profile) Create(
        Action<Profile>? onSaved = null)
    {
        var repository = new InMemoryProfileRepository();
        var profile = DefaultProfile.Create("Editable");
        repository.Save(profile);

        var editor = new BindingEditor(repository, profile);
        return (new BindingEditorViewModel(editor, onSaved), repository, profile);
    }

    [Fact]
    public void Constructor_LoadsOneRowPerBinding()
    {
        var (vm, _, profile) = Create();

        Assert.Equal(profile.Bindings.Count, vm.Rows.Count);
    }

    [Fact]
    public void Constructor_StartsClean()
    {
        var (vm, _, _) = Create();

        Assert.False(vm.HasUnsavedChanges);
        Assert.Null(vm.ValidationError);
        Assert.False(vm.CanSave);
    }

    [Fact]
    public void AvailableOptions_AreOfferedForTheDropdowns()
    {
        var (vm, _, _) = Create();

        Assert.NotEmpty(vm.AvailableInputs);
        Assert.NotEmpty(vm.AvailableOutputs);
        Assert.All(vm.AvailableInputs, o => Assert.False(string.IsNullOrWhiteSpace(o.Display)));
        Assert.All(vm.AvailableOutputs, o => Assert.False(string.IsNullOrWhiteSpace(o.Display)));
    }

    [Fact]
    public void AddBinding_AddsRowAndEnablesSaving()
    {
        var (vm, _, profile) = Create();

        vm.AddBindingCommand.Execute(null);

        Assert.Equal(profile.Bindings.Count + 1, vm.Rows.Count);
        Assert.True(vm.HasUnsavedChanges);
        Assert.True(vm.CanSave);
    }

    [Fact]
    public void RemoveBinding_RemovesTheSelectedRow()
    {
        var (vm, _, profile) = Create();
        var target = vm.Rows[0];

        vm.RemoveBindingCommand.Execute(target);

        Assert.Equal(profile.Bindings.Count - 1, vm.Rows.Count);
        Assert.DoesNotContain(target, vm.Rows);
    }

    [Fact]
    public void RemoveBinding_WithNull_IsIgnored()
    {
        var (vm, _, profile) = Create();

        vm.RemoveBindingCommand.Execute(null);

        Assert.Equal(profile.Bindings.Count, vm.Rows.Count);
    }

    [Fact]
    public void ChangingARow_SyncsToTheEditorAndMarksDirty()
    {
        var (vm, repository, profile) = Create();

        vm.Rows[0].Output = OutputOption.For(new VirtualOutput.DigitalButton(GamepadButton.Start));
        vm.SaveCommand.Execute(null);

        var saved = repository.Load(profile.Id);
        Assert.Contains(saved.Bindings, b => b.Output is VirtualOutput.DigitalButton { Button: GamepadButton.Start });
    }

    [Fact]
    public void ChangingAnInput_IsPersisted()
    {
        var (vm, repository, profile) = Create();

        vm.Rows[0].Input = InputOption.For(PhysicalInput.FromKey(Key.E));
        vm.SaveCommand.Execute(null);

        Assert.Contains(repository.Load(profile.Id).Bindings, b => b.Input == PhysicalInput.FromKey(Key.E));
    }

    [Fact]
    public void Save_PersistsAndReportsSuccess()
    {
        var (vm, repository, profile) = Create();
        vm.AddBindingCommand.Execute(null);

        vm.SaveCommand.Execute(null);

        Assert.False(vm.HasUnsavedChanges);
        Assert.NotNull(vm.StatusMessage);
        Assert.Null(vm.ValidationError);
        Assert.True(repository.Load(profile.Id).Bindings.Count > profile.Bindings.Count);
    }

    [Fact]
    public void Save_NotifiesSoTheEngineCanApplyTheNewProfile()
    {
        Profile? notified = null;
        var (vm, _, _) = Create(onSaved: p => notified = p);

        vm.AddBindingCommand.Execute(null);
        vm.SaveCommand.Execute(null);

        Assert.NotNull(notified);
    }

    [Fact]
    public void ConflictingBindings_BlockSavingAndExplainWhy()
    {
        var (vm, repository, profile) = Create();
        var originalCount = repository.Load(profile.Id).Bindings.Count;

        // El mouse ya ocupa el stick derecho; asignarle además una dirección es el conflicto que
        // ADR-006 punto 4 rechaza.
        vm.AddBindingCommand.Execute(null);
        vm.Rows[^1].Output = OutputOption.For(new VirtualOutput.StickDirection(Stick.Right, Direction.Up));

        Assert.NotNull(vm.ValidationError);
        Assert.False(vm.CanSave);

        // Aunque se fuerce el guardado, el perfil en disco no debe cambiar.
        vm.SaveCommand.Execute(null);
        Assert.Equal(originalCount, repository.Load(profile.Id).Bindings.Count);
    }

    [Fact]
    public void FixingAConflict_ReenablesSaving()
    {
        var (vm, _, _) = Create();
        vm.AddBindingCommand.Execute(null);
        vm.Rows[^1].Output = OutputOption.For(new VirtualOutput.StickDirection(Stick.Right, Direction.Up));
        Assert.False(vm.CanSave);

        vm.Rows[^1].Output = OutputOption.For(new VirtualOutput.DigitalButton(GamepadButton.B));

        Assert.Null(vm.ValidationError);
        Assert.True(vm.CanSave);
    }

    [Fact]
    public void Discard_RestoresTheSavedBindings()
    {
        var (vm, _, profile) = Create();
        vm.AddBindingCommand.Execute(null);
        vm.AddBindingCommand.Execute(null);

        vm.DiscardCommand.Execute(null);

        Assert.Equal(profile.Bindings.Count, vm.Rows.Count);
        Assert.False(vm.HasUnsavedChanges);
    }

    [Fact]
    public void Discard_DoesNotResurrectStaleRowsIntoTheEditor()
    {
        // Regresión: si al reconstruir las filas se disparara la sincronización, el editor recibiría
        // los datos de las filas viejas y descartar no descartaría nada.
        var (vm, repository, profile) = Create();
        vm.RemoveBindingCommand.Execute(vm.Rows[0]);

        vm.DiscardCommand.Execute(null);
        vm.AddBindingCommand.Execute(null);
        vm.SaveCommand.Execute(null);

        Assert.Equal(profile.Bindings.Count + 1, repository.Load(profile.Id).Bindings.Count);
    }

    [Fact]
    public void RemovingEveryBinding_IsAllowed()
    {
        var (vm, repository, profile) = Create();

        while (vm.Rows.Count > 0)
        {
            vm.RemoveBindingCommand.Execute(vm.Rows[0]);
        }

        vm.SaveCommand.Execute(null);

        Assert.Empty(repository.Load(profile.Id).Bindings);
    }
}
