namespace Estreya.BlishHUD.Shared.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

public static class GW2Utils
{
    public static string FormatCoins(int coins)
    {
        var copper = coins % 100;
        coins = (coins - copper) / 100;
        var silver = coins % 100;
        var gold = (coins - silver) / 100;

        return gold > 0 ? $"{gold}g {silver}s {copper}c" : silver > 0 ? $"{silver}s {copper}c" : $"{copper}c";
    }
}
