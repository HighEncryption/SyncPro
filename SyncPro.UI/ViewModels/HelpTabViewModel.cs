namespace SyncPro.UI.ViewModels
{
    public class HelpTabViewModel : TabPageViewModelBase
    {
        public HelpTabViewModel(ITabControlHostViewModel tabControlHost)
            : base(tabControlHost)
        {
        }

        public override string NavTitle => "Help";

        public override string PageTitle => "Help and Information";

        public override string TabItemImageSource => "/SyncPro.UI;component/Resources/Graphics/help_20.png";

        public override string PageSubText => "This is where the text would go that talks about what goes on this page.";


        public override void LoadContext()
        {
        }

        public override void SaveContext()
        {
        }
    }
}