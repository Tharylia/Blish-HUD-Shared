namespace Estreya.BlishHUD.Shared.State;

using Blish_HUD;
using Blish_HUD.Modules.Managers;
using Estreya.BlishHUD.Shared.Extensions;
using Estreya.BlishHUD.Shared.Helpers;
using Estreya.BlishHUD.Shared.Utils;
using Gw2Sharp.WebApi.Exceptions;
using Gw2Sharp.WebApi.V2.Models;
using Humanizer;
using Microsoft.Xna.Framework;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

public abstract class APIState<T> : ManagedState
{
    private readonly Logger Logger;

    protected readonly Gw2ApiManager _apiManager;
    protected readonly List<TokenPermission> _neededPermissions = new List<TokenPermission>();

    private TimeSpan _updateInterval;
    private double _timeSinceUpdate = 0;

    private Task _loadTask;

    protected readonly AsyncLock _listLock = new AsyncLock();

    protected List<T> APIObjectList { get; } = new List<T>();
    protected Func<Gw2ApiManager, Task<List<T>>> FetchAction { get; init; }

    protected event EventHandler<T> APIObjectAdded;
    protected event EventHandler<T> APIObjectRemoved;
    protected event EventHandler APIUpdated;

    public APIState(Gw2ApiManager apiManager, List<TokenPermission> neededPermissions = null, TimeSpan? updateInterval = null, bool awaitLoad = true, int saveInterval = -1) : base(awaitLoad, saveInterval)
    {
        this.Logger = Logger.GetLogger(this.GetType());

        this._apiManager = apiManager;

        if (neededPermissions != null)
        {
            this._neededPermissions.AddRange(neededPermissions);
        }

        this._updateInterval = updateInterval ?? TimeSpan.FromMinutes(5).Add(TimeSpan.FromMilliseconds(100));
    }

    private void ApiManager_SubtokenUpdated(object sender, ValueEventArgs<IEnumerable<Gw2Sharp.WebApi.V2.Models.TokenPermission>> e)
    {
        // Load already called. Don't refresh if no permissions needed anyway.
        if (this._neededPermissions.Count > 0)
        {
            _ = Task.Run(this.Reload);
        }
    }

    public sealed override async Task Clear()
    {
        await this.WaitForLoad();

        using (this._listLock.Lock())
        {
            this.APIObjectList.Clear();
        }

        await this.DoClear();
    }

    private async Task WaitForLoad()
    {
        if (this._loadTask != null)
        {
            await this._loadTask;
        }
    }

    protected abstract Task DoClear();

    protected sealed override async Task InternalReload()
    {
        await this.Clear();
        await this.Load();
    }

    protected sealed override Task Initialize()
    {
        this._apiManager.SubtokenUpdated += this.ApiManager_SubtokenUpdated;
        return Task.CompletedTask;
    }

    protected override void InternalUnload()
    {
        this._apiManager.SubtokenUpdated -= this.ApiManager_SubtokenUpdated;
        AsyncHelper.RunSync(this.Clear);

        this.DoUnload();
    }

    protected abstract void DoUnload();

    protected override void InternalUpdate(GameTime gameTime)
    {
        if (this._updateInterval != Timeout.InfiniteTimeSpan)
        {
            _ = UpdateUtil.UpdateAsync(this.FetchFromAPI, gameTime, this._updateInterval.TotalMilliseconds, ref this._timeSinceUpdate);
        }
    }

    private async Task FetchFromAPI(GameTime gameTime)
    {
        Logger.Info($"Check for api objects.");

        if (this._apiManager == null)
        {
            Logger.Warn("API Manager is null");
            return;
        }

        if (this.FetchAction == null)
        {
            Logger.Warn("No fetchaction definied.");
            return;
        }

        try
        {
            List<T> oldAPIObjectList;
            using (await this._listLock.LockAsync())
            {
                oldAPIObjectList = this.APIObjectList.Copy();
                this.APIObjectList.Clear();

                Logger.Debug("Got {0} api objects from previous fetch: {1}", oldAPIObjectList.Count, JsonConvert.SerializeObject(oldAPIObjectList));

                if (!this._apiManager.HasPermissions(this._neededPermissions))
                {
                    Logger.Warn("API Manager does not have needed permissions: {0}", this._neededPermissions.Humanize());
                    return;
                }

                List<T> apiObjects = await this.FetchAction.Invoke(this._apiManager);

                Logger.Debug("API returned objects: {0}", JsonConvert.SerializeObject(apiObjects));

                this.APIObjectList.AddRange(apiObjects);

                // Check if new api objects have been added.
                foreach (T apiObject in apiObjects)
                {
                    if (!oldAPIObjectList.Any(oldApiObject => oldApiObject.GetHashCode() == apiObject.GetHashCode()))
                    {
                        Logger.Debug($"API Object added: {apiObject}");
                        try
                        {
                            this.APIObjectAdded?.Invoke(this, apiObject);
                        }
                        catch (Exception ex)
                        {
                            Logger.Error(ex, "Error handling api object added event:");
                        }
                    }
                }

                // Immediately after login the api still reports some objects as available because the account record has not been updated yet.
                // After another request to the api they should disappear.
                for (int i = oldAPIObjectList.Count - 1; i >= 0; i--)
                {
                    T oldApiObject = oldAPIObjectList[i];

                    if (!apiObjects.Any(apiObject => apiObject.GetHashCode() == apiObject.GetHashCode()))
                    {
                        Logger.Debug($"API Object disappeared from the api: {oldApiObject}");

                        _ = oldAPIObjectList.Remove(oldApiObject);

                        try
                        {
                            this.APIObjectRemoved?.Invoke(this, oldApiObject);
                        }
                        catch (Exception ex)
                        {
                            Logger.Error(ex, "Error handling api object removed event:");
                        }
                    }
                }

                this.APIUpdated?.Invoke(this, EventArgs.Empty);
            }
        }
        catch (MissingScopesException msex)
        {
            Logger.Warn(msex, "Could not update api objects due to missing scopes:");
        }
        catch (InvalidAccessTokenException iatex)
        {
            Logger.Warn(iatex, "Could not update api objects due to invalid access token:");
        }
        catch (Exception ex)
        {
            Logger.Warn(ex, "Error updating api objects:");
        }
    }

    protected override async Task Load()
    {
        await this.WaitForLoad();

        this._loadTask = this.FetchFromAPI(null);

        await this._loadTask;

        this._loadTask = null;
    }
}
