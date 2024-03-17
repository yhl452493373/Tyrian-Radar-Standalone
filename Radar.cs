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
using System.Diagnostics;
using System.Linq;

namespace Radar
{
    [BepInPlugin("Tyrian.Radar", "Radar", "1.0.9")]
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
        public static ConfigEntry<KeyboardShortcut> radarEnableShortCutConfig;
        public static ConfigEntry<KeyboardShortcut> radarEnableCorpseShortCutConfig;
        public static bool isShortcutDown = false;
        public static bool isCorpseShortcutDown = false;

        public static ConfigEntry<float> radarSizeConfig;
        public static ConfigEntry<float> radarBlipSizeConfig;
        public static ConfigEntry<float> radarDistanceScaleConfig;
        public static ConfigEntry<float> radarHeightThresholdeScaleOffsetConfig;
        public static ConfigEntry<float> radarOffsetYConfig;
        public static ConfigEntry<float> radarOffsetXConfig;
        public static ConfigEntry<float> radarRangeConfig;
        public static ConfigEntry<float> radarScanInterval;

        public static ConfigEntry<Color> bossBlipColor;
        public static ConfigEntry<Color> usecBlipColor;
        public static ConfigEntry<Color> bearBlipColor;
        public static ConfigEntry<Color> scavBlipColor;
        public static ConfigEntry<Color> corpseBlipColor;
        public static ConfigEntry<Color> backgroundColor;


        public static ManualLogSource logger;
        public static float playerHeight = 1.90f;

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
            radarEnablePulseConfig = Config.Bind(baseSettings, "Radar Pulse Enabled", true, "Adds the radar pulse effect.");
            radarEnableCorpseConfig = Config.Bind(baseSettings, "Radar Corpse Detection Enabled", true, "Adds detection for corpse.");
            radarEnableShortCutConfig = Config.Bind(baseSettings, "Short cut for enable/disable radar", new KeyboardShortcut(KeyCode.F10));
            radarEnableCorpseShortCutConfig = Config.Bind(baseSettings, "Short cut for enable/disable corpse dection", new KeyboardShortcut(KeyCode.F11));

            radarSizeConfig = Config.Bind<float>(radarSettings, "Radar HUD Size", 1f, new ConfigDescription("The Scale Offset for the Radar Hud.", new AcceptableValueRange<float>(0.0f, 1f)));
            radarBlipSizeConfig = Config.Bind<float>(radarSettings, "Radar HUD Blip Size", 1f, new ConfigDescription("The Scale Offset for the Radar Hud Blip.", new AcceptableValueRange<float>(0.0f, 1f)));
            radarDistanceScaleConfig = Config.Bind<float>(radarSettings, "Radar HUD Blip Disntance Scale Offset", 0.7f, new ConfigDescription("This scales the blips distances from the player, effectively zooming it in and out.", new AcceptableValueRange<float>(0.1f, 2f)));
            radarHeightThresholdeScaleOffsetConfig = Config.Bind<float>(radarSettings, "Radar HUD Blip Height Threshold Offset", 1f, new ConfigDescription("This scales the distance threshold for blips turning into up or down arrows depending on enemies height levels.", new AcceptableValueRange<float>(1f, 4f)));
            radarOffsetYConfig = Config.Bind<float>(radarSettings, "Radar HUD Y Position Offset", 0f, new ConfigDescription("The Y Position Offset for the Radar Hud.", new AcceptableValueRange<float>(-4000f, 4000f)));
            radarOffsetXConfig = Config.Bind<float>(radarSettings, "Radar HUD X Position Offset", 0f, new ConfigDescription("The X Position Offset for the Radar Hud.", new AcceptableValueRange<float>(-4000f, 4000f)));
            radarRangeConfig = Config.Bind<float>(radarSettings, "Radar Range", 128f, new ConfigDescription("The range within which enemies are displayed on the radar.", new AcceptableValueRange<float>(32f, 512f)));
            radarScanInterval = Config.Bind<float>(radarSettings, "Radar Scan Interval", 1f, new ConfigDescription("The interval between two scans.", new AcceptableValueRange<float>(0.1f, 30f)));
            
            bossBlipColor = Config.Bind<Color>(colorSettings, "Boss Blip Color", new Color(1f, 0f, 0f));
            scavBlipColor = Config.Bind<Color>(colorSettings, "Scav Blip Color", new Color(0f, 1f, 0f));
            usecBlipColor = Config.Bind<Color>(colorSettings, "Usec PMC Blip Color", new Color(1f, 1f, 0f));
            bearBlipColor = Config.Bind<Color>(colorSettings, "Bear PMC Blip Color", new Color(1f, 0.5f, 0f));
            corpseBlipColor = Config.Bind<Color>(colorSettings, "Corpse Blip Color", new Color(0.5f, 0.5f, 0.5f));
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
            if (!isShortcutDown && radarEnableShortCutConfig.Value.IsDown())
            {
                radarEnableConfig.Value = !radarEnableConfig.Value;
                isShortcutDown = true;
            }

            if (!radarEnableShortCutConfig.Value.IsDown())
            {
                isShortcutDown = false;
            }

            // // enable corpse shortcut process
            if (!isCorpseShortcutDown && radarEnableCorpseShortCutConfig.Value.IsDown())
            {
                radarEnableCorpseConfig.Value = !radarEnableCorpseConfig.Value;
                isCorpseShortcutDown = true;
            }

            if (!radarEnableShortCutConfig.Value.IsDown())
            {
                isCorpseShortcutDown = false;
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

    public class Enemy
    {
        public Player enemyPlayer;
        public bool show;
        public bool isDead;
        public GameObject blip;

        public static Vector3 playerPosition;

        public Enemy(Player enemyPlayer)
        {
            this.enemyPlayer = enemyPlayer;
            this.show = false;
            this.isDead = false;
            var blipInstance = UnityEngine.Object.Instantiate(HaloRadar.RadarBliphudPrefab,
                HaloRadar.radarHudBlipBasePosition.position, HaloRadar.radarHudBlipBasePosition.rotation);
            this.blip = blipInstance as GameObject;
            this.blip.transform.parent = HaloRadar.radarHudBlipBasePosition.transform;
            this.blip.transform.SetAsLastSibling();
            this.blip.SetActive(true);
        }

        public static void setPlayerPosition(Vector3 playerPosition)
        {
            Enemy.playerPosition = playerPosition;
        }

        public void Update(bool positionUpdate)
        {
            if (enemyPlayer == null)
                return;

            GameObject enemyObject = enemyPlayer.gameObject;
            float x = enemyObject.transform.position.x - playerPosition.x;
            float z = enemyObject.transform.position.z - playerPosition.z;
            float radarRange = Radar.radarRangeConfig.Value;

            show = x * x + z * z > radarRange * radarRange ? false : true;

            if (!isDead && enemyPlayer.HealthController.IsAlive == isDead)
            {
                isDead = true;
            }

            if (isDead)
            {
                show = Radar.radarEnableCorpseConfig.Value && show;
            }

            // up to here would take 200 ticks, why?

            if (enemyObject.activeInHierarchy)
            {
                float yDifference = enemyObject.transform.position.y - playerPosition.y;
                float totalThreshold = Radar.playerHeight * 1.5f * Radar.radarHeightThresholdeScaleOffsetConfig.Value;

                var blipTransform = blip.transform.Find("Blip/RadarEnemyBlip") as RectTransform;
                var blipImage = blipTransform.GetComponent<Image>();

                if (!show)
                {
                    blipImage.color = new Color(0, 0, 0, 0);
                    return;
                }

                if (isDead)
                {
                    blipImage.sprite = HaloRadar.EnemyBlipDead;
                    blipImage.color = Radar.corpseBlipColor.Value;
                }
                else
                {
                    if (Mathf.Abs(yDifference) <= totalThreshold)
                    {
                        blipImage.sprite = HaloRadar.EnemyBlip;
                    }
                    else if (yDifference > totalThreshold)
                    {
                        blipImage.sprite = HaloRadar.EnemyBlipUp;
                    }
                    else if (yDifference < -totalThreshold)
                    {
                        blipImage.sprite = HaloRadar.EnemyBlipDown;
                    }
                    // set blip color
                    switch (enemyPlayer.Profile.Info.Side)
                    {
                        case EPlayerSide.Savage:
                            switch (enemyPlayer.Profile.Info.Settings.Role)
                            {
                                case WildSpawnType.assault:
                                case WildSpawnType.marksman:
                                case WildSpawnType.assaultGroup:
                                    blipImage.color = Radar.scavBlipColor.Value;
                                    break;
                                default:
                                    blipImage.color = Radar.bossBlipColor.Value;
                                    break;
                            }
                            break;
                        case EPlayerSide.Bear:
                            blipImage.color = Radar.bearBlipColor.Value;
                            break;
                        case EPlayerSide.Usec:
                            blipImage.color = Radar.usecBlipColor.Value;
                            break;
                        default:
                            break;
                    }
                }

                float r = blipImage.color.r, g = blipImage.color.g, b = blipImage.color.b, a = blipImage.color.a;
                float delta_a = 1;
                if (Radar.radarScanInterval.Value > 0.8)
                {
                    float ratio = (Time.time - HaloRadar.radarLastUpdateTime) / Radar.radarScanInterval.Value;
                    delta_a = 1 - ratio * ratio;
                }
                blipImage.color = new Color(r, g, b, a * delta_a);

                float blipSize = Radar.radarBlipSizeConfig.Value * 3f;
                blip.transform.localScale = new Vector3(blipSize, blipSize, blipSize);

                // blip.transform.parent = HaloRadar.radarHudBlipBasePosition.transform;

                // Calculate the position based on the angle and distance
                float distance = Mathf.Sqrt(x * x + z * z);
                // Calculate the offset factor based on the distance
                float offsetRadius = Mathf.Pow(distance / radarRange, 0.4f + Radar.radarDistanceScaleConfig.Value * Radar.radarDistanceScaleConfig.Value / 2.0f);
                // Calculate angle
                // Apply the rotation of the parent transform
                Vector3 rotatedDirection = HaloRadar.radarHudBlipBasePosition.rotation * Vector3.forward;
                float angle = Mathf.Atan2(rotatedDirection.x, rotatedDirection.z) * Mathf.Rad2Deg;
                float angleInRadians = Mathf.Atan2(x, z);

                // Get the scale of the radarHudBlipBasePosition
                Vector3 scale = HaloRadar.radarHudBlipBasePosition.localScale;
                // Multiply the sizeDelta by the scale to account for scaling
                Vector2 scaledSizeDelta = HaloRadar.radarHudBlipBasePosition.sizeDelta;
                scaledSizeDelta.x *= scale.x;
                scaledSizeDelta.y *= scale.y;
                // Calculate the radius of the circular boundary
                float graphicRadius = Mathf.Min(scaledSizeDelta.x, scaledSizeDelta.y) * 0.68f;

                // Set the local position of the blip
                if (positionUpdate)
                {
                    blip.transform.localPosition = new Vector2(
                        Mathf.Sin(angleInRadians - angle * Mathf.Deg2Rad),
                        Mathf.Cos(angleInRadians - angle * Mathf.Deg2Rad))
                        * offsetRadius * graphicRadius;
                }

                Quaternion reverseRotation = Quaternion.Inverse(HaloRadar.radarHudBlipBasePosition.rotation);
                blip.transform.localRotation = reverseRotation;
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

        public static float radarRange = 128; // The range within which enemies are displayed on the radar

        public static float radarLastUpdateTime = 0;
        public float radarInterval = -1;

        public HashSet<Player> enemyList = new HashSet<Player>();
        public List<Enemy> enemyCustomObject = new List<Enemy>();

        public static int count = 0;

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

                radarRange = Radar.radarRangeConfig.Value;

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
                if (!enemyList.Contains(enemyPlayer))
                {
                    UnityEngine.Debug.LogError("Add enemy: " + enemyPlayer.Profile.Info.Nickname);
                    enemyList.Add(enemyPlayer);
                    enemyCustomObject.Add(new Enemy(enemyPlayer));
                }
            }
            return 0;
        }

        private void UpdateRadar(bool positionUpdate = true)
        {
            Enemy.setPlayerPosition(player.Transform.position);
            int nbr = enemyCustomObject.Count;
            for (int i = 0; i < nbr; i++)
            {
                enemyCustomObject[i].Update(positionUpdate);
            }
        }
    }
}