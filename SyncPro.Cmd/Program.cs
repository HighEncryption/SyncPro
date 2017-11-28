namespace SyncPro.Cmd
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;

    using Newtonsoft.Json;

    using SyncPro.Adapters;
    using SyncPro.Adapters.MicrosoftOneDrive;
    using SyncPro.Data;
    using SyncPro.OAuth;
    using SyncPro.Runtime;
    using SyncPro.Utility;

    class Program
    {
        static void Main(string[] arguments)
        {
            Dictionary<string, string> args = CommandLineHelper.ParseCommandLineArgs(arguments);

            try
            {
                if (args.ContainsKey("dumpConfig"))
                {
                    DumpConfig();
                }
                else if (args.ContainsKey("extractToken"))
                {
                    ExtractToken(args);
                }
                else if (args.ContainsKey("setToken"))
                {
                    SetToken(args);
                }
                else if (args.ContainsKey("reset"))
                {
                    Reset(args);
                }
                else
                {
                    throw new Exception("Invalid command line syntax");
                }
            }
            catch (Exception exception)
            {
                Console.WriteLine(exception);
            }
        }

        private static void Reset(Dictionary<string, string> args)
        {
            Global.Initialize(false);

            SyncRelationship relationship = GetRelationship(args);

            using (var db = relationship.GetDatabase())
            {
                var rootEntry = db.Entries.First(e => e.ParentId == null || e.ParentId == 0);

                var entryCount = db.Entries.Count();
                Console.WriteLine("Removing {0} entries", entryCount);

                foreach (SyncEntry syncEntry in db.Entries.Where(e => e.ParentId != null && e.ParentId != 0))
                {
                    db.Entries.Remove(syncEntry);
                }

                var entryACount = db.AdapterEntries.Count();
                Console.WriteLine("Removing {0} adapter entries", entryACount);

                foreach (SyncEntryAdapterData adapterData in db.AdapterEntries.Where(e => e.SyncEntryId != rootEntry.Id))
                {
                    db.AdapterEntries.Remove(adapterData);
                }

                var historyCount = db.History.Count();
                Console.WriteLine("Removing {0} histories", historyCount);

                db.Database.ExecuteSqlCommand("TRUNCATE TABLE [HistoryEntries]");

                var historyEntryCount = db.HistoryEntries.Count();
                Console.WriteLine("Removing {0} history entries", historyEntryCount);

                db.Database.ExecuteSqlCommand("TRUNCATE TABLE [History]");

                db.SaveChanges();
            }
        }

        private static void DumpConfig()
        {
            Global.Initialize(false);

            string localAppDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string appDataRoot = Path.Combine(localAppDataPath, "SyncPro");

            DirectoryInfo appDataRootDir = new DirectoryInfo(appDataRoot);

            foreach (DirectoryInfo relationshipDir in appDataRootDir.GetDirectories())
            {
                Guid guid;
                if (!Guid.TryParse(relationshipDir.Name, out guid))
                {
                    WriteWarning("Failed to parse relationship directory '{0}' as a GUID", relationshipDir.Name);
                    continue;
                }

                SyncRelationship relationship = SyncRelationship.Load(guid);
                //relationship.BeginInitialize();

                // TODO: Do we really need to initialize the relationship in order to dump the configuration?
                //relationship.InitializeAsync().Wait();

                Console.WriteLine("---------------------------- [Relationship] ----------------------------");
                Console.WriteLine("RelationshipId: " + relationship.Configuration.RelationshipId);
                Console.WriteLine("Name: " + relationship.Configuration.Name);
                Console.WriteLine("Description: " + relationship.Configuration.Description);
                Console.WriteLine("Scope: " + relationship.Configuration.Scope);
                Console.WriteLine("SyncAttributes: " + relationship.Configuration.SyncAttributes);
                Console.WriteLine("TriggerType: " + relationship.Configuration.TriggerConfiguration.TriggerType);
                Console.WriteLine("SourceAdapter: " + relationship.Configuration.SourceAdapterId);
                Console.WriteLine("DestinationAdapter: " + relationship.Configuration.DestinationAdapterId);
                Console.WriteLine();
                Console.WriteLine("---------- [Adapters] ---------- ");

                foreach (AdapterBase adapter in relationship.Adapters)
                {
                    Console.WriteLine("Id: " + adapter.Configuration.Id);
                    Console.WriteLine("AdapterTypeId: " + adapter.Configuration.AdapterTypeId);
                    Console.WriteLine("AdapterTypeName: " + 
                        AdapterFactory.GetTypeFromAdapterTypeId(adapter.Configuration.AdapterTypeId).Name);
                    Console.WriteLine("IsOriginator: " + adapter.Configuration.IsOriginator);
                    Console.WriteLine("Flags: " + 
                        string.Join(",", StringExtensions.GetSetFlagNames<Data.AdapterFlags>(adapter.Configuration.Flags)));
                    Console.WriteLine();
                }

                Console.WriteLine();
            }
        }

        private static void ExtractToken(Dictionary<string, string> args)
        {
            Global.Initialize(false);

            AdapterBase adapter = GetAdapter(args);

            bool formatToken = args.ContainsKey("formatToken");
            string file;
            args.TryGetValue("file", out file);

            if (adapter.Configuration.AdapterTypeId == OneDriveAdapter.TargetTypeId)
            {
                TokenResponse token = ((OneDriveAdapterConfiguration) adapter.Configuration).CurrentToken;

                if (args.ContainsKey("decrypt"))
                {
                    token.Unprotect();
                }

                string formattedToken = JsonConvert.SerializeObject(token, formatToken ? Formatting.Indented : Formatting.None);

                if (string.IsNullOrEmpty(file))
                {
                    Console.WriteLine(formattedToken);
                }
                else
                {
                    File.WriteAllText(file, formattedToken);
                }
            }
            else
            {
                Type adapterType = AdapterFactory.GetTypeFromAdapterTypeId(adapter.Configuration.AdapterTypeId);
                throw new Exception(
                    string.Format("Cannot extract token from adapter with type {0} ({1})",
                        adapterType.Name, adapter.Configuration.AdapterTypeId));
            }
        }

        private static void SetToken(Dictionary<string, string> args)
        {
            Global.Initialize(false);

            AdapterBase adapter = GetAdapter(args);
            string file;

            if (!args.TryGetValue("file", out file))
            {
                throw new Exception("/file is required");
            }

            if (adapter.Configuration.AdapterTypeId == OneDriveAdapter.TargetTypeId)
            {
                string tokenContent = File.ReadAllText(file);
                TokenResponse token = JsonConvert.DeserializeObject<TokenResponse>(tokenContent);

                // Encrypt the token if not already encrypted
                token.Protect();

                ((OneDriveAdapterConfiguration) adapter.Configuration).CurrentToken = token;

                adapter.SaveConfiguration();
            }
            else
            {
                Type adapterType = AdapterFactory.GetTypeFromAdapterTypeId(adapter.Configuration.AdapterTypeId);
                throw new Exception(
                    string.Format("Cannot set token from adapter with type {0} ({1})",
                        adapterType.Name, adapter.Configuration.AdapterTypeId));
            }
        }

        private static SyncRelationship GetRelationship(Dictionary<string, string> args)
        {
            string strRelationshipId;
            if (!args.TryGetValue("relationshipId", out strRelationshipId))
            {
                throw new Exception("/relationshipId parameter not provided.");
            }

            Guid relationshipId = Guid.Parse(strRelationshipId);

            string localAppDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string appDataRoot = Path.Combine(localAppDataPath, "SyncPro");

            DirectoryInfo appDataRootDir = new DirectoryInfo(appDataRoot);

            foreach (DirectoryInfo relationshipDir in appDataRootDir.GetDirectories())
            {
                Guid guid;
                if (Guid.TryParse(relationshipDir.Name, out guid) && guid == relationshipId)
                {
                    SyncRelationship relationship = SyncRelationship.Load(guid);

                    return relationship;
                }
            }

            throw new Exception("No relationship found with ID " + relationshipId);
        }

        private static AdapterBase GetAdapter(Dictionary<string, string> args)
        {
            SyncRelationship relationship = GetRelationship(args);

            string strAdapterId;
            if (!args.TryGetValue("adapterId", out strAdapterId))
            {
                throw new Exception("/adapterId parameter not provided.");
            }

            int adapterId = int.Parse(strAdapterId);

            AdapterBase adapter = relationship.Adapters.FirstOrDefault(a => a.Configuration.Id == adapterId);
            if (adapter == null)
            {
                throw new Exception("No adapter found with ID " + adapterId);
            }

            return adapter;
        }

        private static void WriteWarning(string format, params object[] args)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine(format, args);
            Console.ResetColor();
        }
    }
}
