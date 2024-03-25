using System.Linq;
using System.Reflection;
using Comfort.Common;
using EFT;
using EFT.Interactive;
using UnityEngine;
using Aki.Reflection.Patching;

namespace Radar.Patches
{
    public class GameStartPatch : ModulePatch
    {

        protected override MethodBase GetTargetMethod()
        {
            return typeof(GameWorld).GetMethod("OnGameStarted", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        }

        [PatchPostfix]
        private static void Postfix()
        {
            Radar.Log.LogDebug("GameStartPatch:Postfix");
            Radar.Instance.OnRaidGameStart();
        }
    }
}