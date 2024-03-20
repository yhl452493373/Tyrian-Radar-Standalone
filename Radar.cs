using EFT;
using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using Comfort.Common;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;
using LootItem = EFT.Interactive.LootItem;
using Debug = UnityEngine.Debug;
using System.Linq;

namespace Radar
{
    [BepInPlugin("Tyrian.Radar", "Radar", "1.1.0")]
    public class Radar : BaseUnityPlugin
    {
        private static GameWorld gameWorld;
        public static bool MapLoaded() => Singleton<GameWorld>.Instantiated;
        public static Player player;
        public static Radar instance;
        public static Dictionary<GameObject, HashSet<Material>> objectsMaterials = new Dictionary<GameObject, HashSet<Material>>();

        const string baseSettings = "Base Settings";
        const string colorSettings = "Color Settings";
        const string radarSettings = "Radar Settings";

        public static ConfigEntry<bool> radarEnableConfig;
        public static ConfigEntry<bool> radarEnablePulseConfig;
        public static ConfigEntry<bool> radarEnableCorpseConfig;
        public static ConfigEntry<bool> radarEnableLootConfig;
        public static ConfigEntry<KeyboardShortcut> radarEnableShortCutConfig;
        public static ConfigEntry<KeyboardShortcut> radarEnableCorpseShortCutConfig;
        public static ConfigEntry<KeyboardShortcut> radarEnableLootShortCutConfig;
        public static bool enableSCDown = false;
        public static bool corpseSCDown = false;
        public static bool lootSCDown = false;

        public static ConfigEntry<float> radarSizeConfig;
        public static ConfigEntry<float> radarBlipSizeConfig;
        public static ConfigEntry<float> radarDistanceScaleConfig;
        public static ConfigEntry<float> radarHeightThresholdeScaleOffsetConfig;
        public static ConfigEntry<float> radarOffsetYConfig;
        public static ConfigEntry<float> radarOffsetXConfig;
        public static ConfigEntry<float> radarRangeConfig;
        public static ConfigEntry<float> radarScanInterval;
        public static ConfigEntry<float> radarLootThreshold;

        public static ConfigEntry<Color> bossBlipColor;
        public static ConfigEntry<Color> usecBlipColor;
        public static ConfigEntry<Color> bearBlipColor;
        public static ConfigEntry<Color> scavBlipColor;
        public static ConfigEntry<Color> corpseBlipColor;
        public static ConfigEntry<Color> lootBlipColor;
        public static ConfigEntry<Color> backgroundColor;


        public static ManualLogSource logger;

        public static Radar Instance
        {
            get { return instance; }
        }

        private void Awake()
        {
            logger = Logger;
            logger.LogInfo("Radar Plugin Enabled.");
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);

            // Add a custom configuration option for the Apply button
            radarEnableConfig = Config.Bind(baseSettings, "Radar Enabled", true, "Adds a Radar feature to the undersuit when you wear it.");
            radarEnableShortCutConfig = Config.Bind(baseSettings, "Short cut for enable/disable radar", new KeyboardShortcut(KeyCode.F10));
            radarEnablePulseConfig = Config.Bind(baseSettings, "Radar Pulse Enabled", true, "Adds the radar pulse effect.");
            
            radarEnableCorpseConfig = Config.Bind(baseSettings, "Radar Corpse Detection Enabled", true, "Adds detection for corpse.");
            radarEnableCorpseShortCutConfig = Config.Bind(baseSettings, "Short cut for enable/disable corpse dection", new KeyboardShortcut(KeyCode.F11));
            radarEnableLootConfig = Config.Bind(baseSettings, "Radar Loot Detection Enabled", true, "Adds detection for loot.");
            radarEnableLootShortCutConfig = Config.Bind(baseSettings, "Short cut for enable/disable loot dection", new KeyboardShortcut(KeyCode.F9));

            radarSizeConfig = Config.Bind<float>(radarSettings, "HUD Size", 1f, new ConfigDescription("The Scale Offset for the Radar Hud.", new AcceptableValueRange<float>(0.0f, 1f)));
            radarBlipSizeConfig = Config.Bind<float>(radarSettings, "HUD Blip Size", 1f, new ConfigDescription("The Scale Offset for the Radar Hud Blip.", new AcceptableValueRange<float>(0.0f, 1f)));
            radarDistanceScaleConfig = Config.Bind<float>(radarSettings, "HUD Blip Disntance Scale Offset", 0.7f, new ConfigDescription("This scales the blips distances from the player, effectively zooming it in and out.", new AcceptableValueRange<float>(0.1f, 2f)));
            radarHeightThresholdeScaleOffsetConfig = Config.Bind<float>(radarSettings, "HUD Blip Height Threshold Offset", 1f, new ConfigDescription("This scales the distance threshold for blips turning into up or down arrows depending on enemies height levels.", new AcceptableValueRange<float>(1f, 4f)));
            radarOffsetYConfig = Config.Bind<float>(radarSettings, "HUD Y Position Offset", 0f, new ConfigDescription("The Y Position Offset for the Radar Hud.", new AcceptableValueRange<float>(-4000f, 4000f)));
            radarOffsetXConfig = Config.Bind<float>(radarSettings, "HUD X Position Offset", 0f, new ConfigDescription("The X Position Offset for the Radar Hud.", new AcceptableValueRange<float>(-4000f, 4000f)));
            radarRangeConfig = Config.Bind<float>(radarSettings, "Radar Range", 128f, new ConfigDescription("The range within which enemies are displayed on the radar.", new AcceptableValueRange<float>(32f, 512f)));
            radarScanInterval = Config.Bind<float>(radarSettings, "Radar Scan Interval", 1f, new ConfigDescription("The interval between two scans.", new AcceptableValueRange<float>(0.1f, 30f)));
            
            radarLootThreshold = Config.Bind<float>(radarSettings, "Loot Threshold", 30000f, new ConfigDescription("Item above this price will show on radar", new AcceptableValueRange<float>(1000f, 100000f)));
            
            bossBlipColor = Config.Bind<Color>(colorSettings, "Boss Blip Color", new Color(1f, 0f, 0f));
            scavBlipColor = Config.Bind<Color>(colorSettings, "Scav Blip Color", new Color(0f, 1f, 0f));
            usecBlipColor = Config.Bind<Color>(colorSettings, "Usec PMC Blip Color", new Color(1f, 1f, 0f));
            bearBlipColor = Config.Bind<Color>(colorSettings, "Bear PMC Blip Color", new Color(1f, 0.5f, 0f));
            corpseBlipColor = Config.Bind<Color>(colorSettings, "Corpse Blip Color", new Color(0.5f, 0.5f, 0.5f));
            lootBlipColor = Config.Bind<Color>(colorSettings, "Loot Blip Color", new Color(0.9f, 0.5f, 0.5f));
            backgroundColor = Config.Bind<Color>(colorSettings, "Background Color", new Color(0f, 0.7f, 0.85f));
        }

        private void Update()
        {
            if (!MapLoaded())
                return;

            gameWorld = Singleton<GameWorld>.Instance;
            player = gameWorld.MainPlayer;
            if (gameWorld == null || player == null)
                return;

            GameObject gamePlayerObject = player.gameObject;
            HaloRadar haloRadar = gamePlayerObject.GetComponent<HaloRadar>();

            // enable radar shortcut process
            if (!enableSCDown && radarEnableShortCutConfig.Value.IsDown())
            {
                radarEnableConfig.Value = !radarEnableConfig.Value;
                enableSCDown = true;
            }
            if (!radarEnableShortCutConfig.Value.IsDown())
            {
                enableSCDown = false;
            }

            // enable corpse shortcut process
            if (!corpseSCDown && radarEnableCorpseShortCutConfig.Value.IsDown())
            {
                radarEnableCorpseConfig.Value = !radarEnableCorpseConfig.Value;
                corpseSCDown = true;
            }
            if (!radarEnableCorpseShortCutConfig.Value.IsDown())
            {
                corpseSCDown = false;
            }

            // enable loot shortcut process
            if (!lootSCDown && radarEnableLootShortCutConfig.Value.IsDown())
            {
                radarEnableLootConfig.Value = !radarEnableLootConfig.Value;
                lootSCDown = true;
            }
            if (!radarEnableLootShortCutConfig.Value.IsDown())
            {
                lootSCDown = false;
            }

            if (radarEnableConfig.Value && haloRadar == null)
            {
                // Add the HaloRadar component if it doesn't exist.
                gamePlayerObject.AddComponent<HaloRadar>();
            }
            else if (!radarEnableConfig.Value && haloRadar != null)
            {
                // Remove the HaloRadar component if it exists.
                haloRadar.Destory();
                Destroy(haloRadar);
            }
        }
    }

    

    public class HaloRadar : MonoBehaviour
    {
        public static GameWorld gameWorld;
        public static Player player;
        public static Object RadarhudPrefab { get; private set; } 
        public static Object RadarBliphudPrefab { get; private set; }
        public static AssetBundle radarBundle;
        public static GameObject radarHud;
        public static GameObject radarBlipHud;
        public static GameObject playerCamera;

        public static RectTransform radarHudBlipBasePosition { get; private set; }
        public static RectTransform radarHudBasePosition { get; private set; }
        public static RectTransform radarHudPulse { get; private set; }
        public static RectTransform radarHudBlip { get; private set; }
        public static Image blipImage;
        public static Sprite EnemyBlip;
        public static Sprite EnemyBlipDown;
        public static Sprite EnemyBlipUp;
        public static Sprite EnemyBlipDead;
        public static Coroutine pulseCoroutine;
        public static float animationDuration = 1f;
        public static float pauseDuration = 4f;
        public static Vector3 radarScaleStart;
        public static float radarPositionYStart = 0f;
        public static float radarPositionXStart = 0f;
        public static bool MapLoaded() => Singleton<GameWorld>.Instantiated;

        public static float radarLastUpdateTime = 0;
        public float radarInterval = -1;

        public HashSet<int> enemyList = new HashSet<int>();
        public List<BlipPlayer> enemyCustomObject = new List<BlipPlayer>();

        public HashSet<string> lootList = new HashSet<string>();
        public List<BlipLoot> lootCustomObject = new List<BlipLoot>();

        private void Start()
        {
            // Create our prefabs from our bundles.
            if (RadarhudPrefab == null)
            {
                String haloRadarHUD = Path.Combine(Environment.CurrentDirectory, "BepInEx/plugins/radar/radarhud.bundle");
                if (!File.Exists(haloRadarHUD))
                    return;
                radarBundle = AssetBundle.LoadFromFile(haloRadarHUD);
                if (radarBundle == null)
                    return;
                RadarhudPrefab = radarBundle.LoadAsset("Assets/Examples/Halo Reach/Hud/RadarHUD.prefab");
                RadarBliphudPrefab = radarBundle.LoadAsset("Assets/Examples/Halo Reach/Hud/RadarBlipHUD.prefab");

                EnemyBlip = radarBundle.LoadAsset<Sprite>("EnemyBlip");
                EnemyBlipUp = radarBundle.LoadAsset<Sprite>("EnemyBlipUp");
                EnemyBlipDown = radarBundle.LoadAsset<Sprite>("EnemyBlipDown");
                EnemyBlipDead = radarBundle.LoadAsset<Sprite>("EnemyBlipDead");
            }
        }
        private void Update()
        {
            if (MapLoaded())
            {
                gameWorld = Singleton<GameWorld>.Instance;
                if (gameWorld.MainPlayer == null)
                {
                    return;
                }

                if (player == null)
                {
                    player = gameWorld.MainPlayer;
                }

                if (playerCamera == null)
                {
                    playerCamera = GameObject.Find("FPS Camera");
                    if (playerCamera == null)
                    {
                        return;
                    }
                }

                if (radarHud == null)
                {
                    var radarHudBase = Instantiate(RadarhudPrefab, playerCamera.transform.position, playerCamera.transform.rotation);
                    radarHud = radarHudBase as GameObject;
                    radarHud.transform.parent = playerCamera.transform;
                    radarHudBasePosition = radarHud.transform.Find("Radar") as RectTransform;
                    radarHudBlipBasePosition = radarHud.transform.Find("Radar/RadarBorder") as RectTransform;
                    radarHudBlipBasePosition.SetAsLastSibling();
                    radarHudPulse = radarHud.transform.Find("Radar/RadarPulse") as RectTransform;
                    radarScaleStart = radarHudBasePosition.localScale;
                    radarPositionYStart = radarHudBasePosition.position.y;
                    radarPositionXStart = radarHudBasePosition.position.x;
                    radarHudBasePosition.position = new Vector2(radarPositionYStart + Radar.radarOffsetYConfig.Value, radarPositionXStart + Radar.radarOffsetXConfig.Value);
                    radarHudBasePosition.localScale = new Vector2(radarScaleStart.y * Radar.radarSizeConfig.Value, radarScaleStart.x * Radar.radarSizeConfig.Value);

                    radarHudBlipBasePosition.GetComponent<Image>().color = Radar.backgroundColor.Value;
                    radarHudPulse.GetComponent<Image>().color = Radar.backgroundColor.Value;
                    radarHud.transform.Find("Radar/RadarBackground").GetComponent<Image>().color = Radar.backgroundColor.Value;

                    radarHud.SetActive(true);
                }

                radarHudBasePosition.position = new Vector2(radarPositionYStart + Radar.radarOffsetYConfig.Value, radarPositionXStart + Radar.radarOffsetXConfig.Value);
                radarHudBasePosition.localScale = new Vector2(radarScaleStart.y * Radar.radarSizeConfig.Value, radarScaleStart.x * Radar.radarSizeConfig.Value);
                radarHudBlipBasePosition.GetComponent<RectTransform>().eulerAngles = new Vector3(0, 0, playerCamera.transform.eulerAngles.y);
                
                UpdateLoot();
                long rslt = UpdateActivePlayer();
                UpdateRadar(rslt != -1);

                if (radarInterval != Radar.radarScanInterval.Value)
                {
                    radarInterval = Radar.radarScanInterval.Value;
                    if (Radar.radarEnablePulseConfig.Value)
                    {
                        StartPulseAnimation();
                    }
                }
            }
        }

        public void Destory()
        {
            if (radarHud != null)
            {
                Destroy(radarHud);
            }
        }

        private void StartPulseAnimation()
        {
            // Stop any previous pulse coroutine
            if (pulseCoroutine != null)
            {
                StopCoroutine(pulseCoroutine);
            }
            // Start the pulse coroutine
            pulseCoroutine = StartCoroutine(PulseCoroutine());
        }

        private IEnumerator PulseCoroutine()
        {
            float interval = Radar.radarScanInterval.Value;
            if (interval < 1)
            {
                interval = 1;
            }
            while (true)
            {
                // Rotate from 360 to 0 over the animation duration
                float t = 0f;
                while (t < 1.0f)
                {
                    t += Time.deltaTime / interval;
                    float angle = Mathf.Lerp(0f, 1f, 1 - t) * 360;

                    // Apply the scale to all axes
                    radarHudPulse.localEulerAngles = new Vector3(0, 0, angle);
                    yield return null;
                }
                // Pause for the specified duration
                // yield return new WaitForSeconds(interval);
            }
        }

        private long UpdateActivePlayer()
        {
            if (Time.time - radarLastUpdateTime < Radar.radarScanInterval.Value)
            {
                return -1;
            }
            else
            {
                radarLastUpdateTime = Time.time;
            }
            IEnumerable<Player> allPlayers = gameWorld.AllPlayersEverExisted;

            if (allPlayers.Count() == enemyList.Count + 1)
            {
                return -2;
            }

            foreach (Player enemyPlayer in allPlayers)
            {
                if (enemyPlayer == null || enemyPlayer == player)
                {
                    continue;
                }
                if (!enemyList.Contains(enemyPlayer.Id))
                {
                    enemyList.Add(enemyPlayer.Id);
                    enemyCustomObject.Add(new BlipPlayer(enemyPlayer));
                }
            }
            return 0;
        }

        private void UpdateLoot()
        {
            if (Time.time - radarLastUpdateTime < Radar.radarScanInterval.Value)
            {
                return;
            }

            if (!Radar.radarEnableLootConfig.Value)
            {
                if (lootList.Count > 0)
                {
                    lootList.Clear();
                    foreach (var loot in lootCustomObject)
                    {
                        loot.DestoryLoot();
                    }
                    lootCustomObject.Clear();
                }
                return;
            }

            HashSet<string> checkedLoot = new HashSet<string>();
            var allLoot = gameWorld.LootItems;
            foreach (LootItem loot in allLoot.GetValuesEnumerator())
            {
                checkedLoot.Add(loot.ItemId);
                if (!lootList.Contains(loot.ItemId))
                {
                    lootList.Add(loot.ItemId);
                    lootCustomObject.Add(new BlipLoot(loot));
                }
            }

            foreach (var item in lootCustomObject.Where(item => !checkedLoot.Contains(item.itemId)).ToList()) // ToList creates a copy to avoid modification during enumeration
            {
                item.DestoryLoot();
                lootList.Remove(item.itemId);
            }

            lootCustomObject.RemoveAll(item => !checkedLoot.Contains(item.itemId));
        }

        private void UpdateRadar(bool positionUpdate = true)
        {
            Target.setPlayerPosition(player.Transform.position);
            Target.setRadarRange(Radar.radarRangeConfig.Value);
            foreach (var obj in enemyCustomObject)
            {
                obj.Update(positionUpdate);
            }
            foreach (var obj in lootCustomObject)
            {
                obj.Update(positionUpdate);
            }
        }
    }
}