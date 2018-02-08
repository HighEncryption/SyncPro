namespace SyncProLogViewer.ViewModels
{
    using System.Diagnostics;
    using System.IO;

    using Newtonsoft.Json;

    public class ViewerConfiguration : ViewModelBase
    {
        public const string DefaultFileName = "configuration.json";

        public ViewerConfiguration()
        {
            this.WindowHeight = 650;
            this.WindowWidth = 950;
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private int windowHeight;

        public int WindowHeight
        {
            get { return this.windowHeight; }
            set { this.SetProperty(ref this.windowHeight, value); }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private int windowWidth;

        public int WindowWidth
        {
            get { return this.windowWidth; }
            set { this.SetProperty(ref this.windowWidth, value); }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private int windowTop;

        public int WindowTop
        {
            get { return this.windowTop; }
            set { this.SetProperty(ref this.windowTop, value); }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private int windowLeft;

        public int WindowLeft
        {
            get { return this.windowLeft; }
            set { this.SetProperty(ref this.windowLeft, value); }
        }

        public static ViewerConfiguration LoadOrCreate(string path, string configFileName = DefaultFileName)
        {
            string configFilePath = Path.Combine(path, configFileName);

            if (File.Exists(configFilePath))
            {
                string configJson = File.ReadAllText(configFilePath);
                return JsonConvert.DeserializeObject<ViewerConfiguration>(configJson);
            }

            return new ViewerConfiguration();
        }

        public void Save(string path, string configFileName = DefaultFileName)
        {
            string configJson = JsonConvert.SerializeObject(this, Formatting.Indented);

            File.WriteAllText(
                Path.Combine(path, configFileName),
                configJson);
        }
    }
}