namespace SyncPro.UI.RelationshipEditor
{
    using System.Diagnostics;
    using System.Windows.Input;

    using SyncPro.Configuration;
    using SyncPro.UI.Dialogs;
    using SyncPro.UI.Framework.MVVM;
    using SyncPro.UI.ViewModels;

    public class SyncOptionsPageViewModel : WizardPageViewModelBase
    {
        public ICommand ShowEncryptionSettingsDialogCommand { get; }

        public SyncOptionsPageViewModel(RelationshipEditorViewModel editorViewModel)
            : base(editorViewModel)
        {
            this.SelectedScopeType = SyncScopeType.SourceToDestination;

            this.ShowEncryptionSettingsDialogCommand = new DelegatedCommand(
                this.ShowEncryptionSettingsDialog,
                this.CanShowEncryptionSettingsDialog);
        }

        private bool CanShowEncryptionSettingsDialog(object obj)
        {
            return this.SelectedScopeType == SyncScopeType.SourceToDestination;
        }

        private void ShowEncryptionSettingsDialog(object obj)
        {
            EncryptionSettingsDialogViewModel dialogViewModel = new EncryptionSettingsDialogViewModel
            {
                IsCreateMode = this.EditorViewModel.IsCreateMode
            };

            if (this.EditorViewModel.IsEditMode)
            {
                dialogViewModel.IsEncryptionEnabled = 
                    this.EditorViewModel.Relationship.EncryptionMode != EncryptionMode.None;
                dialogViewModel.CreateNewCertificate = 
                    this.EditorViewModel.Relationship.EncryptionCreateCertificate;
            }

            EncryptionSettingsDialog dialog = new EncryptionSettingsDialog
            {
                DataContext = dialogViewModel
            };

            bool? result = dialog.ShowDialog();
            if (result != null && result.Value)
            {
                this.IsEncryptionEnabled = dialogViewModel.IsEncryptionEnabled;
                this.CreateNewEncryptionCertificate = dialogViewModel.CreateNewCertificate;

                this.SetEncryptedSettingsStatus();
            }
        }

        private void SetEncryptedSettingsStatus()
        {
            if (this.EditorViewModel.IsEditMode)
            {
                switch (this.EditorViewModel.Relationship.EncryptionMode)
                {
                    case EncryptionMode.None:
                        this.EncryptedSettingsStatus = "Encryption is disabled";
                        this.EncryptedSettingsStatusImportant = false;
                        break;
                    case EncryptionMode.Encrypt:
                        this.EncryptedSettingsStatus = "File encryption is enabled";
                        this.EncryptedSettingsStatusImportant = true;
                        break;
                    case EncryptionMode.Decrypt:
                        this.EncryptedSettingsStatus = "File decryption is enabled";
                        this.EncryptedSettingsStatusImportant = true;
                        break;
                }

                return;
            }

            if (this.IsEncryptionEnabled)
            {
                if (this.CreateNewEncryptionCertificate)
                {
                    this.EncryptedSettingsStatus = "File encryption will be enabled using a new certificate.";
                    this.EncryptedSettingsStatusImportant = true;
                }
                else
                {
                    this.EncryptedSettingsStatus = "File encryption will be enabled using an existing certificate.";
                    this.EncryptedSettingsStatusImportant = true;
                }
            }
            else
            {
                this.EncryptedSettingsStatus = "Files will not be encrypted before copying.";
                this.EncryptedSettingsStatusImportant = false;
            }
        }

        public override string TabItemImageSource => "/SyncPro.UI;component/Resources/Graphics/list_20.png";

        public override void LoadContext()
        {
            if (this.EditorViewModel.Relationship.Scope != SyncScopeType.Undefined)
            {
                this.SelectedScopeType = this.EditorViewModel.Relationship.Scope;
            }

            this.IsEncryptionEnabled = 
                this.EditorViewModel.Relationship.EncryptionMode != EncryptionMode.None;

            this.SetEncryptedSettingsStatus();
        }

        public override void SaveContext()
        {
            this.EditorViewModel.Relationship.Scope = this.SelectedScopeType;

            if (this.EditorViewModel.IsCreateMode)
            {
                this.EditorViewModel.Relationship.EncryptionMode =
                    this.IsEncryptionEnabled ? EncryptionMode.Encrypt : EncryptionMode.None;
                this.EditorViewModel.Relationship.EncryptionCreateCertificate =
                    this.CreateNewEncryptionCertificate;
            }
        }

        public override string NavTitle => "Options";

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private SyncScopeType selectedScopeType;

        public SyncScopeType SelectedScopeType
        {
            get { return this.selectedScopeType; }
            set
            {
                if (this.SetProperty(ref this.selectedScopeType, value))
                {
                    if (value == SyncScopeType.Bidirectional)
                    {
                        this.SyncScopeExplaination =
                            "New file, updates, and deletions will be copied from the source to the destination, as well as from the destination to the source. Both directories will be identicaly after synchronization.";
                    }
                    else if (value == SyncScopeType.SourceToDestination)
                    {
                        this.SyncScopeExplaination =
                            "New file, updates, and deletions will be copied from the source to the destination only. Changes made to the destination will not be copied to the source.";
                    }
                }
            }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string syncScopeExplaination;

        public string SyncScopeExplaination
        {
            get { return this.syncScopeExplaination; }
            set { this.SetProperty(ref this.syncScopeExplaination, value); }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private bool isEncryptionEnabled;

        public bool IsEncryptionEnabled
        {
            get { return this.isEncryptionEnabled; }
            set { this.SetProperty(ref this.isEncryptionEnabled, value); }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private bool createNewEncryptionCertificate;

        public bool CreateNewEncryptionCertificate
        {
            get { return this.createNewEncryptionCertificate; }
            set { this.SetProperty(ref this.createNewEncryptionCertificate, value); }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string encryptedSettingsStatus;

        public string EncryptedSettingsStatus
        {
            get { return this.encryptedSettingsStatus; }
            set { this.SetProperty(ref this.encryptedSettingsStatus, value); }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private bool encryptedSettingsStatusImportant;

        public bool EncryptedSettingsStatusImportant
        {
            get { return this.encryptedSettingsStatusImportant; }
            set { this.SetProperty(ref this.encryptedSettingsStatusImportant, value); }
        }
    }
}