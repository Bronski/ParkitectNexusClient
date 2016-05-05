﻿// ParkitectNexusClient
// Copyright (C) 2016 ParkitectNexus, Tim Potze
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <http://www.gnu.org/licenses/>.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using ParkitectNexus.Data.Assets.Modding;
using ParkitectNexus.Data.Caching;
using ParkitectNexus.Data.Game;

namespace ParkitectNexus.Data.Assets
{
    public class AssetUpdatesManager : IAssetUpdatesManager
    {
        private readonly ICacheManager _cacheManager;
        private readonly TimeSpan _checkInterval = TimeSpan.FromHours(1);
        private readonly IParkitect _parkitect;
        private readonly IRemoteAssetRepository _remoteAssetRepository;

        private bool _isChecking;
        private IDictionary<IAsset, string> _updatesAvailable = new Dictionary<IAsset, string>();

        public AssetUpdatesManager(IRemoteAssetRepository remoteAssetRepository, IParkitect parkitect,
            ICacheManager cacheManager)
        {
            _remoteAssetRepository = remoteAssetRepository;
            _parkitect = parkitect;
            _cacheManager = cacheManager;
        }

        public bool HasChecked { get; private set; }

        public event EventHandler<AssetEventArgs> UpdateFound
        {
            add
            {
                BaseUpdateFound += value;

                if (HasChecked)
                {
                    foreach (var asset in _updatesAvailable.Keys)
                        value(this, new AssetEventArgs(asset));
                }
            }
            remove { BaseUpdateFound -= value; }
        }

        public bool ShouldCheckForUpdates()
        {
            var cache = _cacheManager.GetItem<AssetUpdatesCache>("updates_check");

            if (cache == null || DateTime.Now - cache.CheckedDate > _checkInterval)
                return true;

            if (_isChecking)
                return false;

            ReadFromCache();

            return !HasChecked;
        }

        public async Task<int> CheckForUpdates()
        {
            _isChecking = true;
            var count = 0;
            try
            {
                foreach (var asset in _parkitect.Assets[AssetType.Mod].OfType<ModAsset>())
                {
                    if (asset?.Repository == null || asset.Information.IsDevelopment)
                        continue;

                    var latestTag = await _remoteAssetRepository.GetLatestModTag(asset);

                    if (latestTag != null && asset.Tag != latestTag)
                    {
                        _updatesAvailable.Add(asset, latestTag);
                        count++;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine(e);
            }

            _cacheManager.SetItem("updates_check", AssetUpdatesCache.FromAssetsList(_updatesAvailable));

            HasChecked = true;

            return count;
        }

        public async Task<bool> IsUpdateAvailableOnline(IAsset asset)
        {
            if (asset == null) throw new ArgumentNullException(nameof(asset));

            switch (asset.Type)
            {
                case AssetType.Blueprint:
                case AssetType.Savegame:
                    return false;
                case AssetType.Mod:
                    var modAsset = asset as IModAsset;
                    var latestTag = await _remoteAssetRepository.GetLatestModTag(modAsset);
                    return latestTag != null && latestTag != modAsset.Tag;
                default:
                    throw new ArgumentException("invalid asset type", nameof(asset));
            }
        }

        public bool IsUpdateAvailableInMemory(IAsset asset)
        {
            if (asset == null) throw new ArgumentNullException(nameof(asset));

            ReadFromCache();

            switch (asset.Type)
            {
                case AssetType.Blueprint:
                case AssetType.Savegame:
                    return false;
                case AssetType.Mod:
                    return HasChecked && _updatesAvailable.ContainsKey(asset);

                default:
                    throw new ArgumentException("invalid asset type", nameof(asset));
            }
        }

        public async Task<string> GetLatestVersionName(IAsset asset)
        {
            if (asset == null) throw new ArgumentNullException(nameof(asset));

            ReadFromCache();

            switch (asset.Type)
            {
                case AssetType.Blueprint:
                case AssetType.Savegame:
                    // TODO: Implement
                    return null;
                case AssetType.Mod:
                    var modAsset = asset as IModAsset;

                    if (!HasChecked)
                        return await _remoteAssetRepository.GetLatestModTag(modAsset);

                    string tag;
                    return _updatesAvailable.TryGetValue(asset, out tag) ? tag : modAsset.Tag;
                default:
                    throw new ArgumentException("invalid asset type", nameof(asset));
            }
        }

        private event EventHandler<AssetEventArgs> BaseUpdateFound;

        private void ReadFromCache()
        {
            if (HasChecked)
                return;

            var cache = _cacheManager.GetItem<AssetUpdatesCache>("updates_check");

            if (cache == null)
                return;

            _updatesAvailable = cache.ToAssetsList(_parkitect);
            HasChecked = true;
        }

        protected virtual void OnUpdateFound(AssetEventArgs e)
        {
            BaseUpdateFound?.Invoke(this, e);
        }

        private class AssetUpdatesCache
        {
            public IList<AssetCachedUpdateInfo> Updates { get; set; }

            public DateTime CheckedDate { get; set; }

            public IDictionary<IAsset, string> ToAssetsList(IParkitect parkitect)
            {
                if (Updates == null)
                    return null;

                var result = new Dictionary<IAsset, string>();
                foreach (var keyValue in Updates)
                {
                    var asset = parkitect.Assets[keyValue.Type].FirstOrDefault(a => a.Id == keyValue.Id);

                    if (asset != null)
                        result[asset] = keyValue.Tag;
                }

                return result;
            }

            public static AssetUpdatesCache FromAssetsList(IDictionary<IAsset, string> updatesAvailable)
            {
                if (updatesAvailable == null) throw new ArgumentNullException(nameof(updatesAvailable));
                var result = new AssetUpdatesCache
                {
                    CheckedDate = DateTime.Now,
                    Updates = new List<AssetCachedUpdateInfo>()
                };

                foreach (var keyValue in updatesAvailable)
                    result.Updates.Add(new AssetCachedUpdateInfo
                    {
                        Type = keyValue.Key.Type,
                        Id = keyValue.Key.Id,
                        Tag = keyValue.Value
                    });

                return result;
            }
        }

        private class AssetCachedUpdateInfo
        {
            public AssetType Type { get; set; }

            public string Id { get; set; }

            public string Tag { get; set; }
        }
    }
}