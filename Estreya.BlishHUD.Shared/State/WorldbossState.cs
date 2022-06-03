namespace Estreya.BlishHUD.Shared.State
{
    using Blish_HUD;
    using Blish_HUD.Modules.Managers;
    using Estreya.BlishHUD.Shared.State;
    using Gw2Sharp.WebApi.Exceptions;
    using Gw2Sharp.WebApi.V2.Models;
    using Microsoft.Xna.Framework;
    using Newtonsoft.Json;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    public class WorldbossState : APIState<string>
    {
        public event EventHandler<string> WorldbossCompleted;
        public event EventHandler<string> WorldbossRemoved;

        public WorldbossState(Gw2ApiManager apiManager) :
            base(apiManager,
                new List<TokenPermission> { TokenPermission.Account, TokenPermission.Progression })
        {
            this.FetchAction = async (apiManager) => (await apiManager.Gw2ApiClient.V2.Account.WorldBosses.GetAsync()).ToList();

            this.APIObjectAdded += this.APIState_APIObjectAdded;
            this.APIObjectRemoved += this.APIState_APIObjectRemoved;
        }

        private void APIState_APIObjectRemoved(object sender, string e)
        {
            this.WorldbossRemoved?.Invoke(this, e);
        }

        private void APIState_APIObjectAdded(object sender, string e)
        {
            this.WorldbossCompleted?.Invoke(this, e);
        }

        public bool IsCompleted(string apiCode)
        {
            return this.APIObjectList.Contains(apiCode);
        }

        protected override Task Save()
        {
            return Task.CompletedTask;
        }

        protected override Task DoClear() => Task.CompletedTask;

        protected override void DoUnload()
        {
            this.APIObjectAdded -= this.APIState_APIObjectAdded;
            this.APIObjectRemoved -= this.APIState_APIObjectRemoved;
        }
    }
}
