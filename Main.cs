﻿using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Windows.Forms;

using Roblox.Reflection;
using Microsoft.Win32;

namespace Roblox
{
    public partial class Main : Form
    {
        private const string VERSION_API_KEY = "76e5a40c-3ae1-4028-9f10-7c62520bd94f";
        private static RegistryKey versionRegistry => Program.GetRegistryKey(Program.MainRegistry, "Current Versions");

        private delegate void StatusDelegate(string msg);
        private delegate string BranchDelegate();

        private static WebClient http = new WebClient();

        public Main()
        {
            InitializeComponent();
        }

        private string getBranch()
        {
            object result;

            if (InvokeRequired)
            {
                BranchDelegate branchDelegate = new BranchDelegate(getBranch);
                result = Invoke(branchDelegate);
            }
            else
            {
                result = branch.SelectedItem;
            }

            return result.ToString();
        }

        private static async Task<string> getLiveVersion(string branch, string endPoint, string binaryType)
        {
            string versionUrl = "https://versioncompatibility.api."
                                + branch + ".com/" + endPoint + "?binaryType=" 
                                + binaryType + "&apiKey=" + VERSION_API_KEY;

            string version = await http.DownloadStringTaskAsync(versionUrl);
            version = version.Replace('"', ' ').Trim();

            return version;
        }

        private void setStatus(string msg = "")
        {
            if (InvokeRequired)
            {
                StatusDelegate status = new StatusDelegate(setStatus);
                Invoke(status, msg);
            }
            else
            {
                status.Text = "Status: " + msg;
                status.Refresh();
            }
        }

        private async Task lockWindowAndRunTask(Func<Task> task)
        {
            Enabled = false;
            UseWaitCursor = true;

            await Task.Run(task);

            Enabled = true;
            UseWaitCursor = false;

            setStatus("Ready!");
        }

        private static void writeAndViewFile(string path, string contents)
        {
            if (!File.Exists(path) || File.ReadAllText(path) != contents)
                File.WriteAllText(path, contents);

            Process.Start(path);
        }

        private static string getWorkDirectory()
        {
            string localAppData = Environment.GetEnvironmentVariable("LocalAppData");

            string workDir = Path.Combine(localAppData, "RobloxApiDumpFiles");
            Directory.CreateDirectory(workDir);

            return workDir;
        }

        public static async Task<string> GetApiDumpFilePath(string branch, Action<string> setStatus = null, bool fetchPrevious = false)
        {
            string coreBin = getWorkDirectory();

            string setupUrl = "https://s3.amazonaws.com/setup." + branch + ".com/";
            setStatus?.Invoke("Checking for update...");

            string version = await getLiveVersion(branch, "GetCurrentClientVersionUpload", "WindowsStudio");

            if (fetchPrevious)
                version = await ReflectionHistory.GetPreviousVersionGuid(branch, version);

            string file = Path.Combine(coreBin, version + ".json");

            if (!File.Exists(file))
            {
                setStatus?.Invoke("Grabbing the" + (fetchPrevious ? " previous " : " ") + "API Dump from " + branch);

                string apiDump = await http.DownloadStringTaskAsync(setupUrl + version + "-API-Dump.json");
                File.WriteAllText(file, apiDump);

                if (fetchPrevious)
                    versionRegistry.SetValue(branch + "-prev", version);
                else
                    versionRegistry.SetValue(branch, version);

                clearOldVersionFiles();
            }
            else
            {
                setStatus?.Invoke("Already up to date!");
            }

            return file;
        }

        private async Task<string> getApiDumpFilePath(string branch, bool fetchPrevious = false)
        {
            return await GetApiDumpFilePath(branch, setStatus, fetchPrevious);
        }

        private void branch_SelectedIndexChanged(object sender, EventArgs e)
        {
            string branch = getBranch();

            if (branch == "roblox")
                compareVersions.Text = "Compare Previous Version";
            else
                compareVersions.Text = "Compare to Production";

            Program.MainRegistry.SetValue("LastSelectedBranch", branch);

            viewApiDumpJson.Enabled = true;
            viewApiDumpClassic.Enabled = true;
            compareVersions.Enabled = true;
        }

        private async void viewApiDumpJson_Click(object sender, EventArgs e)
        {
            await lockWindowAndRunTask(async () =>
            {
                string branch = getBranch();
                string filePath = await getApiDumpFilePath(branch);

                clearOldVersionFiles();
                Process.Start(filePath);
            });
        }

        private async void viewApiDumpClassic_Click(object sender, EventArgs e)
        {
            await lockWindowAndRunTask(async () =>
            {
                string branch = getBranch();
                string apiFilePath = await getApiDumpFilePath(branch);
                string apiJson = File.ReadAllText(apiFilePath);

                ReflectionDatabase api = ReflectionDatabase.Load(apiJson);
                ReflectionDumper dumper = new ReflectionDumper(api);

                string result = dumper.DumpTxt();

                FileInfo info = new FileInfo(apiFilePath);
                string directory = info.DirectoryName;

                string resultPath = Path.Combine(directory, branch + "-api-dump.txt");
                writeAndViewFile(resultPath, result);
            });
        }

        private async void compareVersions_Click(object sender, EventArgs e)
        {
            await lockWindowAndRunTask(async () =>
            {
                string newBranch = getBranch();
                bool fetchPrevious = (newBranch == "roblox");

                string newApiFilePath = await getApiDumpFilePath(newBranch);
                string oldApiFilePath = await getApiDumpFilePath("roblox", fetchPrevious);

                setStatus("Reading the " + (fetchPrevious ? "Previous" : "Production") + " API...");
                string oldApiJson = File.ReadAllText(oldApiFilePath);
                ReflectionDatabase oldApi = ReflectionDatabase.Load(oldApiJson);

                setStatus("Reading the " + (fetchPrevious ? "Production" : "New") + " API...");
                string newApiJson = File.ReadAllText(newApiFilePath);
                ReflectionDatabase newApi = ReflectionDatabase.Load(newApiJson);

                setStatus("Comparing APIs...");
                ReflectionDiffer differ = new ReflectionDiffer();
                string result = differ.CompareDatabases(oldApi, newApi);

                if (result.Length > 0)
                {
                    FileInfo info = new FileInfo(newApiFilePath);

                    string directory = info.DirectoryName;
                    string resultPath = Path.Combine(directory, newBranch + "-diff.txt");

                    writeAndViewFile(resultPath, result);
                }
                else
                {
                    MessageBox.Show("No differences were found!", "Well, this is awkward...", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }

                clearOldVersionFiles();
            });
        }

        private static void clearOldVersionFiles()
        {
            string workDir = getWorkDirectory();

            string[] activeVersions = versionRegistry.GetValueNames()
                .Select(branch => Program.GetRegistryString(versionRegistry, branch))
                .ToArray();

            string[] oldFiles = Directory.GetFiles(workDir, "version-*.json")
                .Select(file => new FileInfo(file))
                .Where(fileInfo => !activeVersions.Contains(fileInfo.Name.Substring(0, 24)))
                .Select(fileInfo => fileInfo.FullName)
                .ToArray();

            foreach (string oldFile in oldFiles)
            {
                try
                {
                    File.Delete(oldFile);
                }
                catch
                {
                    Console.WriteLine("Could not delete file {0}", oldFile);
                }
            }
        }

        private async Task initVersionCache()
        {
            await lockWindowAndRunTask(async () =>
            {
                string[] branches = branch.Items.Cast<string>().ToArray();
                setStatus("Initializing version cache...");

                // Fetch the version guids for roblox, and gametest1-gametest5
                foreach (string branchName in branches)
                {
                    string versionGuid = await getLiveVersion(branchName, "GetCurrentClientVersionUpload", "WindowsStudio");
                    versionRegistry.SetValue(branchName, versionGuid);
                }

                // Fetch the previous version guid for roblox.
                string robloxGuid = Program.GetRegistryString(versionRegistry, "roblox");
                string prevGuid = await ReflectionHistory.GetPreviousVersionGuid("roblox", robloxGuid);
                versionRegistry.SetValue("roblox-prev", prevGuid);

                // Done.
                Program.MainRegistry.SetValue("InitializedVersions", true);
            });
        }

        private async void Main_Load(object sender, EventArgs e)
        {
            if (Program.GetRegistryString(Program.MainRegistry, "InitializedVersions") != "True")
            {
                await initVersionCache();
                clearOldVersionFiles();
            }

            try
            {
                string lastSelectedBranch = Program.GetRegistryString(Program.MainRegistry, "LastSelectedBranch");
                branch.SelectedIndex = branch.Items.IndexOf(lastSelectedBranch);
            }
            catch
            {
                branch.SelectedIndex = 0;
            }
        }
    }
}
