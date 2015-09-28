// ParkitectNexusClient
// Copyright 2015 Parkitect, Tim Potze

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using ParkitectNexus.Data.Properties;
using ParkitectNexus.ModLoader;

namespace ParkitectNexus.Data
{
    /// <summary>
    ///     Represents the Parkitect game.
    /// </summary>
    public class Parkitect
    {
        /// <summary>
        ///     Gets or sets the installation path.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown if the installation path is invalid</exception>
        public string InstallationPath
        {
            get
            {
                return IsValidInstallationPath(Settings.Default.InstallationPath)
                    ? Settings.Default.InstallationPath
                    : null;
            }
            set
            {
                if (!IsValidInstallationPath(value))
                    throw new ArgumentException("invalid installation path", nameof(value));

                Settings.Default.InstallationPath = value;
                Settings.Default.Save();
            }
        }

        /// <summary>
        ///     Gets or sets a value indicating whether the game is installed.
        /// </summary>
        public bool IsInstalled => IsValidInstallationPath(InstallationPath);

        /// <summary>
        ///     Gets the data path.
        /// </summary>
        public string DataPath => !IsInstalled ? null : Path.Combine(InstallationPath, "Parkitect_Data");

        /// <summary>
        ///     Gets the executable path.
        /// </summary>
        public string ExecutablePath => !IsInstalled ? null : Path.Combine(InstallationPath, "Parkitect.exe");


        /// <summary>
        ///     Gets a collection of assembly names provided by the game.
        /// </summary>
        public IEnumerable<string> ManagedAssemblyNames
                    => !IsInstalled ? null : Directory.GetFiles(ManagedDataPath, "*.dll").Select(Path.GetFileName);

        /// <summary>
        ///     Gets the path to the mods directory.
        /// </summary>
        public string ModsPath
        {
            get
            {
                if (!IsInstalled)
                    return null;

                var path = Path.Combine(InstallationPath, "mods");
                Directory.CreateDirectory(path);
                return path;
            }
        }

        /// <summary>
        ///     Gets the managed data path.
        /// </summary>
        public string ManagedDataPath => !IsInstalled ? null : Path.Combine(DataPath, "Managed");
        
        /// <summary>
        ///     Gets a collection of installed mods.
        /// </summary>
        public IEnumerable<ParkitectMod> InstalledMods
        {
            get
            {
                if (!IsInstalled)
                    yield break;

                // Iterate trough every directory in the mods directory which has a mod.json file.
                foreach (
                    var path in
                        Directory.GetDirectories(ModsPath).Where(path => File.Exists(Path.Combine(path, "mod.json"))))
                {
                    // Attempt to deserialize the mod.json file.
                    ParkitectMod mod = null;
                    try
                    {
                        mod =
                            JsonConvert.DeserializeObject<ParkitectMod>(File.ReadAllText(Path.Combine(path, "mod.json")));
                        mod.Path = path;
                    }
                    catch
                    {
                    }

                    // If the mod.json file was deserialized successfully, return the mod.
                    if (mod != null)
                        yield return mod;
                }
            }
        }

        /// <summary>
        ///     Determines whether the specified path is valid installation path.
        /// </summary>
        /// <param name="path">The path.</param>
        /// <returns>true if valid; false otherwise.</returns>
        private static bool IsValidInstallationPath(string path)
        {
            // Path must exist and contain Parkitect.exe.
            return !string.IsNullOrWhiteSpace(path) && File.Exists(Path.Combine(path, "Parkitect.exe"));
        }

        /// <summary>
        ///     Sets the installation path if the specified path is a valid installation path.
        /// </summary>
        /// <param name="path">The path.</param>
        /// <returns>true if valid; false otherwise.</returns>
        public bool SetInstallationPathIfValid(string path)
        {
            if (IsValidInstallationPath(path))
            {
                InstallationPath = path;
                return true;
            }

            return false;
        }

        /// <summary>
        ///     Detects the installation path.
        /// </summary>
        /// <returns>true if the installation path has been detected; false otherwise.</returns>
        public bool DetectInstallationPath()
        {
            if (IsInstalled)
                return true;

            // todo: Detect registry key of installation path.
            // can only do this once it's installed trough steam or a setup.
            return false;
        }

        /// <summary>
        ///     Launches the game with the specified arguments.
        /// </summary>
        /// <param name="arguments">The arguments.</param>
        /// <returns>The launched process.</returns>
        public Process Launch(string arguments = "-single-instance")
        {
            // If the process is already running, push it to the foreground and return it.
            var running = Process.GetProcessesByName("Parkitect").FirstOrDefault();
            
            if (running != null)
            {
                User32.SetForegroundWindow(running.MainWindowHandle);
                return running;
            }

            // Start the game process.
            return !IsInstalled
                ? null
                : Process.Start(new ProcessStartInfo(ExecutablePath)
                {
                    WorkingDirectory = InstallationPath,
                    Arguments = arguments
                });
        }

        /// <summary>
        ///     Launches the game with mods with the specified arguments.
        /// </summary>
        /// <param name="arguments">The arguments.</param>
        /// <returns>The launched process.</returns>
        public Process LaunchWithMods(string arguments = "-single-instance")
        {
            // If no mods have been installed, simply launch the game.
            if (!InstalledMods.Any())
                return Launch(arguments);
            try
            {
                // Compile mods which have been enabled or are tagged as development mods.
                foreach (var mod in InstalledMods)
                {
                    if (mod.IsEnabled || mod.IsDevelopment)
                    {
                        mod.CopyAssetBundles(this);
                        mod.Compile(this);
                    }
                }

                // Launch the game.
                var process = Launch(arguments);

                // Wait for the game to start.
                do
                {
                    Thread.Sleep(500);
                    process.Refresh();
                } while (!process.HasExited && process.MainWindowTitle.Contains("Configuration"));

                // Make sure game didn't close.
                if (process.HasExited)
                    return null;

                // Inject modloader.
                var modLoaderPath =
                    Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location),
                        "ParkitectNexus.Mod.ModLoader.dll");

                if(!File.Exists(modLoaderPath))
                    throw new Exception("ParkitectNexus.Mod.ModLoader.dll not found");
                
                ModInjector.Inject(modLoaderPath, "ParkitectNexus.Mod.ModLoader", "Main", "Load");

                return process;
            }
            catch (Exception e)
            {
                // log exceptions to mods/launcher.log.
                using (var logFile = File.AppendText(Path.Combine(ModsPath, "launcher.log")))
                    logFile.Log(e.Message, LogLevel.Fatal);

                return null;
            }
        }

        /// <summary>
        ///     Stores the specified asset in the game's correct directory.
        /// </summary>
        /// <param name="asset">The asset.</param>
        /// <returns>A task which performs the requested action.</returns>
        public async Task StoreAsset(ParkitectAsset asset)
        {
            if (asset == null) throw new ArgumentNullException(nameof(asset));
            if (!IsInstalled)
                throw new Exception("parkitect is not installed");

            // Gather information about the asset type.
            var assetInfo = asset.Type.GetCustomAttribute<ParkitectAssetInfoAttribute>();

            if (assetInfo == null)
                throw new Exception("invalid asset type");

            switch (asset.Type)
            {
                case ParkitectAssetType.Blueprint:
                case ParkitectAssetType.Savegame:
                    // Create the directory where the asset should be stored and create a path to where the asset should be stored.
                    var storagePath = Path.Combine(InstallationPath, assetInfo.StorageFolder);
                    var assetPath = Path.Combine(storagePath, asset.FileName);

                    Directory.CreateDirectory(storagePath);

                    // If the file already exists, add a number behind the file name.
                    if (File.Exists(assetPath))
                    {
                        var md5 = MD5.Create();

                        // Compute hash of downloaded asset to match with installed hash.
                        asset.Stream.Seek(0, SeekOrigin.Begin);
                        var validHash = md5.ComputeHash(asset.Stream);

                        if (validHash.SequenceEqual(md5.ComputeHash(File.OpenRead(assetPath))))
                            return;

                        // Separate the filename and the extension.
                        var attempt = 1;
                        var fileName = Path.GetFileNameWithoutExtension(asset.FileName);
                        var fileExtension = Path.GetExtension(asset.FileName);

                        // Update the path to where the the asset should be stored by adding a number behind the name until an available filename has been found.
                        do
                        {
                            assetPath = Path.Combine(storagePath, $"{fileName} ({++attempt}){fileExtension}");

                            if (File.Exists(assetPath) &&
                                validHash.SequenceEqual(md5.ComputeHash(File.OpenRead(assetPath))))
                                return;
                        } while (File.Exists(assetPath));
                    }

                    // Write the stream to a file at the asset path.
                    using (var fileStream = File.Create(assetPath))
                    {
                        asset.Stream.Seek(0, SeekOrigin.Begin);
                        await asset.Stream.CopyToAsync(fileStream);
                    }
                    break;
                case ParkitectAssetType.Mod:
                    using (var zip = new ZipArchive(asset.Stream, ZipArchiveMode.Read))
                    {
                        // Compute name of main directory inside archive.
                        var mainFolder = zip.Entries.FirstOrDefault()?.FullName;
                        if (mainFolder == null)
                            throw new Exception("invalid archive");

                        // Find the mod.json file. Yay for / \ path divider compatibility.
                        var modJsonPath = Path.Combine(mainFolder, "mod.json").Replace('/', '\\');
                        var modJson = zip.Entries.FirstOrDefault(e => e.FullName.Replace('/', '\\') == modJsonPath);

                        // Read mod.json.
                        if (modJson == null) throw new Exception("mod is missing mod.json file");
                        using (var streamReader = new StreamReader(modJson.Open()))
                        {
                            var json = await streamReader.ReadToEndAsync();
                            var mod = JsonConvert.DeserializeObject<ParkitectMod>(json);

                            // Set default mod properties.
                            mod.Tag = asset.DownloadInfo.Tag;
                            mod.Repository = asset.DownloadInfo.Repository;
                            mod.Path = Path.Combine(ModsPath, asset.DownloadInfo.Repository.Replace('/', '@'));
                            mod.IsEnabled = true;
                            mod.IsDevelopment = false;

                            // Find previous version of mod.
                            var oldMod = InstalledMods.FirstOrDefault(m => m.Repository == mod.Repository);
                            if (oldMod != null)
                            {
                                // This version was already installed.
                                if (oldMod.IsDevelopment || oldMod.Tag == mod.Tag)
                                    return;
                                
                                oldMod.Delete(this);

                                // Deleting is stupid.
                                // todo look for better solution
                                await Task.Delay(1000);
                            }
                            
                            // Install mod.
                            foreach (var entry in zip.Entries)
                            {
                                if (!entry.FullName.StartsWith(mainFolder))
                                    continue;

                                // Compute path.
                                var partDir = entry.FullName.Substring(mainFolder.Length);
                                var path = Path.Combine(mod.Path, partDir);

                                if (string.IsNullOrEmpty(entry.Name))
                                {
                                    Directory.CreateDirectory(path);
                                }
                                else
                                {
                                    using (var openStream = entry.Open())
                                    using (var fileStream = File.OpenWrite(path))
                                        await openStream.CopyToAsync(fileStream);
                                }
                            }

                            // Save and compile the mod.
                            mod.Save();
                            mod.CopyAssetBundles(this);
                            mod.Compile(this);
                        }
                    }
                    break;
                default:
                    throw new Exception("unsupported asset type");
            }
        }
    }
}