namespace SyncProLogViewer.ViewModels
{
    using System;
    using System.Windows.Input;

    public sealed class DelegatedCommand : ICommand
    {
        private readonly Predicate<object> canExecuteMethod;

        private readonly Action<object> executeMethod;

        public event EventHandler CanExecuteChanged
        {
            add
            {
                if (this.canExecuteMethod != null)
                {
                    CommandManager.RequerySuggested += value;
                }
            }

            remove
            {
                if (this.canExecuteMethod != null)
                {
                    CommandManager.RequerySuggested -= value;
                }
            }
        }

        public DelegatedCommand(Action<object> executeMethod)
            : this(executeMethod, null)
        {
        }

        public DelegatedCommand(Action<object> executeMethod, Predicate<object> canExecuteMethod)
        {
            if (executeMethod == null)
            {
                throw new ArgumentNullException(nameof(executeMethod));
            }

            this.executeMethod = executeMethod;
            this.canExecuteMethod = canExecuteMethod;
        }

        public bool CanExecute(object parameter)
        {
            return this.canExecuteMethod == null || this.canExecuteMethod(parameter);
        }

        public void Execute(object parameter)
        {
            this.executeMethod?.Invoke(parameter);
        }
    }
}