using CraneOptimizer.Domain.Units;
using CraneOptimizer.UI.Core.Base;
using CraneOptimizer.UI.Core.Commands;
using CraneOptimizer.UI.Core.Interfaces;
using CraneOptimizer.UI.F10_ProjectManagement.Views;
using CraneOptimizer.UI.Features.F10_ProjectManagement.Models;
using CraneOptimizer.UI.Features.F10_ProjectManagement.Services;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using Microsoft.Win32;

namespace CraneOptimizer.UI.Features.F10_ProjectManagement.ViewModels
{
    public sealed class ProjectManagementViewModel : ViewModelBase
    {
        private readonly IProjectWorkspaceService _workspaceService;
        private readonly IProjectNavigationService _navigationService;
        private readonly IProjectShellService _shellService;
        private readonly IDialogService _dialogService;
        private readonly CancellationTokenSource _lifetime = new();
        private string _searchText = string.Empty;
        private bool _favoritesOnly;
        private bool _isBusy;
        private string _statusText = "Select a project to continue";

        public ProjectManagementViewModel(
            IProjectWorkspaceService workspaceService,
            IProjectNavigationService navigationService,
            IProjectShellService shellService,
            IDialogService dialogService)
        {
            _workspaceService = workspaceService;
            _navigationService = navigationService;
            _shellService = shellService;
            _dialogService = dialogService;

            RefreshCommand = new AsyncRelayCommand(RefreshAsync);
            CreateProjectCommand = new AsyncRelayCommand(CreateProjectAsync);
            OpenProjectCommand = new AsyncRelayCommand<ProjectCardViewModel>(OpenProjectAsync, project => project != null);
            DeleteProjectCommand = new AsyncRelayCommand<ProjectCardViewModel>(DeleteProjectAsync, project => project != null);
            ToggleFavoriteCommand = new AsyncRelayCommand<ProjectCardViewModel>(ToggleFavoriteAsync, project => project != null);
            ShowInFolderCommand = new RelayCommand<ProjectCardViewModel>(ShowInFolder, project => project != null);
        }

        public ObservableCollection<ProjectCardViewModel> Projects { get; } = new();

        public string SearchText
        {
            get => _searchText;
            set
            {
                if (_searchText == value)
                    return;

                _searchText = value;
                OnPropertyChanged();
                _ = SearchAsync();
            }
        }

        public bool FavoritesOnly
        {
            get => _favoritesOnly;
            set
            {
                if (_favoritesOnly == value)
                    return;

                _favoritesOnly = value;
                OnPropertyChanged();
                _ = SearchAsync();
            }
        }

        public bool IsBusy
        {
            get => _isBusy;
            private set
            {
                if (_isBusy == value)
                    return;

                _isBusy = value;
                OnPropertyChanged();
            }
        }

        public string StatusText
        {
            get => _statusText;
            private set
            {
                if (_statusText == value)
                    return;

                _statusText = value;
                OnPropertyChanged();
            }
        }

        public ICommand RefreshCommand { get; }
        public ICommand CreateProjectCommand { get; }
        public ICommand OpenProjectCommand { get; }
        public ICommand DeleteProjectCommand { get; }
        public ICommand ToggleFavoriteCommand { get; }
        public ICommand ShowInFolderCommand { get; }

        public Task InitializeAsync()
        {
            return RefreshAsync();
        }

        private async Task RefreshAsync()
        {
            await LoadProjectsAsync(() => _workspaceService.GetRecentProjectsAsync(_lifetime.Token));
        }

        private async Task SearchAsync()
        {
            await LoadProjectsAsync(() => _workspaceService.SearchProjectsAsync(SearchText, FavoritesOnly, _lifetime.Token));
        }

        private async Task LoadProjectsAsync(Func<Task<IReadOnlyList<ProjectCardModel>>> loader)
        {
            try
            {
                IsBusy = true;
                var projects = await loader();

                Projects.Clear();
                foreach (var project in projects)
                    Projects.Add(new ProjectCardViewModel(project));

                StatusText = Projects.Count == 0
                    ? "No projects found"
                    : $"{Projects.Count} project(s)";
            }
            catch (Exception ex)
            {
                StatusText = "Could not load projects";
                _dialogService.ShowNonBlockingError(ex.Message, "Project Database Error");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task CreateProjectAsync()
        {
            var createViewModel = new CreateProjectViewModel();
            var dialog = new CreateProjectDialog
            {
                DataContext = createViewModel,
                Owner = System.Windows.Application.Current.MainWindow
            };

            createViewModel.CancelRequested += (_, _) => dialog.DialogResult = false;
            createViewModel.BrowseLocationRequested += (_, _) => BrowseProjectLocation(createViewModel, dialog);
            createViewModel.CreateRequested += (_, _) => dialog.DialogResult = true;

            if (dialog.ShowDialog() != true)
                return;

            try
            {
                IsBusy = true;
                var session = await _workspaceService.CreateProjectAsync(
                    new CreateProjectRequest
                    {
                        ProjectName = createViewModel.ProjectName,
                        ParentFolderPath = createViewModel.ParentFolderPath,
                        UnitSettings = createViewModel.BuildUnitSettings()
                    },
                    _lifetime.Token);

                _navigationService.OpenMainWindow(session);
                CloseEntryWindow();
            }
            catch (Exception ex)
            {
                _dialogService.ShowNonBlockingError(ex.Message, "Create Project Error");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task OpenProjectAsync(ProjectCardViewModel? card)
        {
            if (card == null)
                return;

            try
            {
                IsBusy = true;
                var session = await _workspaceService.OpenProjectAsync(card.Id, _lifetime.Token);

                if (session == null)
                {
                    _dialogService.ShowMessage("Project was not found.");
                    await RefreshAsync();
                    return;
                }

                _navigationService.OpenMainWindow(session);
                CloseEntryWindow();
            }
            catch (Exception ex)
            {
                _dialogService.ShowNonBlockingError(ex.Message, "Open Project Error");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task DeleteProjectAsync(ProjectCardViewModel? card)
        {
            if (card == null)
                return;

            if (!_dialogService.Confirm($"Delete project '{card.Name}'?", "Delete Project"))
                return;

            try
            {
                await _workspaceService.DeleteProjectAsync(card.Id, _lifetime.Token);
                Projects.Remove(card);
                StatusText = $"{Projects.Count} project(s)";
            }
            catch (Exception ex)
            {
                _dialogService.ShowNonBlockingError(ex.Message, "Delete Project Error");
            }
        }

        private async Task ToggleFavoriteAsync(ProjectCardViewModel? card)
        {
            if (card == null)
                return;

            try
            {
                await _workspaceService.ToggleFavoriteAsync(card.Id, card.IsFavorite, _lifetime.Token);

                if (FavoritesOnly && !card.IsFavorite)
                    Projects.Remove(card);
            }
            catch (Exception ex)
            {
                card.IsFavorite = !card.IsFavorite;
                _dialogService.ShowNonBlockingError(ex.Message, "Favorite Error");
            }
        }

        private void ShowInFolder(ProjectCardViewModel? card)
        {
            if (card == null)
                return;

            if (string.IsNullOrWhiteSpace(card.ProjectFolderPath) || !Directory.Exists(card.ProjectFolderPath))
            {
                _dialogService.ShowMessage("Project folder was not found.");
                return;
            }

            _shellService.ShowFileInFolder(card.ProjectFolderPath);
        }

        private static void BrowseProjectLocation(
            CreateProjectViewModel createViewModel,
            System.Windows.Window owner)
        {
            var picker = new OpenFolderDialog
            {
                Title = "Select Project Location",
                Multiselect = false
            };

            if (!string.IsNullOrWhiteSpace(createViewModel.ParentFolderPath)
                && Directory.Exists(createViewModel.ParentFolderPath))
            {
                picker.InitialDirectory = createViewModel.ParentFolderPath;
            }

            if (picker.ShowDialog(owner) == true)
                createViewModel.ParentFolderPath = picker.FolderName;
        }

        private static void CloseEntryWindow()
        {
            foreach (System.Windows.Window window in System.Windows.Application.Current.Windows)
            {
                if (window is ProjectManagementWindow)
                {
                    window.Close();
                    return;
                }
            }
        }
    }
}
