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
    [BepInPlugin("Tyrian.Radar", "Radar", "1.0.0")]
    public class Radar : BaseUnityPlugin {
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
        public static ManualLogSource logger;
        public static float playerHeight = 0f;

        public static Radar Instance {
            get { return instance; }
        }

        private void Awake() {
            logger = Logger;
            logger.LogInfo("Radar Plugin Enabled.");
            if (instance != null && instance != this) {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);

            // Add a custom configuration option for the Apply button
            radarEnabledConfig = Config.Bind("B - Radar Settings", "Radar Enabled", true, "Adds a Radar feature to the undersuit when you wear it.");
            radarScaleOffsetConfig = Config.Bind<float>("B - Radar Settings", "Radar HUD Scale Offset", 1f, new BepInEx.Configuration.ConfigDescription("The Scale Offset for the Radar Hud.", new BepInEx.Configuration.AcceptableValueRange<float>(0.1f, 2f)));
            radarDistanceScaleOffsetConfig = Config.Bind<float>("B - Radar Settings", "Radar HUD Blip Disntance Scale Offset", 1f, new BepInEx.Configuration.ConfigDescription("This scales the blips distances from the player, effectively zooming it in and out.", new BepInEx.Configuration.AcceptableValueRange<float>(0.1f, 2f)));
            radarHeightThresholdeScaleOffsetConfig = Config.Bind<float>("B - Radar Settings", "Radar HUD Blip Height Threshold Offset", 1f, new BepInEx.Configuration.ConfigDescription("This scales the distance threshold for blips turning into up or down arrows depending on enemies height levels.", new BepInEx.Configuration.AcceptableValueRange<float>(1f, 4f)));
            radarOffsetYConfig = Config.Bind<float>("B - Radar Settings", "Radar HUD Y Position Offset", 0f, new BepInEx.Configuration.ConfigDescription("The Y Position Offset for the Radar Hud.", new BepInEx.Configuration.AcceptableValueRange<float>(-2000f, 2000f)));
            radarOffsetXConfig = Config.Bind<float>("B - Radar Settings", "Radar HUD X Position Offset", 0f, new BepInEx.Configuration.ConfigDescription("The X Position Offset for the Radar Hud.", new BepInEx.Configuration.AcceptableValueRange<float>(-2000f, 2000f)));
            radarRangeConfig = Config.Bind<float>("B - Radar Settings", "Radar Range", 128f, new BepInEx.Configuration.ConfigDescription("The range within which enemies are displayed on the radar.", new BepInEx.Configuration.AcceptableValueRange<float>(32f, 512f)));
        }

        private void Update() {
            if (!MapLoaded())
                return;

            gameWorld = Singleton<GameWorld>.Instance;
            player = gameWorld.MainPlayer;
            if (gameWorld == null || player == null)
                return;

            GameObject gamePlayerObject = player.gameObject;
            HaloRadar haloRadar = gamePlayerObject.GetComponent<HaloRadar>();
            if (radarEnabledConfig.Value && haloRadar == null) {
                gamePlayerObject.AddComponent<HaloRadar>();
            } else if (!radarEnabledConfig.Value && haloRadar != null) {
                Destroy(haloRadar);
            }
        }
    }

    public class HaloRadar : MonoBehaviour {
        public static GameWorld gameWorld;
        public static Player player;
        public static Object RadarhudPrefab { get; private set; }
        public static Object RadarBliphudPrefab { get; private set; }
        public static AssetBundle radarBundle;
        public static GameObject radarHud;
        public static GameObject radarBlipHud;
        public static GameObject playerCamera;
        private Player[] enemyList;

        private Dictionary<Player, GameObject> enemyBlips;

        public static GameObject radarHudBlipParent;
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

        private void Start() {
            enemyList = new Player[0];
            enemyBlips = new Dictionary<Player, GameObject>();
            // Create our prefabs from our bundles and shit.
            if (RadarhudPrefab == null) {
                String haloRadarHUD = Path.Combine(Environment.CurrentDirectory, "BepInEx/plugins/radar/radarhud.bundle");
                if (!File.Exists(haloRadarHUD))
                    return;
                radarBundle = AssetBundle.LoadFromFile(haloRadarHUD);
                if (radarBundle == null)
                    return;
                RadarhudPrefab = radarBundle.LoadAsset("Assets/Examples/Halo Reach/Hud/RadarHUD.prefab");
                RadarBliphudPrefab = radarBundle.LoadAsset("Assets/Examples/Halo Reach/Hud/RadarBlipHUD.prefab");
            }
        }

        private void Update() {
            if (MapLoaded()) {
                gameWorld = Singleton<GameWorld>.Instance;
                if (gameWorld.MainPlayer != null) {
                    player = gameWorld.MainPlayer;
                    playerTransform = player.Transform;
                    Renderer[] renderers = player.GetComponentsInChildren<Renderer>();
                    float minBoundsY = float.MaxValue;
                    float maxBoundsY = float.MinValue;
                    foreach (Renderer renderer in renderers) {
                        Bounds bounds = renderer.bounds;
                        if (bounds.min.y < minBoundsY)
                            minBoundsY = bounds.min.y;
                        if (bounds.max.y > maxBoundsY)
                            maxBoundsY = bounds.max.y;
                    }
                    Radar.playerHeight = maxBoundsY - minBoundsY;
                }

                if (playerCamera == null) {
                    playerCamera = GameObject.Find("FPS Camera");
                }

                if (playerCamera != null) {
                    if (Radar.radarEnabledConfig.Value) {
                        if (radarHud == null) {
                            var radarHudBase = Instantiate(RadarhudPrefab, playerCamera.transform.position, playerCamera.transform.rotation);
                            radarHud = radarHudBase as GameObject;
                            radarHud.transform.parent = playerCamera.transform;
                            radarHudBasePosition = radarHud.transform.Find("Radar") as RectTransform;
                            radarHudBlipBasePosition = radarHud.transform.Find("Radar/RadarBorder") as RectTransform;
                            radarHudPulse = radarHud.transform.Find("Radar/RadarPulse") as RectTransform;
                            radarScaleStart = radarHudBasePosition.localScale;
                            radarPositionYStart = radarHudBasePosition.position.y;
                            radarPositionXStart = radarHudBasePosition.position.x;
                            StartPulseAnimation();
                        }
                        if (!radarHud.activeSelf) {
                            radarHud.SetActive(true);
                        }
                        if (radarHudBasePosition.position.y != radarPositionYStart + Radar.radarOffsetYConfig.Value
                         || radarHudBasePosition.position.x != radarPositionXStart + Radar.radarOffsetXConfig.Value) {
                            radarHudBasePosition.position = new Vector2(radarPositionYStart + Radar.radarOffsetYConfig.Value, radarPositionXStart + Radar.radarOffsetXConfig.Value);
                        }
                        if (radarHudBasePosition.localScale.y != radarScaleStart.y * Radar.radarScaleOffsetConfig.Value
                         && radarHudBasePosition.localScale.x != radarScaleStart.x * Radar.radarScaleOffsetConfig.Value) {
                            radarHudBasePosition.localScale = new Vector2(radarScaleStart.y * Radar.radarScaleOffsetConfig.Value, radarScaleStart.x * Radar.radarScaleOffsetConfig.Value);
                        }

                        if (radarHudBlipParent == null) {
                            radarHudBlipParent = new GameObject("BlipParent");
                            radarHudBlipParent.transform.parent = playerCamera.transform;
                            radarHudBlipParent.transform.SetAsLastSibling(); // If necessary, set to render on top
                        }
                        // sync position
                        radarHudBlipParent.transform.position = radarHudBlipBasePosition.position;

                        radarRange = Radar.radarRangeConfig.Value;
                        UpdateEnemyObjects();
                    } else if (radarHud != null) {
                        radarHud.SetActive(false);
                    }
                    
                    if (radarHud != null) {
                        radarHudBlipBasePosition.GetComponent<RectTransform>().eulerAngles = new Vector3(0, 0, playerCamera.transform.eulerAngles.y);
                    }
                }
            }
        }

        private void StartPulseAnimation() {
            // Stop any previous pulse coroutine
            if (pulseCoroutine != null) {
                StopCoroutine(pulseCoroutine);
            }
            // Start the pulse coroutine
            pulseCoroutine = StartCoroutine(PulseCoroutine());
        }

        private IEnumerator PulseCoroutine() {
            while (true) {
                // Scale from 0 to 1 over the animation duration
                float t = 0f;
                while (t < 1f) {
                    t += Time.deltaTime / animationDuration;
                    float scale = Mathf.Lerp(0f, 1f, t);

                    // Apply the scale to all axes
                    radarHudPulse.localScale = new Vector3(scale, scale, scale);

                    yield return null;
                }
                // Reset the scale to 0
                radarHudPulse.localScale = Vector3.zero;
                // Pause for the specified duration
                yield return new WaitForSeconds(pauseDuration);
            }
        }
        private void UpdateEnemyObjects() {
            // Get the current players in gameWorld.AllPlayers and convert to a list
            List<Player> players = gameWorld.AllAlivePlayersList.ToList();

            // Exclude gameWorld.MainPlayer from the players list
            players.Remove(gameWorld.MainPlayer);

            // Resize the enemyObjects array to match the number of players
            enemyList = new Player[players.Count];

            // Add the player game objects to the enemyObjects array
            for (int i = 0; i < players.Count; i++) {
                enemyList[i] = players[i];
            }

            List<Player> activeEnemyObjects = new List<Player>();
            List<Player> blipsToRemove = new List<Player>();
            foreach (Player enemyPlayer in enemyList) {
                GameObject enemyObject = enemyPlayer.gameObject;
                // Calculate the relative position of the enemy object
                Vector3 relativePosition = enemyObject.transform.position - playerTransform.position;

                // Check if the enemy is within the radar range
                if (relativePosition.magnitude <= radarRange) {
                    // Update blips on the radar for the enemies.
                    activeEnemyObjects.Add(enemyPlayer);
                } else {
                    // Remove the blip if the enemy is outside the radar range
                    blipsToRemove.Add(enemyPlayer);
                }
            }
            // Remove blips for any enemy objects that are no longer active
            RemoveInactiveEnemyBlips(activeEnemyObjects, blipsToRemove);
            foreach (Player enemyPlayer in activeEnemyObjects) {
                // Update blips on the radar for the enemies.
                UpdateBlips(enemyPlayer);
            }
            foreach (Player enemyPlayer in blipsToRemove) {
                // Remove out of range blips on the radar for the enemies.
                RemoveOutOfRangeBlip(enemyPlayer);
            }
            foreach (var enemyBlip in enemyBlips.Keys.ToList()) {
                if (!activeEnemyObjects.Contains(enemyBlip)) {
                    RemoveOutOfRangeBlip(enemyBlip);
                }
            }
        }
        private void UpdateBlips(Player enemyPlayer) {
            if (enemyPlayer == null)
                return;

            GameObject enemyObject = enemyPlayer.gameObject;
            if (enemyObject.activeInHierarchy) {
                float x = enemyObject.transform.position.x - player.Transform.position.x;
                float z = enemyObject.transform.position.z - player.Transform.position.z;

                // Check if a blip already exists for the enemy object
                if (!enemyBlips.TryGetValue(enemyPlayer, out GameObject blip)) {
                    // Instantiate a blip game object and set its position relative to the radar HUD
                    var radarHudBlipBase = Instantiate(RadarBliphudPrefab, radarHudBlipBasePosition.position, radarHudBlipBasePosition.rotation);
                    radarBlipHud = radarHudBlipBase as GameObject;
                    radarBlipHud.transform.parent = radarHudBlipParent.transform;
                    EnemyBlip = radarBundle.LoadAsset<Sprite>("EnemyBlip");
                    EnemyBlipUp = radarBundle.LoadAsset<Sprite>("EnemyBlipUp");
                    EnemyBlipDown = radarBundle.LoadAsset<Sprite>("EnemyBlipDown");
                    // Add the enemy object and its blip to the dictionary
                    enemyBlips.Add(enemyPlayer, radarBlipHud);
                    blip = radarBlipHud;
                }

                if (blip != null) {
                    // Update the blip's image component based on y distance to enemy.
                    radarHudBlip = blip.transform.Find("Blip/RadarEnemyBlip") as RectTransform;
                    blipImage = radarHudBlip.GetComponent<Image>();
                    float yDifference = enemyObject.transform.position.y - player.Transform.position.y;
                    float totalThreshold = (Radar.playerHeight + Radar.playerHeight / 2f) * Radar.radarHeightThresholdeScaleOffsetConfig.Value;
                    if (Mathf.Abs(yDifference) <= totalThreshold) {
                        blipImage.sprite = EnemyBlip;
                    } else if (yDifference > totalThreshold) {
                        blipImage.sprite = EnemyBlipUp;
                    } else if (yDifference < -totalThreshold) {
                        blipImage.sprite = EnemyBlipDown;
                    }

                    switch (enemyPlayer.Profile.Info.Settings.Role) {
                        case WildSpawnType.pmcBot:
                        case WildSpawnType.exUsec:
                            blipImage.color = Color.yellow;
                            break;
                        case WildSpawnType.assault:
                        case WildSpawnType.marksman:
                            blipImage.color = Color.green;
                            break;
                        default:
                            blipImage.color = Color.red;
                            break;
                    }

                    blip.transform.parent = radarHudBlipBasePosition.transform;
                    // Apply the scale to the blip

                    // Apply the rotation of the parent transform
                    Quaternion parentRotation = radarHudBlipBasePosition.rotation;
                    Vector3 rotatedDirection = parentRotation * Vector3.forward;

                    // Calculate the angle based on the rotated direction
                    float angle = Mathf.Atan2(rotatedDirection.x, rotatedDirection.z) * Mathf.Rad2Deg;

                    // Calculate the position based on the angle and distance
                    float distance = Mathf.Sqrt(x * x + z * z);
                    // Calculate the offset factor based on the distance
                    float offsetFactor = Mathf.Clamp(distance / radarRange, 2f, 4f);
                    float offsetDistance = (distance * offsetFactor) * Radar.radarDistanceScaleOffsetConfig.Value;
                    float angleInRadians = Mathf.Atan2(x, z);
                    Vector2 position = new Vector2(Mathf.Sin(angleInRadians - angle * Mathf.Deg2Rad), Mathf.Cos(angleInRadians - angle * Mathf.Deg2Rad)) * offsetDistance;

                    // Get the scale of the radarHudBlipBasePosition
                    Vector3 scale = radarHudBlipBasePosition.localScale;
                    // Multiply the sizeDelta by the scale to account for scaling
                    Vector2 scaledSizeDelta = radarHudBlipBasePosition.sizeDelta;
                    scaledSizeDelta.x *= scale.x;
                    scaledSizeDelta.y *= scale.y;
                    // Calculate the radius of the circular boundary
                    float radius = Mathf.Min(scaledSizeDelta.x, scaledSizeDelta.y) * 0.5f;
                    // Clamp the position within the circular boundary
                    float distanceFromCenter = position.magnitude;
                    if (distanceFromCenter > radius)
                    {
                        position = position.normalized * radius;
                    }
                    // Set the local position of the blip
                    blip.transform.localPosition = position;
                    Quaternion reverseRotation = Quaternion.Inverse(radarHudBlipBasePosition.rotation);
                    blip.transform.localRotation = reverseRotation;
                }
            } else {
                // Remove the inactive enemy blips from the dictionary and destroy the blip game objects
                if (enemyBlips.TryGetValue(enemyPlayer, out GameObject blip)) {
                    enemyBlips.Remove(enemyPlayer);
                    Destroy(blip);
                }
            }
        }

        private void RemoveOutOfRangeBlip(Player enemyPlayer)
        {
            if (enemyBlips.ContainsKey(enemyPlayer))
            {
                // Remove the blip game object from the scene
                GameObject blip = enemyBlips[enemyPlayer];
                enemyBlips.Remove(enemyPlayer);
                Destroy(blip);
            }
        }
        private void RemoveInactiveEnemyBlips(List<Player> activeEnemyObjects, List<Player> blipsToRemove)
        {
            // Create a list to store the enemy objects that need to be removed
            List<Player> enemiesToRemove = new List<Player>();

            // Iterate through the enemyBlips dictionary
            foreach (var enemyBlip in enemyBlips) {
                Player enemyPlayer = enemyBlip.Key;

                // Check if the enemy object is not in the activeEnemyObjects list
                if (!activeEnemyObjects.Contains(enemyPlayer))
                {
                    // Add the enemy object to the enemiesToRemove list
                    enemiesToRemove.Add(enemyPlayer);
                }
            }

            // Iterate through the enemiesToRemove list and remove blips
            foreach (Player enemyPlayer in enemiesToRemove)
            {
                // Check if the enemy object exists in the enemyBlips dictionary
                if (enemyBlips.TryGetValue(enemyPlayer, out GameObject blip))
                {
                    // Remove the blip game object from the scene
                    enemyBlips.Remove(enemyPlayer);
                    Destroy(blip);
                }
            }
        }
    }
}