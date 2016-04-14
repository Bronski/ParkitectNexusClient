﻿// ParkitectNexusClient
// Copyright 2016 Parkitect, Tim Potze

using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using ParkitectNexus.Client.Base.Main;
using ParkitectNexus.Data;
using ParkitectNexus.Data.Game;
using ParkitectNexus.Data.Presenter;
using ParkitectNexus.Data.Tasks;
using ParkitectNexus.Data.Tasks.Prefab;
using ParkitectNexus.Data.Utilities;
using ParkitectNexus.Data.Web;
using ParkitectNexus.Data.Web.Models;
using Xwt;

namespace ParkitectNexus.Client.Base
{
    public class App : IPresenter
    {
        private readonly ILogger _log;
        private readonly IParkitect _parkitect;
        private readonly IPresenterFactory _presenterFactory;
        private readonly IQueueableTaskManager _taskManager;
        private bool _isRunning;
        private MainWindow _window;

        public App(IPresenterFactory presenterFactory, IParkitect parkitect, IQueueableTaskManager taskManager,
            ILogger log)
        {
            _presenterFactory = presenterFactory;
            _parkitect = parkitect;
            _taskManager = taskManager;
            _log = log;
        }

        public static UIImageProvider Images { get; } = new UIImageProvider();

        public bool Initialize(ToolkitType type)
        {
            _log.Open(Path.Combine(AppData.Path, "ParkitectNexusLauncher.log"));
            _log.MinimumLogLevel = LogLevel.Debug;

            Application.Initialize(type);

            _window = _presenterFactory.InstantiatePresenter<MainWindow>();
            _window.Show();

            if (!_parkitect.DetectInstallationPath())
            {
                if (
                    !MessageDialog.Confirm("We couldn't detect Parkitect on your machine.\nPlease point me to it!",
                        Command.Ok))
                {
                    _window.Dispose();
                    Application.Dispose();
                    return false;
                }

                do
                {
                    var dlg = new SelectFolderDialog("Select your Parkitect installation folder.")
                    {
                        CanCreateFolders = false,
                        Multiselect = false
                    };


                    if (dlg.Run(_window))
                    {
                        if (_parkitect.SetInstallationPathIfValid(dlg.Folder))
                            break;
                    }
                    else
                    {
                        _window.Dispose();
                        Application.Dispose();
                        return false;
                    }
                } while (!_parkitect.IsInstalled);
            }

            return true;
        }

        public void HandleUrl(INexusUrl url)
        {
            var attribute = url.Data.GetType().GetCustomAttribute<UrlActionTaskAttribute>();
            if (attribute?.TaskType != null && typeof (UrlQueueableTask).IsAssignableFrom(attribute.TaskType))
            {
                var task = ObjectFactory.GetInstance<UrlQueueableTask>(attribute.TaskType);
                task.Data = url.Data;

                _taskManager.Add(task);
            }
        }

        // TODO: Replace with simple TCP server configuration
        private async void CheckForIpcFile()
        {
            var ipcPath = Path.Combine(AppData.Path, "ipc.dat");
            while (_isRunning)
            {
                try
                {
                    if (File.Exists(ipcPath))
                    {
                        var lines = File.ReadAllLines(ipcPath);
                        File.Delete(ipcPath);
                        foreach (var contents in lines)
                        {
                            NexusUrl url;
                            if (NexusUrl.TryParse(contents, out url))
                                HandleUrl(url);
                        }
                    }
                }
                catch
                {
                }
                await Task.Delay(1000);
            }
        }

        public void Run()
        {
            _isRunning = true;
            CheckForIpcFile();

            Application.Run();

            _isRunning = false;
            _window.Dispose();
            Application.Dispose();
        }
    }
}
