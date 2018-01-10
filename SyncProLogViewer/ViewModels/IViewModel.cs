namespace SyncProLogViewer.ViewModels
{
    using System.ComponentModel;

    public interface IViewModel : INotifyPropertyChanged, INotifyPropertyChanging
    {
    }

    public interface IViewModel<TContext> : INotifyPropertyChanged, INotifyPropertyChanging
    {
    }
}