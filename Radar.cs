using EFT;
using System;
using System.IO;
using System.Linq;
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
    [BepInPlugin("Tyrian.Radar", "Radar", "1.0.3")]
    public class Radar : BaseUnityPlugin
    {
        private static GameWorld gameWorld;
        public static bool MapLoaded() => Singleton<GameWorld>.Instantiated;
        public static Player player;
        public static Radar instance;
        public static Dictionary<GameObject, HashSet<Material>> objectsMaterials = new Dictionary<GameObject, HashSet<Material>>();
        public static ConfigEntry<bool> radarEnabledConfig;
        public static ConfigEntry<float> radarScaleOffsetConfig;
        public static ConfigEntry<float> radarDistanceScaleOffsetConfig;
        public static ConfigEntry<float> radarHeightThresholdeScaleOffsetConfig;
        public static ConfigEntry<float> radarOffsetYConfig;
        public static ConfigEntry<float> radarOffsetXConfig;
        public static ConfigEntry<float> radarRangeConfig;
        public static ConfigEntry<float> radarScanInterval;
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
            radarEnabledConfig = Config.Bind("B - Radar Settings", "Radar Enabled", true, "Adds a Radar feature to the undersuit when you wear it.");
            radarScaleOffsetConfig = Config.Bind<float>("B - Radar Settings", "Radar HUD Scale Offset", 1f, new BepInEx.Configuration.ConfigDescription("The Scale Offset for the Radar Hud.", new BepInEx.Configuration.AcceptableValueRange<float>(0.0f, 1f)));
            radarDistanceScaleOffsetConfig = Config.Bind<float>("B - Radar Settings", "Radar HUD Blip Disntance Scale Offset", 0f, new BepInEx.Configuration.ConfigDescription("This scales the blips distances from the player, effectively zooming it in and out.", new BepInEx.Configuration.AcceptableValueRange<float>(0.1f, 2f)));
            radarHeightThresholdeScaleOffsetConfig = Config.Bind<float>("B - Radar Settings", "Radar HUD Blip Height Threshold Offset", 1f, new BepInEx.Configuration.ConfigDescription("This scales the distance threshold for blips turning into up or down arrows depending on enemies height levels.", new BepInEx.Configuration.AcceptableValueRange<float>(1f, 4f)));
            radarOffsetYConfig = Config.Bind<float>("B - Radar Settings", "Radar HUD Y Position Offset", 0f, new BepInEx.Configuration.ConfigDescription("The Y Position Offset for the Radar Hud.", new BepInEx.Configuration.AcceptableValueRange<float>(-2000f, 2000f)));
            radarOffsetXConfig = Config.Bind<float>("B - Radar Settings", "Radar HUD X Position Offset", 0f, new BepInEx.Configuration.ConfigDescription("The X Position Offset for the Radar Hud.", new BepInEx.Configuration.AcceptableValueRange<float>(-2000f, 2000f)));
            radarRangeConfig = Config.Bind<float>("B - Radar Settings", "Radar Range", 128f, new BepInEx.Configuration.ConfigDescription("The range within which enemies are displayed on the radar.", new BepInEx.Configuration.AcceptableValueRange<float>(32f, 512f)));
            radarScanInterval = Config.Bind<float>("B - Radar Settings", "Radar Scan Interval", 1f, new BepInEx.Configuration.ConfigDescription("The interval between two scans.", new BepInEx.Configuration.AcceptableValueRange<float>(0f,30f)));
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

            if (radarEnabledConfig.Value && haloRadar == null)
            {
                // Add the HaloRadar component if it doesn't exist.
                gamePlayerObject.AddComponent<HaloRadar>();
            }
            else if (!radarEnabledConfig.Value && haloRadar != null)
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

        private Dictionary<Player, GameObject> enemyBlips;
        public static RectTransform radarHudBlipBasePosition { get; private set; }
        public static RectTransform radarHudBasePosition { get; private set; }
        public static RectTransform radarHudPulse { get; private set; }
        public static RectTransform radarHudBlip { get; private set; }
        public static Image blipImage;
        public static Sprite EnemyBlip;
        public static Sprite EnemyBlipDown;
        public static Sprite EnemyBlipUp;
        public static Coroutine pulseCoroutine;
        public static float animationDuration = 1f;
        public static float pauseDuration = 4f;
        public static Vector3 radarScaleStart;
        public static float radarPositionYStart = 0f;
        public static float radarPositionXStart = 0f;
        public static bool MapLoaded() => Singleton<GameWorld>.Instantiated;
        public BifacialTransform playerTransform; // Player's transform component

        public float radarRange = 128; // The range within which enemies are displayed on the radar

        public float radarLastUpdateTime = 0;
        public float radarInterval = 0;
        public List<Player> activePlayerOnRadar;

        private void Start()
        {
            enemyBlips = new Dictionary<Player, GameObject>();
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
            }
        }

        private void Update()
        {
            if (MapLoaded())
            {
                gameWorld = Singleton<GameWorld>.Instance;
                if (gameWorld.MainPlayer != null)
                {
                    player = gameWorld.MainPlayer;
                    playerTransform = player.Transform;
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
                }

                if (playerCamera == null)
                {
                    playerCamera = GameObject.Find("FPS Camera");
                }

                if (playerCamera != null)
                {
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
                        radarHudBasePosition.localScale = new Vector2(radarScaleStart.y * Radar.radarScaleOffsetConfig.Value, radarScaleStart.x * Radar.radarScaleOffsetConfig.Value);
                        radarHud.SetActive(true);
                        StartPulseAnimation();
                    }

                    radarHudBasePosition.position = new Vector2(radarPositionYStart + Radar.radarOffsetYConfig.Value, radarPositionXStart + Radar.radarOffsetXConfig.Value);
                    radarHudBasePosition.localScale = new Vector2(radarScaleStart.y * Radar.radarScaleOffsetConfig.Value, radarScaleStart.x * Radar.radarScaleOffsetConfig.Value);

                    radarRange = Radar.radarRangeConfig.Value;
                    //UpdateEnemyObjects();
                    UpdateRadar(UpdateActivePlayerOnRadar());
                    if (radarHud != null)
                    {
                        radarHudBlipBasePosition.GetComponent<RectTransform>().eulerAngles = new Vector3(0, 0, playerCamera.transform.eulerAngles.y);
                    }

                    if (radarInterval != Radar.radarScanInterval.Value)
                    {
                        radarInterval = Radar.radarScanInterval.Value;
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
            float interval = Radar.radarScanInterval.Value - 1;
            if (interval < 0)
            {
                interval = 0;
            }
            while (true)
            {
                // Scale from 0 to 1 over the animation duration
                float t = 0f;
                while (t < 1f)
                {
                    t += Time.deltaTime / animationDuration;
                    float scale = Mathf.Lerp(0f, 1f, Mathf.Sqrt(1 - Mathf.Pow(1 - t, 4f)));

                    // Apply the scale to all axes
                    radarHudPulse.localScale = new Vector3(scale, scale, scale);
                    Image img = radarHudPulse.GetComponent<Image>();
                    img.color = new Color(img.color.r, img.color.g, img.color.b, 1 - t);

                    yield return null;
                }
                // Reset the scale to 0
                radarHudPulse.localScale = Vector3.zero;
                // Pause for the specified duration
                yield return new WaitForSeconds(interval);
            }
        }

        private bool UpdateActivePlayerOnRadar()
        {
            if (Time.time - radarLastUpdateTime < Radar.radarScanInterval.Value)
            {
                return false;
            }
            else
            {
                radarLastUpdateTime = Time.time;
            }
            activePlayerOnRadar = new List<Player>();
            // Get the current players in gameWorld.AllAlivePlayersList and convert to a list
            List<Player> players = gameWorld.AllAlivePlayersList.ToList();
            // Exclude gameWorld.MainPlayer from the players list
            players.Remove(gameWorld.MainPlayer);
            foreach (Player enemyPlayer in players)
            {
                // Calculate the relative position of the enemy object
                Vector3 relativePosition = enemyPlayer.gameObject.transform.position - playerTransform.position;
                // Check if the enemy is within the radar range and make sure it's alive
                if (relativePosition.magnitude <= radarRange && enemyPlayer.HealthController.IsAlive)
                {
                    // Update blips on the radar for the enemies.
                    activePlayerOnRadar.Add(enemyPlayer);
                }
            }
            foreach (var enemyBlip in enemyBlips)
            {
                Destroy(enemyBlip.Value);
            }
            enemyBlips.Clear();
            return true;
        }

        private void UpdateRadar(bool positionUpdate)
        {
            foreach (Player enemyPlayer in activePlayerOnRadar)
            {
                UpdateBlips(enemyPlayer, positionUpdate);
            }
        }

        private void UpdateBlips(Player enemyPlayer, bool positionUpdate)
        {
            if (enemyPlayer == null)
                return;

            GameObject enemyObject = enemyPlayer.gameObject;
            if (enemyObject.activeInHierarchy)
            {
                float x = enemyObject.transform.position.x - player.Transform.position.x;
                float z = enemyObject.transform.position.z - player.Transform.position.z;

                // Check if a blip already exists for the enemy object
                if (!enemyBlips.TryGetValue(enemyPlayer, out GameObject blip))
                {
                    // Instantiate a blip game object and set its position relative to the radar HUD
                    var radarHudBlipBase = Instantiate(RadarBliphudPrefab, radarHudBlipBasePosition.position, radarHudBlipBasePosition.rotation);
                    radarBlipHud = radarHudBlipBase as GameObject;
                    radarBlipHud.transform.parent = radarHudBlipBasePosition.transform;
                    radarBlipHud.transform.SetAsLastSibling();

                    // Add the enemy object and its blip to the dictionary
                    enemyBlips.Add(enemyPlayer, radarBlipHud);
                    blip = radarBlipHud;
                }

                if (blip != null)
                {
                    // Update the blip's image component based on y distance to enemy.
                    radarHudBlip = blip.transform.Find("Blip/RadarEnemyBlip") as RectTransform;
                    blipImage = radarHudBlip.GetComponent<Image>();
                    float yDifference = enemyObject.transform.position.y - player.Transform.position.y;
                    float totalThreshold = (Radar.playerHeight + Radar.playerHeight / 2f) * Radar.radarHeightThresholdeScaleOffsetConfig.Value;
                    if (Mathf.Abs(yDifference) <= totalThreshold)
                    {
                        blipImage.sprite = EnemyBlip;
                    } else if (yDifference > totalThreshold)
                    {
                        blipImage.sprite = EnemyBlipUp;
                    } else if (yDifference < -totalThreshold)
                    {
                        blipImage.sprite = EnemyBlipDown;
                    }

                    switch (enemyPlayer.Profile.Info.Side)
                    {
                        case EPlayerSide.Savage:
                            switch (enemyPlayer.Profile.Info.Settings.Role)
                            {
                                case WildSpawnType.assault:
                                case WildSpawnType.marksman:
                                    blipImage.color = Color.green;
                                    break;
                                default:
                                    blipImage.color = Color.red;
                                    break;
                            }
                            break;
                        case EPlayerSide.Bear:
                        case EPlayerSide.Usec:
                            blipImage.color = Color.yellow;
                            break;
                        default:
                            break;
                    }
                    float r = blipImage.color.r, g = blipImage.color.g, b = blipImage.color.b;
                    float a = 1;
                    if (Radar.radarScanInterval.Value > 0)
                    {
                        a = 1 - (Time.time - radarLastUpdateTime) / Radar.radarScanInterval.Value;
                    }
                    blipImage.color = new Color(r, g, b, a);

                    // Calculate the position based on the angle and distance
                    float distance = Mathf.Sqrt(x * x + z * z);
                    // Calculate the offset factor based on the distance
                    float offsetRadius = Mathf.Pow(distance / radarRange, 0.4f + Radar.radarDistanceScaleOffsetConfig.Value * Radar.radarDistanceScaleOffsetConfig.Value / 2.0f);
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
                }
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