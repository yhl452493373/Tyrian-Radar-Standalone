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

namespace HaloArmour
{
    [BepInPlugin("Tyrian.Radar", "Radar", "1.0.0")]
    public class Radar : BaseUnityPlugin
    {
        private static GameWorld gameWorld;
        public static bool MapLoaded() => Singleton<GameWorld>.Instantiated;
        public static Player Player;
        public static Radar instance;
        public static Dictionary<GameObject, HashSet<Material>> objectsMaterials = new Dictionary<GameObject, HashSet<Material>>();
        public static ConfigEntry<bool> radarEnabledConfig;
        public static ConfigEntry<float> radarScaleOffsetConfig;
        public static ConfigEntry<float> radarOffsetYConfig;
        public static ConfigEntry<float> radarOffsetXConfig;
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
            radarScaleOffsetConfig = Config.Bind<float>("B - Radar Settings", "Radar HUD Scale Offset", 1f, new BepInEx.Configuration.ConfigDescription("The Scale Offset for the Radar Hud.", new BepInEx.Configuration.AcceptableValueRange<float>(0.1f, 2f)));
            radarOffsetYConfig = Config.Bind<float>("B - Radar Settings", "Radar HUD Y Position Offset", 0f, new BepInEx.Configuration.ConfigDescription("The Y Position Offset for the Radar Hud.", new BepInEx.Configuration.AcceptableValueRange<float>(-2000f, 2000f)));
            radarOffsetXConfig = Config.Bind<float>("B - Radar Settings", "Radar HUD X Position Offset", 0f, new BepInEx.Configuration.ConfigDescription("The X Position Offset for the Radar Hud.", new BepInEx.Configuration.AcceptableValueRange<float>(-2000f, 2000f)));
        }

        private void Update()
        {
            if (!MapLoaded())
                return;

            gameWorld = Singleton<GameWorld>.Instance;
            if (gameWorld == null || gameWorld.MainPlayer == null)
                return;

            Player = gameWorld.MainPlayer;
            if (Player == null)
                return;

            if (radarEnabledConfig.Value)
            {
                    GameObject gamePlayerObject = Player.gameObject;
                    if (gamePlayerObject.GetComponent<HaloRadar>() == null && radarEnabledConfig.Value)
                    {
                        HaloRadar haloRadar = gamePlayerObject.AddComponent<HaloRadar>();
                    }
            }
            else
            {
                    GameObject gamePlayerObject = Player.gameObject;
                    HaloRadar haloRadar = gamePlayerObject.GetComponent<HaloRadar>();
                    if (haloRadar != null)
                    {
                        Destroy(haloRadar);
                    }
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
        private GameObject[] enemyObjects;
        private Dictionary<GameObject, GameObject> enemyBlips;
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

        private void Start()
        {
            enemyObjects = new GameObject[0];
            enemyBlips = new Dictionary<GameObject, GameObject>();
            // Create our prefabs from our bundles and shit.
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
                }

                if (playerCamera == null)
                {
                    playerCamera = GameObject.Find("FPS Camera");
                }

                if (playerCamera != null)
                {
                    if (Radar.radarEnabledConfig.Value)
                    {
                        if (radarHud == null)
                        {
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
                        if (!radarHud.activeSelf)
                        {
                            radarHud.SetActive(true);
                        }
                        if (radarHudBasePosition.position.y != radarPositionYStart + Radar.radarOffsetYConfig.Value || radarHudBasePosition.position.x != radarPositionXStart + Radar.radarOffsetXConfig.Value)
                        {
                            radarHudBasePosition.position = new Vector2(radarPositionYStart + Radar.radarOffsetYConfig.Value, radarPositionXStart + Radar.radarOffsetXConfig.Value);
                        }
                        if (radarHudBasePosition.localScale.y != radarScaleStart.y * Radar.radarScaleOffsetConfig.Value && radarHudBasePosition.localScale.x != radarScaleStart.x * Radar.radarScaleOffsetConfig.Value)
                        {
                            radarHudBasePosition.localScale = new Vector2(radarScaleStart.y * Radar.radarScaleOffsetConfig.Value, radarScaleStart.x * Radar.radarScaleOffsetConfig.Value);
                        }
                        UpdateEnemyObjects();
                    }
                    else if (radarHud != null)
                    {
                        radarHud.SetActive(false);
                    }
                    if (radarHud != null)
                    {
                        radarHudBlipBasePosition.GetComponent<RectTransform>().eulerAngles = new Vector3(0, 0, playerCamera.transform.eulerAngles.y);
                    }
                }
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
            while (true)
            {
                // Scale from 0 to 1 over the animation duration
                float t = 0f;
                while (t < 1f)
                {
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
        private void UpdateEnemyObjects()
        {
            // Get the current players in gameWorld.AllPlayers and convert to a list
            List<Player> players = gameWorld.AllPlayers.ToList();

            // Exclude gameWorld.MainPlayer from the players list
            players.Remove(gameWorld.MainPlayer);

            // Resize the enemyObjects array to match the number of players
            enemyObjects = new GameObject[players.Count];

            // Add the player game objects to the enemyObjects array
            for (int i = 0; i < players.Count; i++)
            {
                enemyObjects[i] = players[i].gameObject;
            }

            List<GameObject> activeEnemyObjects = new List<GameObject>();
            List<GameObject> blipsToRemove = new List<GameObject>();
            foreach (GameObject enemyObject in enemyObjects)
            {
                // Calculate the relative position of the enemy object
                Vector3 relativePosition = enemyObject.transform.position - playerTransform.position;

                // Check if the enemy is within the radar range
                if (relativePosition.magnitude <= radarRange)
                {
                    // Update blips on the radar for the enemies.
                    activeEnemyObjects.Add(enemyObject);
                }
                else
                {
                    // Remove the blip if the enemy is outside the radar range
                    blipsToRemove.Add(enemyObject);
                }
            }
            // Remove blips for any enemy objects that are no longer active
            RemoveInactiveEnemyBlips(activeEnemyObjects, blipsToRemove);
            foreach (GameObject enemyObject in activeEnemyObjects)
            {
                // Update blips on the radar for the enemies.
                UpdateBlips(enemyObject);
            }
            foreach (GameObject enemyObject in blipsToRemove)
            {
                // Remove out of range blips on the radar for the enemies.
                RemoveOutOfRangeBlip(enemyObject);
            }
            foreach (var enemyBlip in enemyBlips.Keys.ToList())
            {
                if (!activeEnemyObjects.Contains(enemyBlip))
                {
                    RemoveOutOfRangeBlip(enemyBlip);
                }
            }
        }
        private void UpdateBlips(GameObject enemyObject)
        {
            if (enemyObject != null && enemyObject.activeInHierarchy)
            {
                float x = enemyObject.transform.position.x - player.Transform.position.x;
                float z = enemyObject.transform.position.z - player.Transform.position.z;

                // Check if a blip already exists for the enemy object
                if (!enemyBlips.TryGetValue(enemyObject, out GameObject blip))
                {
                    // Instantiate a blip game object and set its position relative to the radar HUD
                    var radarHudBlipBase = Instantiate(RadarBliphudPrefab, radarHudBlipBasePosition.position, radarHudBlipBasePosition.rotation);
                    radarBlipHud = radarHudBlipBase as GameObject;
                    radarBlipHud.transform.parent = radarHudBlipBasePosition.transform;
                    EnemyBlip = radarBundle.LoadAsset<Sprite>("EnemyBlip");
                    EnemyBlipUp = radarBundle.LoadAsset<Sprite>("EnemyBlipUp");
                    EnemyBlipDown = radarBundle.LoadAsset<Sprite>("EnemyBlipDown");
                    // Add the enemy object and its blip to the dictionary
                    enemyBlips.Add(enemyObject, radarBlipHud);
                    blip = radarBlipHud;
                }

                if (blip != null)
                {
                    // Update the blip's image component based on y distance to enemy.
                    radarHudBlip = blip.transform.Find("Blip/RadarEnemyBlip") as RectTransform;
                    blipImage = radarHudBlip.GetComponent<Image>();
                    float yDifference = enemyObject.transform.position.y - player.Transform.position.y;
                    float totalThreshold = Radar.playerHeight + Radar.playerHeight / 2f;
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
                    float offsetDistance = distance * offsetFactor;
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
            }
            else
            {
                // Remove the inactive enemy blips from the dictionary and destroy the blip game objects
                if (enemyBlips.TryGetValue(enemyObject, out GameObject blip))
                {
                    enemyBlips.Remove(enemyObject);
                    Destroy(blip);
                }
            }
        }

        private void RemoveOutOfRangeBlip(GameObject enemyObject)
        {
            if (enemyBlips.ContainsKey(enemyObject))
            {
                // Remove the blip game object from the scene
                GameObject blip = enemyBlips[enemyObject];
                enemyBlips.Remove(enemyObject);
                Destroy(blip);
            }
        }
        private void RemoveInactiveEnemyBlips(List<GameObject> activeEnemyObjects, List<GameObject> blipsToRemove)
        {
            // Create a list to store the enemy objects that need to be removed
            List<GameObject> enemiesToRemove = new List<GameObject>();

            // Iterate through the enemyBlips dictionary
            foreach (var enemyBlip in enemyBlips)
            {
                GameObject enemyObject = enemyBlip.Key;

                // Check if the enemy object is not in the activeEnemyObjects list
                if (!activeEnemyObjects.Contains(enemyObject))
                {
                    // Add the enemy object to the enemiesToRemove list
                    enemiesToRemove.Add(enemyObject);
                }
            }

            // Iterate through the enemiesToRemove list and remove blips
            foreach (GameObject enemyObject in enemiesToRemove)
            {
                // Check if the enemy object exists in the enemyBlips dictionary
                if (enemyBlips.TryGetValue(enemyObject, out GameObject blip))
                {
                    // Remove the blip game object from the scene
                    enemyBlips.Remove(enemyObject);
                    Destroy(blip);
                }
            }
        }
    }
}