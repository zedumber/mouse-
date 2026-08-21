using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VirtualController.Core.Profiles;

namespace VirtualController.App.ViewModels;

/// <summary>Gestiona perfiles sin filtrar detalles de persistencia a la vista.</summary>
public sealed partial class ProfileSelectorViewModel : ObservableObject
{
    private readonly ProfileService _profiles;
    private readonly Action<Profile> _onSelected;
    private readonly Func<string, bool>? _confirmDelete;
    private bool _suspendSelection;

    public ProfileSelectorViewModel(
        ProfileService profiles,
        Profile selected,
        Action<Profile> onSelected,
        Func<string, bool>? confirmDelete = null)
    {
        _profiles = profiles;
        _onSelected = onSelected;
        _confirmDelete = confirmDelete;
        Reload(selected.Id);
    }

    public ObservableCollection<ProfileMetadata> Profiles { get; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DuplicateCommand))]
    [NotifyCanExecuteChangedFor(nameof(RenameCommand))]
    [NotifyCanExecuteChangedFor(nameof(DeleteCommand))]
    private ProfileMetadata? _selectedProfile;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CreateCommand))]
    [NotifyCanExecuteChangedFor(nameof(RenameCommand))]
    private string _nameDraft = string.Empty;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private string? _statusMessage;

    [RelayCommand(CanExecute = nameof(CanCreate))]
    private void Create()
    {
        Execute(() =>
        {
            var created = _profiles.Create(NameDraft);
            Reload(created.Id);
            Select(created);
            StatusMessage = "Perfil creado.";
        });
    }

    private bool CanCreate() => !string.IsNullOrWhiteSpace(NameDraft);

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void Duplicate()
    {
        Execute(() =>
        {
            var copy = _profiles.Duplicate(SelectedProfile!.Id);
            Reload(copy.Id);
            Select(copy);
            StatusMessage = "Perfil duplicado.";
        });
    }

    [RelayCommand(CanExecute = nameof(CanRename))]
    private void Rename()
    {
        Execute(() =>
        {
            var renamed = _profiles.Rename(SelectedProfile!.Id, NameDraft);
            Reload(renamed.Id);
            Select(renamed);
            StatusMessage = "Perfil renombrado.";
        });
    }

    private bool CanRename() => HasSelection() && !string.IsNullOrWhiteSpace(NameDraft);

    [RelayCommand(CanExecute = nameof(CanDelete))]
    private void Delete()
    {
        if (SelectedProfile is not { } selected
            || (_confirmDelete is not null && !_confirmDelete(selected.Name)))
        {
            return;
        }

        Execute(() =>
        {
            _profiles.Delete(selected.Id);
            var fallback = _profiles.List().First();
            Reload(fallback.Id);
            Select(_profiles.Load(fallback.Id));
            StatusMessage = "Perfil eliminado.";
        });
    }

    private bool HasSelection() => SelectedProfile is not null;

    private bool CanDelete() => SelectedProfile is not null && Profiles.Count > 1;

    partial void OnSelectedProfileChanged(ProfileMetadata? value)
    {
        DuplicateCommand.NotifyCanExecuteChanged();
        RenameCommand.NotifyCanExecuteChanged();
        DeleteCommand.NotifyCanExecuteChanged();

        if (_suspendSelection || value is null)
        {
            return;
        }

        Execute(() => Select(_profiles.Load(value.Id)));
    }

    partial void OnNameDraftChanged(string value)
    {
        CreateCommand.NotifyCanExecuteChanged();
        RenameCommand.NotifyCanExecuteChanged();
    }

    private void Select(Profile profile)
    {
        NameDraft = profile.Name;
        _onSelected(profile);
    }

    private void Reload(ProfileId selectedId)
    {
        _suspendSelection = true;
        Profiles.Clear();
        foreach (var profile in _profiles.List().OrderBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase))
        {
            Profiles.Add(profile);
        }

        SelectedProfile = Profiles.FirstOrDefault(p => p.Id == selectedId) ?? Profiles.FirstOrDefault();
        _suspendSelection = false;
        DeleteCommand.NotifyCanExecuteChanged();
    }

    private void Execute(Action action)
    {
        try
        {
            ErrorMessage = null;
            StatusMessage = null;
            action();
        }
        catch (ProfileException ex)
        {
            ErrorMessage = ex.Message;
        }
    }
}
