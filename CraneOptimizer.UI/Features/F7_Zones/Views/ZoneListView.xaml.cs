using CraneOptimizer.UI.Features.F7_Zones.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace CraneOptimizer.UI.Features.F7_Zones.Views
{
    public partial class ZoneListView : UserControl
    {
        public ZoneListView()
        {
            InitializeComponent();
        }

        private void ZoneName_MouseLeftButtonDown(
            object sender,
            MouseButtonEventArgs e)
        {
            if (e.ClickCount != 2 ||
                sender is not FrameworkElement element ||
                element.DataContext is not ZoneItemViewModel item)
            {
                return;
            }

            ZoneList.SelectedItem = item;
            item.BeginNameEdit();
            e.Handled = true;

            _ = Dispatcher.BeginInvoke(
                new Action(() => FocusNameEditor(item)),
                DispatcherPriority.Input);
        }

        private void NameEditor_KeyDown(object sender, KeyEventArgs e)
        {
            if (sender is not TextBox editor ||
                editor.DataContext is not ZoneItemViewModel item)
            {
                return;
            }

            if (e.Key == Key.Enter)
            {
                CommitNameEdit(item);
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                item.CancelNameEdit();
                FocusZoneRow(item);
                e.Handled = true;
            }
        }

        private void NameEditor_LostKeyboardFocus(
            object sender,
            KeyboardFocusChangedEventArgs e)
        {
            if (sender is TextBox editor &&
                editor.DataContext is ZoneItemViewModel item &&
                item.IsEditingName)
            {
                CommitNameEdit(item);
            }
        }

        private void CommitNameEdit(ZoneItemViewModel item)
        {
            if (!item.IsEditingName)
                return;

            if (item.HasNameEditChanged &&
                DataContext is ZoneListViewModel viewModel &&
                viewModel.RenameZoneCommand.CanExecute(item))
            {
                viewModel.RenameZoneCommand.Execute(item);
            }

            item.CompleteNameEdit();
        }

        private void FocusNameEditor(ZoneItemViewModel item)
        {
            if (ZoneList.ItemContainerGenerator.ContainerFromItem(item)
                    is not ListViewItem container)
            {
                return;
            }

            TextBox? editor = FindVisualChild<TextBox>(container, "NameEditor");
            if (editor == null)
                return;

            editor.Focus();
            editor.SelectAll();
        }

        private void FocusZoneRow(ZoneItemViewModel item)
        {
            if (ZoneList.ItemContainerGenerator.ContainerFromItem(item)
                    is ListViewItem container)
            {
                container.Focus();
                Keyboard.Focus(container);
            }
            else
            {
                ZoneList.Focus();
            }
        }

        private static T? FindVisualChild<T>(
            DependencyObject parent,
            string name)
            where T : FrameworkElement
        {
            int childCount = VisualTreeHelper.GetChildrenCount(parent);
            for (int index = 0; index < childCount; index++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(parent, index);
                if (child is T match && match.Name == name)
                    return match;

                T? descendant = FindVisualChild<T>(child, name);
                if (descendant != null)
                    return descendant;
            }

            return null;
        }
    }
}
