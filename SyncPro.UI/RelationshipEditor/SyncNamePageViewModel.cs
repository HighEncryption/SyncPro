namespace SyncPro.UI.RelationshipEditor
{
    using System;
    using System.Diagnostics;
    using System.Linq;
    using System.Windows.Input;

    using SyncPro.UI.Framework.MVVM;
    using SyncPro.UI.Framework.Validation.Rules;
    using SyncPro.UI.ViewModels;
    using SyncPro.UI.ViewModels.Adapters;

    public class SyncNamePageViewModel : TabPageViewModelBase
    {
        public ICommand CreateRelationshipCommand { get; }

        public SyncNamePageViewModel(RelationshipEditorViewModel editorViewModel)
            : base(editorViewModel)
        {
            this.CreateRelationshipCommand = new DelegatedCommand(this.CreateRelationship);
        }

        private void CreateRelationship(object obj)
        {
            this.EditorViewModel.SaveOnExit = true;
            this.EditorViewModel.CloseWindowCommand.Execute(null);
        }

        public override string TabItemImageSource => "/SyncPro.UI;component/Resources/Graphics/rename_20.png";

        public override string PageSubText
            => "Provide a name and (optionally) a description for your sync relationship.";

        public override void LoadContext()
        {
            this.Name = this.EditorViewModel.Relationship.Name;
            this.Description = this.EditorViewModel.Relationship.Description;
        }

        public override void SaveContext()
        {
            this.EditorViewModel.Relationship.Name = this.Name;
            this.EditorViewModel.Relationship.Description = this.Description;
        }

        public override string NavTitle => "Name";

        public override bool IsLastPage => true;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string name;

        [StringNotNullorEmptyValidationRule(WaitForInitialValidation = true)]
        public string Name
        {
            get { return this.name; }
            set { this.SetProperty(ref this.name, value); }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string description;

        public string Description
        {
            get { return this.description; }
            set { this.SetProperty(ref this.description, value); }
        }

        public void ComputeDefaultName()
        {
            if (this.EditorViewModel.IsEditMode)
            {
                return;
            }

            ISyncTargetViewModel sourceAdapter = this.EditorViewModel.SyncSourcePageViewModel.SelectedSyncAdapter;
            if (sourceAdapter == null)
            {
                return;
            }

            var destAdapter = this.EditorViewModel.SyncDestinationPageViewModel.SelectedSyncAdapter;
            if (destAdapter == null)
            {
                return;
            }

            if (string.IsNullOrEmpty(destAdapter.DestinationPath) ||
                string.IsNullOrEmpty(sourceAdapter.DestinationPath))
            {
                return;
            }

            string sourceFolderName = sourceAdapter.DestinationPath.Split(
                new[] { sourceAdapter.AdapterBase.PathSeparator },
                StringSplitOptions.None).Last();

            string destFolderName = destAdapter.DestinationPath.Split(
                new[] { destAdapter.AdapterBase.PathSeparator },
                StringSplitOptions.None).Last();

            this.Name = string.Format(
                "Copy '{0}' ({1}) to '{2}' ({3})",
                sourceFolderName,
                sourceAdapter.ShortDisplayName,
                destFolderName,
                destAdapter.ShortDisplayName);
        }
    }
}