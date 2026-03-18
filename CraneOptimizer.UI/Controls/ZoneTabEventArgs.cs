namespace WPF_IFC_test1.Main_Application_UserControl
{
    public class ZoneTabEventArgs
    {
        public string Action { get; set; }
        public object Parameter { get; set; }

        public ZoneTabEventArgs(string action, object parameter = null)
        {
            Action = action;
            Parameter = parameter;
        }
    }
}
