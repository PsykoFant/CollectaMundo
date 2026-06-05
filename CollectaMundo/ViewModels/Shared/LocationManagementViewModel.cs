using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace CollectaMundo.ViewModels.Shared
{
    public abstract partial class LocationManagementViewModel<TItem> : ObservableObject where TItem : class
    {
        private SelectionEditorMode editorMode;
        protected LocationManagementViewModel()
        {
            SelectedItems.CollectionChanged += (_, _) =>
            {
                IsDeleteConfirmationActive = false;
                OnPropertyChanged(nameof(HasSelectedItems));
                RefreshSelectionMode();
            };

            SetCreateMode();
        }
        protected SelectionEditorMode EditorMode
        {
            get => editorMode;
            private set
            {
                editorMode = value;
                RefreshEditorState();
            }
        }

        public ObservableCollection<TItem> SelectedItems { get; } = [];
        public bool HasSelectedItems => SelectedItems.Count > 0;
        public bool IsCancelVisible => IsDeleteConfirmationActive || EditorMode is SelectionEditorMode.EditSingle or SelectionEditorMode.EditMultiple;
        public bool IsActionButtonEnabled =>
            !IsBusy &&
            !IsDeleteConfirmationActive &&
            EditorMode is SelectionEditorMode.Create
                or SelectionEditorMode.SelectedReadOnly
                or SelectionEditorMode.EditSingle
                or SelectionEditorMode.EditMultiple;
        public bool IsSingleItemTextEditorEnabled =>
            !IsBusy &&
            !IsDeleteConfirmationActive &&
            EditorMode is SelectionEditorMode.Create or SelectionEditorMode.EditSingle;
        public bool IsDiscreteValueEditorEnabled =>
            !IsBusy &&
            !IsDeleteConfirmationActive &&
            EditorMode is SelectionEditorMode.Create
                or SelectionEditorMode.EditSingle
                or SelectionEditorMode.EditMultiple;
        public string ActionButtonText => EditorMode switch
        {
            SelectionEditorMode.Create => CreateButtonText,
            SelectionEditorMode.SelectedReadOnly => EditButtonText,
            SelectionEditorMode.EditSingle => SaveButtonText,
            SelectionEditorMode.EditMultiple => BulkUpdateButtonText,
            _ => "Submit"
        };
        public string ModeMessage => IsDeleteConfirmationActive
            ? DeleteConfirmationMessage
            : EditorMode switch
            {
                SelectionEditorMode.Create => CreateModeMessage,
                SelectionEditorMode.SelectedReadOnly => SelectedReadOnlyModeMessage,
                SelectionEditorMode.EditSingle => EditSingleModeMessage,
                SelectionEditorMode.EditMultiple => EditMultipleModeMessage,
                _ => string.Empty
            };
        public string DeleteButtonText => IsDeleteConfirmationActive ? "Yes, delete!" : "Delete selected";

        [ObservableProperty]
        private string statusMessage = string.Empty;

        [ObservableProperty]
        private bool isStatusVisible;

        [ObservableProperty]
        private bool isBusy;

        [ObservableProperty]
        private bool isDeleteConfirmationActive;

        [ObservableProperty]
        private TItem? selectedItem;

        [ObservableProperty]
        private int clearSelectionTrigger;
        protected virtual string CreateButtonText => "Add";
        protected virtual string EditButtonText => "Edit";
        protected virtual string SaveButtonText => "Save changes";
        protected virtual string BulkUpdateButtonText => "Update selected";

        protected virtual string CreateModeMessage => "Add new item";
        protected virtual string SelectedReadOnlyModeMessage => string.Empty;
        protected virtual string EditSingleModeMessage => "Edit selected item";
        protected virtual string EditMultipleModeMessage => "Edit selected items";
        protected virtual string DeleteConfirmationMessage => "Confirm delete";

        protected virtual void OnEnterCreateMode() { }
        protected virtual void OnEnterSelectedReadOnlyMode(TItem selectedItem) { }
        protected virtual void OnEnterEditSingleMode(TItem selectedItem) { }
        protected virtual void OnEnterEditMultipleMode(IReadOnlyList<TItem> selectedItems) { }
        protected virtual void ClearEditorFields() { }
        partial void OnSelectedItemChanged(TItem? value)
        {
            IsDeleteConfirmationActive = false;

            if (value is null)
            {
                if (SelectedItems.Count == 0)
                {
                    SetCreateMode();
                }

                return;
            }

            EditorMode = SelectionEditorMode.SelectedReadOnly;
            OnEnterSelectedReadOnlyMode(value);
            RefreshEditorState();
        }
        partial void OnIsDeleteConfirmationActiveChanged(bool value)
        {
            RefreshEditorState();
        }
        partial void OnIsBusyChanged(bool value)
        {
            RefreshEditorState();
        }

        [RelayCommand]
        protected virtual void BeginEditSelectedItem()
        {
            if (SelectedItem is null || SelectedItems.Count > 1)
            {
                return;
            }

            EditorMode = SelectionEditorMode.EditSingle;
            OnEnterEditSingleMode(SelectedItem);
            RefreshEditorState();
        }

        [RelayCommand]
        private async Task Submit()
        {
            if (IsBusy)
            {
                return;
            }

            try
            {
                IsBusy = true;
                ClearStatus();

                if (EditorMode is SelectionEditorMode.SelectedReadOnly)
                {
                    BeginEditSelectedItemCommand.Execute(null);
                    return;
                }

                if (EditorMode is SelectionEditorMode.EditSingle && SelectedItem is not null)
                {
                    await UpdateSingleAsync(SelectedItem);
                    return;
                }

                if (EditorMode is SelectionEditorMode.EditMultiple)
                {
                    await UpdateMultipleAsync(SelectedItems.ToList());
                    return;
                }

                await CreateAsync();
            }
            catch (Exception ex)
            {
                ShowStatus($"{SubmitFailureMessage}: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        protected virtual string SubmitFailureMessage => "Failed to submit changes";

        protected abstract Task CreateAsync();

        protected abstract Task UpdateSingleAsync(TItem selectedItem);

        protected abstract Task UpdateMultipleAsync(IReadOnlyList<TItem> selectedItems);

        [RelayCommand]
        protected void CancelEdit()
        {
            IsDeleteConfirmationActive = false;
            ResetEditorAndSelection();
            ClearStatus();
        }

        [RelayCommand]
        protected void ClearSelectionAndRestoreCreateMode()
        {
            if (EditorMode is not SelectionEditorMode.SelectedReadOnly)
            {
                return;
            }

            ResetEditorAndSelection();
        }

        [RelayCommand]
        protected void CancelOrClearSelection()
        {
            if (EditorMode is not SelectionEditorMode.Create)
            {
                ResetEditorAndSelection();
            }
        }
        protected void SetCreateMode()
        {
            EditorMode = SelectionEditorMode.Create;
            ClearEditorFields();
            OnEnterCreateMode();
            RefreshEditorState();
        }
        protected void ResetEditorAndSelection()
        {
            SelectedItem = null;
            SelectedItems.Clear();

            ClearEditorFields();

            EditorMode = SelectionEditorMode.Create;
            ClearSelectionTrigger++;

            OnEnterCreateMode();
            RefreshEditorState();
        }
        protected void RefreshSelectionMode()
        {
            IsDeleteConfirmationActive = false;

            if (SelectedItems.Count > 1)
            {
                EditorMode = SelectionEditorMode.EditMultiple;
                ClearEditorFields();
                OnEnterEditMultipleMode(SelectedItems.ToList());
                RefreshEditorState();
                return;
            }

            if (SelectedItem is not null)
            {
                EditorMode = SelectionEditorMode.SelectedReadOnly;
                OnEnterSelectedReadOnlyMode(SelectedItem);
                RefreshEditorState();
                return;
            }

            SetCreateMode();
        }
        protected void ClearStatus()
        {
            StatusMessage = string.Empty;
            IsStatusVisible = false;
        }
        protected void ShowStatus(string message)
        {
            StatusMessage = message;
            IsStatusVisible = !string.IsNullOrWhiteSpace(message);
        }
        protected void RefreshEditorState()
        {
            OnPropertyChanged(nameof(HasSelectedItems));
            OnPropertyChanged(nameof(IsCancelVisible));
            OnPropertyChanged(nameof(IsActionButtonEnabled));
            OnPropertyChanged(nameof(IsSingleItemTextEditorEnabled));
            OnPropertyChanged(nameof(IsDiscreteValueEditorEnabled));
            OnPropertyChanged(nameof(ActionButtonText));
            OnPropertyChanged(nameof(ModeMessage));
            OnPropertyChanged(nameof(DeleteButtonText));
        }
        protected enum SelectionEditorMode
        {
            Create,
            SelectedReadOnly,
            EditSingle,
            EditMultiple
        }
    }
}

