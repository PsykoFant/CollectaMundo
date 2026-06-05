using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace CollectaMundo.ViewModels.Shared
{
    public abstract partial class LocationManagementViewModel<TItem> : ObservableObject where TItem : class
    {
        // Tracks the current editor workflow state.
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

        // Core editor state
        protected SelectionEditorMode EditorMode
        {
            get => editorMode;
            private set
            {
                editorMode = value;
                RefreshEditorState();
            }
        }

        // Selection state
        public ObservableCollection<TItem> SelectedItems { get; } = [];
        public bool HasSelectedItems => SelectedItems.Count > 0;
        public bool IsCancelVisible => IsDeleteConfirmationActive || EditorMode is SelectionEditorMode.EditSingle or SelectionEditorMode.EditMultiple;

        // Editor enablement
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

        // UI text
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

        // Customizable UI text
        protected virtual string CreateButtonText => "Add";
        protected virtual string EditButtonText => "Edit";
        protected virtual string SaveButtonText => "Save changes";
        protected virtual string BulkUpdateButtonText => "Update selected";

        protected virtual string CreateModeMessage => "Add new item";
        protected virtual string SelectedReadOnlyModeMessage => string.Empty;
        protected virtual string EditSingleModeMessage => "Edit selected item";
        protected virtual string EditMultipleModeMessage => "Edit selected items";
        protected virtual string DeleteConfirmationMessage => "Confirm delete";
        protected virtual string SubmitFailureMessage => "Failed to submit changes";

        // Mode transition hooks
        protected virtual void OnEnterCreateMode() { }
        protected virtual void OnEnterSelectedReadOnlyMode(TItem item)
        {
            LoadEditorFromItem(item);
        }
        protected virtual void OnEnterEditSingleMode(TItem item)
        {
            LoadEditorFromItem(item);
        }
        protected abstract void LoadEditorFromItem(TItem item);
        protected virtual void OnEnterEditMultipleMode(IReadOnlyList<TItem> selectedItems) { }
        protected virtual void ClearEditorFields() { }

        // Domain operations supplied by derived view models
        protected abstract Task CreateAsync();
        protected abstract Task UpdateSingleAsync(TItem selectedItem);
        protected abstract Task UpdateMultipleAsync(IReadOnlyList<TItem> selectedItems);

        // ObservableProperty callbacks
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
        }
        partial void OnIsDeleteConfirmationActiveChanged(bool value)
        {
            RefreshEditorState();
        }
        partial void OnIsBusyChanged(bool value)
        {
            RefreshEditorState();
        }

        // Commands
        [RelayCommand]
        protected virtual void BeginEditSelectedItem()
        {
            if (SelectedItem is null || SelectedItems.Count > 1)
            {
                return;
            }

            EditorMode = SelectionEditorMode.EditSingle;
            OnEnterEditSingleMode(SelectedItem);
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

        // Shared workflow helpers

        protected async Task RunBusyOperationAsync(Func<Task> operation, string failureMessage)
        {
            if (IsBusy)
            {
                return;
            }

            try
            {
                IsBusy = true;
                ClearStatus();

                await operation();
            }
            catch (Exception ex)
            {
                ShowStatus($"{failureMessage}: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }
        protected async Task DeleteSelectedItemsAsync(string confirmationMessage, Func<IReadOnlyList<TItem>, Task<bool>> deleteOperation)
        {
            if (IsBusy || SelectedItems.Count == 0)
            {
                return;
            }

            if (!IsDeleteConfirmationActive)
            {
                IsDeleteConfirmationActive = true;
                ShowStatus(confirmationMessage);
                return;
            }

            try
            {
                IsBusy = true;
                ClearStatus();

                var selectedItems = SelectedItems.ToList();

                bool success = await deleteOperation(selectedItems);

                if (success)
                {
                    IsDeleteConfirmationActive = false;
                    ResetEditorAndSelection();
                }
            }
            catch (Exception ex)
            {
                ShowStatus($"Failed to delete selected items: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }
        protected void SetCreateMode()
        {
            EditorMode = SelectionEditorMode.Create;
            ClearEditorFields();
            OnEnterCreateMode();
        }
        protected void ResetEditorAndSelection()
        {
            SelectedItem = null;
            SelectedItems.Clear();

            ClearEditorFields();

            EditorMode = SelectionEditorMode.Create;
            ClearSelectionTrigger++;

            OnEnterCreateMode();
        }
        protected void RefreshSelectionMode()
        {
            IsDeleteConfirmationActive = false;

            if (SelectedItems.Count > 1)
            {
                EditorMode = SelectionEditorMode.EditMultiple;
                ClearEditorFields();
                OnEnterEditMultipleMode(SelectedItems.ToList());
                return;
            }

            if (SelectedItem is not null)
            {
                EditorMode = SelectionEditorMode.SelectedReadOnly;
                OnEnterSelectedReadOnlyMode(SelectedItem);
                return;
            }

            SetCreateMode();
        }

        // Status and UI refresh helpers
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

