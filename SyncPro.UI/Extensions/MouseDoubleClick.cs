namespace SyncPro.UI.Extensions
{
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Input;

    public static class MouseDoubleClick
    {
        // Sources from http://stackoverflow.com/questions/13867667/mvvm-binding-double-click-to-method-using-telerik-radtreecontrol
        public static DependencyProperty CommandProperty =
        DependencyProperty.RegisterAttached("Command",
            typeof(ICommand),
            typeof(MouseDoubleClick),
            new UIPropertyMetadata(CommandChanged));

        public static DependencyProperty CommandParameterProperty =
            DependencyProperty.RegisterAttached("CommandParameter",
                typeof(object),
                typeof(MouseDoubleClick),
                new UIPropertyMetadata(null));

        public static void SetCommand(DependencyObject target, ICommand value)
        {
            target.SetValue(CommandProperty, value);
        }

        public static void SetCommandParameter(DependencyObject target, object value)
        {
            target.SetValue(CommandParameterProperty, value);
        }

        public static object GetCommandParameter(DependencyObject target)
        {
            return target.GetValue(CommandParameterProperty);
        }

        private static void CommandChanged(DependencyObject target, DependencyPropertyChangedEventArgs e)
        {
            Control control = target as Control;

            if (control == null)
            {
                return;
            }

            if ((e.NewValue != null) && (e.OldValue == null))
            {
                control.MouseDoubleClick += OnMouseDoubleClick;
            }
            else if ((e.NewValue == null) && (e.OldValue != null))
            {
                control.MouseDoubleClick -= OnMouseDoubleClick;
            }
        }

        private static void OnMouseDoubleClick(object sender, RoutedEventArgs e)
        {
            Control control = sender as Control;

            if (control == null)
            {
                return;
            }

            ICommand command = (ICommand) control.GetValue(CommandProperty);
            object commandParameter = control.GetValue(CommandParameterProperty);
            if (command.CanExecute(commandParameter))
            {
                command.Execute(commandParameter);
            }
        }
    }
}