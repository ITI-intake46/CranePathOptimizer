using CraneOptimizer.UI.Core.Helpers;
using CraneOptimizer.UI.Features.F10_ProjectManagement.ViewModels;
using Microsoft.Win32;

namespace CraneOptimizer.UI.F10_ProjectManagement.Views
{
    public partial class EditProjectDialog : System.Windows.Window
    {
        private EditProjectViewModel? _vm;

        public EditProjectDialog()
        {
            InitializeComponent();
            MaximizeTaskbarHelper.Attach(this);
        }

        private void WireViewModel(EditProjectViewModel vm)
        {
            _vm = vm;
            DataContext = vm;

            vm.SaveSucceeded    += (_, _) => { DialogResult = true;  Close(); };
            vm.CancelRequested  += (_, _) => { DialogResult = false; Close(); };
            vm.BrowseImageRequested += (_, _) => BrowseImage();

            vm.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(EditProjectViewModel.ErrorText))
                    ErrorBorder.Visibility = string.IsNullOrWhiteSpace(vm.ErrorText)
                        ? System.Windows.Visibility.Collapsed
                        : System.Windows.Visibility.Visible;
            };
        }

        public static EditProjectDialog Create(EditProjectViewModel vm, System.Windows.Window owner)
        {
            var dlg = new EditProjectDialog { Owner = owner };
            dlg.WireViewModel(vm);
            return dlg;
        }

        private void BrowseImage()
        {
            var picker = new OpenFileDialog
            {
                Title  = "Select Project Image",
                Filter = "Image Files|*.png;*.jpg;*.jpeg;*.bmp;*.gif|All Files|*.*"
            };

            if (picker.ShowDialog(this) == true && _vm != null)
                _vm.ImagePath = picker.FileName;
        }
    }
}
