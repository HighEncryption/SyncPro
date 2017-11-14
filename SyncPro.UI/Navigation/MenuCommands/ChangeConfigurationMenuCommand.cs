namespace SyncPro.UI.Navigation.MenuCommands
{
    using SyncPro.UI.ViewModels;

    public class ChangeConfigurationMenuCommand : NavigationItemMenuCommand
    {
        private readonly SyncRelationshipViewModel relationship;

        public ChangeConfigurationMenuCommand(SyncRelationshipViewModel relationship)
            //: base("CHANGE CONFIGURATION", "/SyncPro.UI;component/Resources/Graphics/tools_16.png")
            : base("CHANGE", "/SyncPro.UI;component/Resources/Graphics/tools_16.png")
        {
            this.relationship = relationship;
            this.ToolTip = "Change the configuration of this sync relationship";
        }

        protected override bool CanInvokeCommand(object obj)
        {
            return this.relationship.EditRelationshipCommand.CanExecute(obj);
        }

        protected override void InvokeCommand(object obj)
        {
            this.relationship.EditRelationshipCommand.Execute(obj);
        }
    }
}