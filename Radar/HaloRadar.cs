using EFT;
using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using Comfort.Common;
using UnityEngine;
using UnityEngine.UI;
using LootItem = EFT.Interactive.LootItem;
using System.Linq;
using Object = UnityEngine.Object;

namespace Radar
{
    public class HaloRadar : MonoBehaviour
    {
        public static GameWorld gameWorld;
        public static Player player;

        public static GameObject radarBlipHud;

        public static RectTransform radarHudBlipBasePosition { get; private set; }
        public static RectTransform radarHudBasePosition { get; private set; }
        public static RectTransform radarHudPulse { get; private set; }
        public static RectTransform radarHudBlip { get; private set; }
        public static Image blipImage;
        
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

        private void Awake()
        {
            if (!Singleton<GameWorld>.Instantiated)
            {
                Radar.Log.LogWarning("GameWorld singleton not found.");
                Destroy(gameObject);
                return;
            }
            
            gameWorld = Singleton<GameWorld>.Instance;
            if (gameWorld.MainPlayer == null)
            {
                Radar.Log.LogWarning("MainPlayer is null.");
                Destroy(gameObject);
                return;
            }
            
            player = gameWorld.MainPlayer;
                
            radarHudBasePosition = (transform.Find("Radar") as RectTransform)!;
            radarHudBlipBasePosition = (transform.Find("Radar/RadarBorder") as RectTransform)!;
            radarHudBlipBasePosition.SetAsLastSibling();
            radarHudPulse = (transform.Find("Radar/RadarPulse") as RectTransform)!;
            radarScaleStart = radarHudBasePosition.localScale;
            radarPositionYStart = radarHudBasePosition.position.y;
            radarPositionXStart = radarHudBasePosition.position.x;
            radarHudBasePosition.position = new Vector2(radarPositionXStart + Radar.radarOffsetXConfig.Value, radarPositionYStart + Radar.radarOffsetYConfig.Value);
            radarHudBasePosition.localScale = new Vector2(radarScaleStart.x * Radar.radarSizeConfig.Value, radarScaleStart.y * Radar.radarSizeConfig.Value);

            
            radarHudBlipBasePosition.GetComponent<Image>().color = Radar.backgroundColor.Value;
            radarHudPulse.GetComponent<Image>().color = Radar.backgroundColor.Value;
            transform.Find("Radar/RadarBackground").GetComponent<Image>().color = Radar.backgroundColor.Value;
            
            Radar.Log.LogInfo("Radar loaded");
        }

        private void Update()
        {
            
            radarHudBasePosition.position = new Vector2(radarPositionXStart + Radar.radarOffsetXConfig.Value, radarPositionYStart + Radar.radarOffsetYConfig.Value);
            radarHudBasePosition.localScale = new Vector2(radarScaleStart.x * Radar.radarSizeConfig.Value, radarScaleStart.y * Radar.radarSizeConfig.Value);
            radarHudBlipBasePosition.GetComponent<RectTransform>().eulerAngles = new Vector3(0, 0, transform.parent.transform.eulerAngles.y);
            
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