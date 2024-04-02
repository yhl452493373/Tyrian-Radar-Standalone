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
using BepInEx.Configuration;
using Object = UnityEngine.Object;

namespace Radar
{
    public class HaloRadar : MonoBehaviour
    {
        private GameWorld _gameWorld = null!;
        private Player _player = null!;

        public static RectTransform RadarHudBlipBasePosition { get; private set; } = null!;
        public static RectTransform RadarHudBasePosition { get; private set; } = null!;
        
        private RectTransform _radarHudPulse = null!;
        
        private Coroutine? _pulseCoroutine;
        private float _radarPulseInterval = 1f;
        
        private Vector3 _radarScaleStart;
        private float _radarPositionYStart = 0f;
        private float _radarPositionXStart = 0f;

        public static float RadarLastUpdateTime = 0;

        private readonly HashSet<int> _enemyList = new HashSet<int>();
        private readonly List<BlipPlayer> _enemyCustomObject = new List<BlipPlayer>();

        private readonly HashSet<string> _lootList = new HashSet<string>();
        private readonly List<BlipLoot> _lootCustomObject = new List<BlipLoot>();

        private void Awake()
        {
            if (!Singleton<GameWorld>.Instantiated)
            {
                Radar.Log.LogWarning("GameWorld singleton not found.");
                Destroy(gameObject);
                return;
            }
            
            _gameWorld = Singleton<GameWorld>.Instance;
            if (_gameWorld.MainPlayer == null)
            {
                Radar.Log.LogWarning("MainPlayer is null.");
                Destroy(gameObject);
                return;
            }
            
            _player = _gameWorld.MainPlayer;
                
            RadarHudBasePosition = (transform.Find("Radar") as RectTransform)!;
            RadarHudBlipBasePosition = (transform.Find("Radar/RadarBorder") as RectTransform)!;
            RadarHudBlipBasePosition.SetAsLastSibling();
            _radarHudPulse = (transform.Find("Radar/RadarPulse") as RectTransform)!;
            _radarScaleStart = RadarHudBasePosition.localScale;
            _radarPositionYStart = RadarHudBasePosition.position.y;
            _radarPositionXStart = RadarHudBasePosition.position.x;
            RadarHudBasePosition.position = new Vector2(_radarPositionXStart + Radar.radarOffsetXConfig.Value, _radarPositionYStart + Radar.radarOffsetYConfig.Value);
            RadarHudBasePosition.localScale = new Vector2(_radarScaleStart.x * Radar.radarSizeConfig.Value, _radarScaleStart.y * Radar.radarSizeConfig.Value);

            
            RadarHudBlipBasePosition.GetComponent<Image>().color = Radar.backgroundColor.Value;
            _radarHudPulse.GetComponent<Image>().color = Radar.backgroundColor.Value;
            transform.Find("Radar/RadarBackground").GetComponent<Image>().color = Radar.backgroundColor.Value;
            
            Radar.Log.LogInfo("Radar loaded");
        }
        
        private void OnEnable()
        {
            Radar.Instance.Config.SettingChanged += UpdateRadarSettings;
            UpdateRadarSettings();
        }
        
        private void OnDisable()
        {
            Radar.Instance.Config.SettingChanged -= UpdateRadarSettings;
        }

        private void UpdateRadarSettings(object? sender = null, SettingChangedEventArgs? e = null)
        {
            if (!gameObject.activeInHierarchy) return; // Don't update if the radar object is disabled

            _radarPulseInterval = Mathf.Max(1f, Radar.radarScanInterval.Value);

            if (e == null || e.ChangedSetting == Radar.radarEnablePulseConfig)
            {
                TogglePulseAnimation(Radar.radarEnablePulseConfig.Value);
            }
        }

        private void TogglePulseAnimation(bool enable)
        {
            if (enable)
            {
                // always create a new coroutine
                if (_pulseCoroutine != null)
                {
                    StopCoroutine(_pulseCoroutine);
                }

                _pulseCoroutine = StartCoroutine(PulseCoroutine());
            }
            else if (_pulseCoroutine != null && !enable)
            {
                StopCoroutine(_pulseCoroutine);
                _pulseCoroutine = null;
            }

            _radarHudPulse.gameObject.SetActive(enable);
        }

        private void Update()
        {
            RadarHudBasePosition.position = new Vector2(_radarPositionXStart + Radar.radarOffsetXConfig.Value, _radarPositionYStart + Radar.radarOffsetYConfig.Value);
            RadarHudBasePosition.localScale = new Vector2(_radarScaleStart.x * Radar.radarSizeConfig.Value, _radarScaleStart.y * Radar.radarSizeConfig.Value);
            RadarHudBlipBasePosition.eulerAngles = new Vector3(0, 0, transform.parent.transform.eulerAngles.y);
            
            UpdateLoot();
            long rslt = UpdateActivePlayer();
            UpdateRadar(rslt != -1);
        }

        private IEnumerator PulseCoroutine()
        {
            while (true)
            {
                // Rotate from 360 to 0 over the animation duration
                float t = 0f;
                while (t < 1.0f)
                {
                    t += Time.deltaTime / _radarPulseInterval;
                    float angle = Mathf.Lerp(0f, 1f, 1 - t) * 360;

                    // Apply the scale to all axes
                    _radarHudPulse.localEulerAngles = new Vector3(0, 0, angle);
                    yield return null;
                }
                // Pause for the specified duration
                // yield return new WaitForSeconds(interval);
            }
        }

        private long UpdateActivePlayer()
        {
            if (Time.time - RadarLastUpdateTime < Radar.radarScanInterval.Value)
            {
                return -1;
            }
            else
            {
                RadarLastUpdateTime = Time.time;
            }
            IEnumerable<Player> allPlayers = _gameWorld.AllPlayersEverExisted;

            if (allPlayers.Count() == _enemyList.Count + 1)
            {
                return -2;
            }

            foreach (Player enemyPlayer in allPlayers)
            {
                if (enemyPlayer == null || enemyPlayer == _player)
                {
                    continue;
                }
                if (!_enemyList.Contains(enemyPlayer.Id))
                {
                    _enemyList.Add(enemyPlayer.Id);
                    _enemyCustomObject.Add(new BlipPlayer(enemyPlayer));
                }
            }
            return 0;
        }

        private void UpdateLoot()
        {
            if (Time.time - RadarLastUpdateTime < Radar.radarScanInterval.Value)
            {
                return;
            }

            if (!Radar.radarEnableLootConfig.Value)
            {
                // clear _lootList to avoid FPS drop
                if (_lootList.Count > 0)
                {
                    _lootList.Clear();
                    foreach (var loot in _lootCustomObject)
                    {
                        loot.DestoryLoot();
                    }
                    _lootCustomObject.Clear();
                }
                return;
            }

            HashSet<string> checkedLoot = new HashSet<string>();
            var allLoot = _gameWorld.LootItems;
            foreach (LootItem loot in allLoot.GetValuesEnumerator())
            {
                checkedLoot.Add(loot.ItemId);
                if (!_lootList.Contains(loot.ItemId))
                {
                    _lootList.Add(loot.ItemId);
                    _lootCustomObject.Add(new BlipLoot(loot));
                }
            }

            foreach (var item in _lootCustomObject.Where(item => !checkedLoot.Contains(item.itemId)).ToList()) // ToList creates a copy to avoid modification during enumeration
            {
                item.DestoryLoot();
                _lootList.Remove(item.itemId);
            }

            _lootCustomObject.RemoveAll(item => !checkedLoot.Contains(item.itemId));
        }

        private void UpdateRadar(bool positionUpdate = true)
        {
            Target.setPlayerPosition(_player.Transform.position);
            Target.setRadarRange(Radar.radarRangeConfig.Value);
            foreach (var obj in _enemyCustomObject)
            {
                obj.Update(positionUpdate);
            }
            foreach (var obj in _lootCustomObject)
            {
                obj.Update(positionUpdate);
            }
        }
    }
}