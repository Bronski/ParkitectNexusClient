﻿// ParkitectNexusClient
// Copyright 2016 Parkitect, Tim Potze

namespace ParkitectNexus.Data.Web.Client
{
    public interface INexusWebClientFactory
    {
        INexusWebClient CreateWebClient();
        INexusWebClient CreateWebClient(bool authorize);
    }
}