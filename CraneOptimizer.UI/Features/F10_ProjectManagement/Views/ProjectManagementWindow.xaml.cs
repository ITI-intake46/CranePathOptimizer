using CraneOptimizer.UI.Core.Helpers;
using CraneOptimizer.UI.Features.F10_ProjectManagement.ViewModels;
using CraneOptimizer.UI.Resources;
using Microsoft.Win32;
using System.IO;
using System.Reflection;
using System.Windows;

namespace CraneOptimizer.UI.F10_ProjectManagement.Views
{
    public partial class ProjectManagementWindow : Window
    {
        private readonly ProjectManagementViewModel    _vm;
        private readonly CreateProjectViewModel        _createVm;
        private readonly ApplicationGuideViewModel     _guideVm;

        // ── Style keys ───────────────────────────────────────────────────
        private const string ActiveNav   = "SideNavBtnActive";
        private const string InactiveNav = "SideNavBtn";
        private const string ActiveTab   = "TopTabBtnActive";
        private const string InactiveTab = "TopTabBtn";

        public ProjectManagementWindow(ProjectManagementViewModel viewModel)
        {
            InitializeComponent();
            DataContext = _vm = viewModel;
            MaximizeTaskbarHelper.Attach(this, MaximizeButton);

            _createVm = new CreateProjectViewModel();
            CreatePanelControl.DataContext = _createVm;

            _guideVm = new ApplicationGuideViewModel();
            GuideCarouselControl.DataContext = _guideVm;

            _createVm.CancelRequested         += (_, _) => ShowHome();
            _createVm.BrowseLocationRequested  += (_, _) => BrowseProjectLocation();
            _createVm.CreateRequested          += async (_, _) => await ExecuteCreateAsync();

            Loaded += async (_, _) =>
            {
                await viewModel.InitializeAsync();
                PopulateSettingsLabels();
            };
        }

        // ════════════════════════════════════════════════════════════════
        //  Window chrome
        // ════════════════════════════════════════════════════════════════

        private void TitleBar_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2) ToggleMaximize();
            else DragMove();
        }

        private void MinimizeWindow_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
        private void MaximizeWindow_Click(object sender, RoutedEventArgs e) => ToggleMaximize();
        private void CloseWindow_Click(object sender, RoutedEventArgs e)    => Close();

        private void ToggleMaximize()
            => MaximizeTaskbarHelper.ToggleMaximize(this, MaximizeButton);

        // ════════════════════════════════════════════════════════════════
        //  Sidebar navigation
        // ════════════════════════════════════════════════════════════════

        private void NavHome_Click(object sender, RoutedEventArgs e)              => ShowHome();
        private void NavCreate_Click(object sender, RoutedEventArgs e)            => ShowCreate();
        private void NavProjectList_Click(object sender, RoutedEventArgs e)       => ShowProjectList();
        private void NavRecentProjects_Click(object sender, RoutedEventArgs e)    => ShowRecentProjects();
        private void NavOpen_Click(object sender, RoutedEventArgs e)              => _ = OpenFolderAsync();
        private void NavAppSettings_Click(object sender, RoutedEventArgs e)       => ShowSettings();
        private void NavAppGuide_Click(object sender, RoutedEventArgs e)          => ShowApplicationGuide();
        private void NavHelp_Click(object sender, RoutedEventArgs e)              => ShowHelp();
        private void NavAbout_Click(object sender, RoutedEventArgs e)             => ShowAbout();

        // ════════════════════════════════════════════════════════════════
        //  Top tabs (legacy — kept to avoid removing named elements)
        // ════════════════════════════════════════════════════════════════

        private void TabOverview_Click(object sender, RoutedEventArgs e)
        {
            TabOverview.Style = (Style)FindResource(ActiveTab);
            TabGuide.Style    = (Style)FindResource(InactiveTab);
            if (ContentGuide.Visibility == Visibility.Visible) ShowHome();
        }

        private void TabGuide_Click(object sender, RoutedEventArgs e)
        {
            TabGuide.Style    = (Style)FindResource(ActiveTab);
            TabOverview.Style = (Style)FindResource(InactiveTab);
            HideAllContent();
            ContentGuide.Visibility = Visibility.Visible;
            ResetNavStyles();
        }

        // ════════════════════════════════════════════════════════════════
        //  Content switch helpers
        // ════════════════════════════════════════════════════════════════

        private void ShowHome()
        {
            HideAllContent();
            ContentHome.Visibility = Visibility.Visible;
            ResetNavStyles();
            BtnNavHome.Style = (Style)FindResource(ActiveNav);
        }

        private void ShowCreate()
        {
            _createVm.ProjectName  = string.Empty;
            _createVm.SiteLocation = string.Empty;
            _createVm.ClearImageCommand.Execute(null);

            HideAllContent();
            ContentCreate.Visibility = Visibility.Visible;
            ResetNavStyles();
            BtnNavCreate.Style = (Style)FindResource(ActiveNav);
        }

        private void ShowProjectList()
        {
            HideAllContent();
            ContentProjectList.Visibility = Visibility.Visible;
            ResetNavStyles();
            BtnNavProjectList.Style = (Style)FindResource(ActiveNav);
        }

        private void ShowRecentProjects()
        {
            HideAllContent();
            ContentRecentProjects.Visibility = Visibility.Visible;
            ResetNavStyles();
            BtnNavRecentProjects.Style = (Style)FindResource(ActiveNav);
        }

        private void ShowSettings()
        {
            PopulateSettingsLabels();
            HideAllContent();
            ContentSettings.Visibility = Visibility.Visible;
            ResetNavStyles();
            BtnNavAppSettings.Style = (Style)FindResource(ActiveNav);
        }

        private void ShowApplicationGuide()
        {
            HideAllContent();
            ContentGuide.Visibility = Visibility.Visible;
            ResetNavStyles();
            BtnNavAppGuide.Style = (Style)FindResource(ActiveNav);
        }

        private void ShowHelp()
        {
            HideAllContent();
            ContentHelp.Visibility = Visibility.Visible;

            TxtHelpTitle.Text    = "Help";
            TxtHelpSubtitle.Text = "Frequently asked questions and support resources.";
            CardHelp.Visibility  = Visibility.Visible;
            CardAbout.Visibility = Visibility.Collapsed;

            ResetNavStyles();
            BtnNavHelp.Style = (Style)FindResource(ActiveNav);
        }

        private void ShowAbout()
        {
            HideAllContent();
            ContentHelp.Visibility = Visibility.Visible;

            TxtHelpTitle.Text    = "About";
            TxtHelpSubtitle.Text = "Information about this application.";
            CardHelp.Visibility  = Visibility.Collapsed;
            CardAbout.Visibility = Visibility.Visible;

            var ver = Assembly.GetEntryAssembly()?.GetName().Version;
            TxtAboutVersion.Text = ver != null ? $"Version {ver}" : "Version N/A";

            ResetNavStyles();
            BtnNavAbout.Style = (Style)FindResource(ActiveNav);
        }

        private void HideAllContent()
        {
            ContentHome.Visibility           = Visibility.Collapsed;
            ContentCreate.Visibility         = Visibility.Collapsed;
            ContentProjectList.Visibility    = Visibility.Collapsed;
            ContentRecentProjects.Visibility = Visibility.Collapsed;
            ContentGuide.Visibility          = Visibility.Collapsed;
            ContentSettings.Visibility       = Visibility.Collapsed;
            ContentHelp.Visibility           = Visibility.Collapsed;
        }

        private void ResetNavStyles()
        {
            BtnNavHome.Style           = (Style)FindResource(InactiveNav);
            BtnNavCreate.Style         = (Style)FindResource(InactiveNav);
            BtnNavProjectList.Style    = (Style)FindResource(InactiveNav);
            BtnNavRecentProjects.Style = (Style)FindResource(InactiveNav);
            BtnNavOpen.Style           = (Style)FindResource(InactiveNav);
            BtnNavAppSettings.Style    = (Style)FindResource(InactiveNav);
            BtnNavAppGuide.Style       = (Style)FindResource(InactiveNav);
            BtnNavHelp.Style           = (Style)FindResource(InactiveNav);
            BtnNavAbout.Style          = (Style)FindResource(InactiveNav);
        }

        // ════════════════════════════════════════════════════════════════
        //  Theme buttons
        // ════════════════════════════════════════════════════════════════

        private void BtnThemeDark_Click(object sender, RoutedEventArgs e)
        {
            ThemeService.Instance.ApplyTheme(AppTheme.Dark);
            UpdateThemeButtons();
            TxtCurrentTheme.Text = "Dark";
        }

        private void BtnThemeLight_Click(object sender, RoutedEventArgs e)
        {
            ThemeService.Instance.ApplyTheme(AppTheme.Light);
            UpdateThemeButtons();
            TxtCurrentTheme.Text = "Light";
        }

        private void UpdateThemeButtons()
        {
            bool isDark = ThemeService.Instance.CurrentTheme == AppTheme.Dark;
            BtnThemeDark.Style  = (Style)FindResource(isDark ? "ButtonPrimary"   : "ButtonSecondary");
            BtnThemeLight.Style = (Style)FindResource(isDark ? "ButtonSecondary" : "ButtonPrimary");
        }

        private void PopulateSettingsLabels()
        {
            var ver = Assembly.GetEntryAssembly()?.GetName().Version;
            TxtAppVersion.Text   = ver != null ? ver.ToString() : "N/A";
            TxtCurrentTheme.Text = ThemeService.Instance.CurrentTheme.ToString();
            UpdateThemeButtons();
        }

        // ════════════════════════════════════════════════════════════════
        //  Create project via embedded panel
        // ════════════════════════════════════════════════════════════════

        private async Task ExecuteCreateAsync()
        {
            await _vm.CreateWithPanelAsync(_createVm, this);
        }

        // ════════════════════════════════════════════════════════════════
        //  Browse folder
        // ════════════════════════════════════════════════════════════════

        private void BrowseProjectLocation()
        {
            var picker = new OpenFolderDialog
            {
                Title = "Select Project Location",
                Multiselect = false
            };

            if (!string.IsNullOrWhiteSpace(_createVm.ParentFolderPath)
                && Directory.Exists(_createVm.ParentFolderPath))
            {
                picker.InitialDirectory = _createVm.ParentFolderPath;
            }

            if (picker.ShowDialog(this) == true)
                _createVm.ParentFolderPath = picker.FolderName;
        }

        private async Task OpenFolderAsync()
        {
            var picker = new OpenFolderDialog
            {
                Title = "Open Project Folder",
                Multiselect = false
            };

            if (picker.ShowDialog(this) != true) return;

            try
            {
                var session = await _vm.OpenFolderDirectAsync(picker.FolderName);
                if (session != null) Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Open Folder Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        // ════════════════════════════════════════════════════════════════
        //  Project details / edit  (... button in Project List)
        // ════════════════════════════════════════════════════════════════

        private async void ProjectDetails_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not System.Windows.Controls.Button btn) return;
            if (btn.Tag is not ProjectCardViewModel card) return;

            var editVm = new EditProjectViewModel(_vm.WorkspaceService, card);

            // When the folder is renamed, sync the active session path
            editVm.FolderRenamed += async (_, newPath) =>
                await _vm.OnProjectFolderRenamedAsync(card.Id, newPath);

            var dlg = EditProjectDialog.Create(editVm, this);

            if (dlg.ShowDialog() == true)
            {
                // Refresh the projects list so Name / CompanyName / path update everywhere
                await _vm.RefreshAfterEditAsync();
            }
        }

    }
}
