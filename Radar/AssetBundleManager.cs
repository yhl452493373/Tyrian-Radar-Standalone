using System;
using System.Reflection;
using UnityEngine;

namespace Radar
{
    internal static class AssetBundleManager
    {
        public static GameObject RadarhudPrefab { get; private set; }
        public static GameObject RadarBliphudPrefab { get; private set; }

        public static Sprite EnemyBlip { get; private set; }
        public static Sprite EnemyBlipDown { get; private set; }
        public static Sprite EnemyBlipUp { get; private set; }
        public static Sprite EnemyBlipDead { get; private set; }

        internal static bool Loaded { get; private set; } = false;


        internal static void LoadAssetBundle()
        {
            if (Loaded) return;
            
            using var resourceStream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Radar.bundle.radarhud.bundle");
            if (resourceStream == null)
            {
                Radar.Log.LogError("Failed to find radar AssetBundle from embedded resource!");
                return;
            }

            try
            {
                // Load the AssetBundle
                var bundle = AssetBundle.LoadFromStream(resourceStream);
                RadarhudPrefab = bundle.LoadAsset<GameObject>("Assets/Examples/Halo Reach/Hud/RadarHUD.prefab")!;
                RadarBliphudPrefab = bundle.LoadAsset<GameObject>("Assets/Examples/Halo Reach/Hud/RadarBlipHUD.prefab")!;

                EnemyBlip = bundle.LoadAsset<Sprite>("EnemyBlip")!;
                EnemyBlipUp = bundle.LoadAsset<Sprite>("EnemyBlipUp")!;
                EnemyBlipDown = bundle.LoadAsset<Sprite>("EnemyBlipDown")!;
                EnemyBlipDead = bundle.LoadAsset<Sprite>("EnemyBlipDead")!;
                bundle.Unload(false);

                Radar.Log.LogInfo("AssetBundle Loaded!");
                Loaded = true;
            }
            catch (Exception e)
            {
                Radar.Log.LogError($"Cannot load radar prefabs from the asset bundle: {e.Message}");
                Radar.Log.LogError(e);
            }
        }
    }
}