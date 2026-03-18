using System.Windows.Controls;
using System.Windows.Input;
using WPF_IFC_test1.ZoneModule.UI;

namespace WPF_IFC_test1
{
    public partial class MainWindow
    {
        #region Text Input Routing

        /// <summary>
        /// Routes printable keystrokes to the CommandLine input when
        /// no text control has focus.
        /// </summary>
        private void MainWindow_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            // Ignore if CmdLine input already has focus
            if (CmdLine == null || CmdLine.InputHasFocus()) 
                return;

            // Ignore if a text input control has focus
            var focused = Keyboard.FocusedElement;
            if (focused is TextBox || focused is ComboBox || focused is PasswordBox) 
                return;

            // Route character to CmdLine
            CmdLine.FocusInput();
        }

        #endregion

        #region Key Down Handler

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            // ESC: Cancel active tools first
            if (e.Key == Key.Escape)
            {
                if (_measureTool?.IsActive == true)
                {
                    _measureTool.Cancel();
                    e.Handled = true;
                    return;
                }
                if (_shapeCtrl?.IsActive == true)
                {
                    _shapeCtrl.Cancel();
                    e.Handled = true;
                    return;
                }
            }

            // SPACE: Confirm shape or focus CommandLine
            if (e.Key == Key.Space)
            {
                if (_shapeCtrl?.IsActive == true)
                {
                    _shapeCtrl.HandleSpaceBar();
                    e.Handled = true;
                    return;
                }

                if (CmdLine != null)
                {
                    CmdLine.SetMode(CommandMode.Idle);
                    CmdLine.SetPrompt("Type a command (UN=units, M=measure, ESC=close):");
                    CmdLine.FocusInput();
                    e.Handled = true;
                    return;
                }
            }

            // C: Close/finish Pick Lines polygon (when CmdLine does not have focus)
            if (e.Key == Key.C && _shapeCtrl?.IsPickObjectsActive == true && !CmdLine.InputHasFocus())
            {
                _shapeCtrl.FinishPickObjects();
                e.Handled = true;
                return;
            }

            // Enter: Also close Pick Lines polygon
            if (e.Key == Key.Return && _shapeCtrl?.IsPickObjectsActive == true)
            {
                _shapeCtrl.FinishPickObjects();
                e.Handled = true;
                return;
            }

            // Route to zone controller
            _zoneController?.HandleKeyDown(e);
            if (e.Handled) return;

            // F5: Reset view
            if (e.Key == Key.F5)
            {
                ResetToFitAll();
                e.Handled = true;
                return;
            }

            // G: Toggle grid (when not typing in command line)
            if (e.Key == Key.G && 
                (Keyboard.Modifiers & ModifierKeys.Control) == 0 &&
                !CmdLine.InputHasFocus())
            {
                _zoneController?.ToggleGrid();
                e.Handled = true;
                return;
            }

            // Ctrl+Z: Undo
            if (e.Key == Key.Z && (Keyboard.Modifiers & ModifierKeys.Control) != 0)
            {
                _zoneController?.HandleZoneAction("Undo");
                e.Handled = true;
                return;
            }

            // Ctrl+Y: Redo
            if (e.Key == Key.Y && (Keyboard.Modifiers & ModifierKeys.Control) != 0)
            {
                _zoneController?.HandleZoneAction("Redo");
                e.Handled = true;
                return;
            }
        }

        #endregion

        #region Dimension Tool

        private void BtnDimensionTool_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            CancelAllTools();
            _measureTool?.Start();
        }

        #endregion
    }
}
