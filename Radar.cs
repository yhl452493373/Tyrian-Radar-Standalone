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

namespace Radar
{
    [BepInPlugin("Tyrian.Radar", "Radar", "1.0.8")]
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
        public static bool isShortcutDown = false;

        public static ConfigEntry<float> radarSizeConfig;
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
        public static float playerHeight = 0f;

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

            radarSizeConfig = Config.Bind<float>(radarSettings, "Radar HUD Size", 1f, new ConfigDescription("The Scale Offset for the Radar Hud.", new AcceptableValueRange<float>(0.0f, 1f)));
            radarDistanceScaleConfig = Config.Bind<float>(radarSettings, "Radar HUD Blip Disntance Scale Offset", 0.7f, new ConfigDescription("This scales the blips distances from the player, effectively zooming it in and out.", new AcceptableValueRange<float>(0.1f, 2f)));
            radarHeightThresholdeScaleOffsetConfig = Config.Bind<float>(radarSettings, "Radar HUD Blip Height Threshold Offset", 1f, new ConfigDescription("This scales the distance threshold for blips turning into up or down arrows depending on enemies height levels.", new AcceptableValueRange<float>(1f, 4f)));
            radarOffsetYConfig = Config.Bind<float>(radarSettings, "Radar HUD Y Position Offset", 0f, new ConfigDescription("The Y Position Offset for the Radar Hud.", new AcceptableValueRange<float>(-2000f, 2000f)));
            radarOffsetXConfig = Config.Bind<float>(radarSettings, "Radar HUD X Position Offset", 0f, new ConfigDescription("The X Position Offset for the Radar Hud.", new AcceptableValueRange<float>(-2000f, 2000f)));
            radarRangeConfig = Config.Bind<float>(radarSettings, "Radar Range", 128f, new ConfigDescription("The range within which enemies are displayed on the radar.", new AcceptableValueRange<float>(32f, 512f)));
            radarScanInterval = Config.Bind<float>(radarSettings, "Radar Scan Interval", 1f, new ConfigDescription("The interval between two scans.", new AcceptableValueRange<float>(0f, 30f)));
            
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

            if (!isShortcutDown && radarEnableShortCutConfig.Value.IsDown())
            {
                radarEnableConfig.Value = !radarEnableConfig.Value;
                isShortcutDown = true;
            }

            if (!radarEnableShortCutConfig.Value.IsDown())
            {
                isShortcutDown = false;
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

        private Dictionary<Player, GameObject> enemyBlips = new Dictionary<Player, GameObject>();
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

        public float radarRange = 128; // The range within which enemies are displayed on the radar

        public float radarLastUpdateTime = 0;
        public float radarInterval = -1;
        public List<Player> activePlayerOnRadar = new List<Player>();
        public List<Player> deadPlayerOnRadar = new List<Player>();

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
                    // no main player
                    return;
                }

                player = gameWorld.MainPlayer;
                Renderer[] renderers = player.GetComponentsInChildren<Renderer>();
                float minBoundsY = float.MaxValue;
                float maxBoundsY = float.MinValue;
                foreach (Renderer renderer in renderers)
                {
                    Bounds bounds = renderer.bounds;
                    if (bounds.min.y < minBoundsY)
                        minBoundsY = bounds.min.y;
                    if (bounds.max.y > maxBoundsY)
                        maxBoundsY = bounds.max.y;
                }
                Radar.playerHeight = maxBoundsY - minBoundsY;

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

                bool updatePosition = UpdateActivePlayerOnRadar();
                UpdateRadar(updatePosition);

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

        private bool UpdateActivePlayerOnRadar()
        {
            if (gameWorld == null)
            {
                return false;
            }
            if (Time.time - radarLastUpdateTime < Radar.radarScanInterval.Value)
            {
                // clean blip if enemyPlayer has been cleared
                foreach (var enemyBlip in enemyBlips)
                {
                    if (enemyBlip.Key == null)
                    {
                        Destroy(enemyBlip.Value);
                    }
                }
                return false;
            }
            else
            {
                radarLastUpdateTime = Time.time;
            }

            // clear blips
            foreach (var enemyBlip in enemyBlips)
            {
                Destroy(enemyBlip.Value);
            }
            enemyBlips.Clear();

            activePlayerOnRadar.Clear();
            deadPlayerOnRadar.Clear();
            
            IEnumerable<Player> allPlayers = gameWorld.AllPlayersEverExisted;
            foreach (Player enemyPlayer in allPlayers)
            {
                if (enemyPlayer == null || enemyPlayer == player)
                {
                    continue;
                }

                Vector3 relativePosition = enemyPlayer.gameObject.transform.position - player.Transform.position;
                if (relativePosition.magnitude <= radarRange)
                {
                    if (enemyPlayer.HealthController.IsAlive)
                    {
                        activePlayerOnRadar.Add(enemyPlayer);
                    }
                    else
                    {
                        deadPlayerOnRadar.Add(enemyPlayer);
                    }
                    enemyBlips.Add(enemyPlayer, GetBlip());
                }
            }
            
            return true;
        }

        private void UpdateRadar(bool positionUpdate)
        {
            // plot corpse first
            if (Radar.radarEnableCorpseConfig.Value)
            {
                foreach (Player enemyPlayer in deadPlayerOnRadar)
                {
                    UpdateBlips(enemyPlayer, positionUpdate, true);
                }
            }
            // then alive players
            foreach (Player enemyPlayer in activePlayerOnRadar)
            {
                UpdateBlips(enemyPlayer, positionUpdate);
            }
        }

        private GameObject GetBlip()
        {
            var radarHudBlipBase = Instantiate(RadarBliphudPrefab, radarHudBlipBasePosition.position, radarHudBlipBasePosition.rotation);
            radarBlipHud = radarHudBlipBase as GameObject;
            radarBlipHud.transform.parent = radarHudBlipBasePosition.transform;
            radarBlipHud.transform.SetAsLastSibling();
            return radarBlipHud;
        }

        private void UpdateBlips(Player enemyPlayer, bool positionUpdate, bool isDead = false)
        {
            if (enemyPlayer == null)
                return;

            GameObject enemyObject = enemyPlayer.gameObject;
            if (enemyObject.activeInHierarchy)
            {
                GameObject blip = enemyBlips[enemyPlayer];
                // Update the blip's image component based on y distance to enemy.
                radarHudBlip = blip.transform.Find("Blip/RadarEnemyBlip") as RectTransform;
                blipImage = radarHudBlip.GetComponent<Image>();
                float x = enemyObject.transform.position.x - player.Transform.position.x;
                float z = enemyObject.transform.position.z - player.Transform.position.z;
                float yDifference = enemyObject.transform.position.y - player.Transform.position.y;
                float totalThreshold = (Radar.playerHeight + Radar.playerHeight / 2f) * Radar.radarHeightThresholdeScaleOffsetConfig.Value;

                if (isDead)
                {
                    blipImage.sprite = EnemyBlipDead;
                    blipImage.color = Radar.corpseBlipColor.Value;
                }
                else
                {
                    if (Mathf.Abs(yDifference) <= totalThreshold)
                    {
                        blipImage.sprite = EnemyBlip;
                    }
                    else if (yDifference > totalThreshold)
                    {
                        blipImage.sprite = EnemyBlipUp;
                    }
                    else if (yDifference < -totalThreshold)
                    {
                        blipImage.sprite = EnemyBlipDown;
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
                if (Radar.radarScanInterval.Value > 0)
                {
                    float ratio = (Time.time - radarLastUpdateTime) / Radar.radarScanInterval.Value;
                    delta_a = 1 - ratio * ratio;
                }
                blipImage.color = new Color(r, g, b, a * delta_a);

                blip.transform.parent = radarHudBlipBasePosition.transform;

                // Calculate the position based on the angle and distance
                float distance = Mathf.Sqrt(x * x + z * z);
                // Calculate the offset factor based on the distance
                float offsetRadius = Mathf.Pow(distance / radarRange, 0.4f + Radar.radarDistanceScaleConfig.Value * Radar.radarDistanceScaleConfig.Value / 2.0f);
                // Calculate angle
                // Apply the rotation of the parent transform
                Vector3 rotatedDirection = radarHudBlipBasePosition.rotation * Vector3.forward;
                float angle = Mathf.Atan2(rotatedDirection.x, rotatedDirection.z) * Mathf.Rad2Deg;
                float angleInRadians = Mathf.Atan2(x, z);

                // Get the scale of the radarHudBlipBasePosition
                Vector3 scale = radarHudBlipBasePosition.localScale;
                // Multiply the sizeDelta by the scale to account for scaling
                Vector2 scaledSizeDelta = radarHudBlipBasePosition.sizeDelta;
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

                Quaternion reverseRotation = Quaternion.Inverse(radarHudBlipBasePosition.rotation);
                blip.transform.localRotation = reverseRotation;
            } else {
                // Remove the inactive enemy blips from the dictionary and destroy the blip game objects
                RemoveBlip(enemyPlayer);
            }
        }

        private void RemoveBlip(Player enemyPlayer)
        {
            if (enemyBlips.ContainsKey(enemyPlayer))
            {
                // Remove the blip game object from the scene
                GameObject blip = enemyBlips[enemyPlayer];
                enemyBlips.Remove(enemyPlayer);
                Destroy(blip);
            }
        }
    }
}