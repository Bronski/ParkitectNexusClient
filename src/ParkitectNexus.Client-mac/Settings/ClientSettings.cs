// ParkitectNexusClient
// Copyright 2016 Parkitect, Tim Potze

using ParkitectNexus.Data.Settings;

namespace ParkitectNexus.Client.Settings
{
    public class ClientSettings : SettingsBase
    {
        public string DownloadOnNextRun { get; set; }
    }
}