using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using Radar.Patches;
using UnityEngine;

namespace Radar
{
    [BepInPlugin("Tyrian.Radar", "Radar", "1.1.0")]
    public class Radar : BaseUnityPlugin
    {
        internal static Radar Instance {get; private set;}
        
        public static Dictionary<GameObject, HashSet<Material>> objectsMaterials = new();

        const string langSettings = "radar_lang_settings";
        const string baseSettings = "radar_base_settings";
        const string advancedSettings = "radar_advanced_settings";
        const string radarSettings = "radar_radar_settings";
        const string colorSettings = "radar_color_settings";

        public static ConfigEntry<Locales.Language> radarLanguage;
        public static ConfigEntry<bool> radarEnableConfig;
        public static ConfigEntry<bool> radarEnablePulseConfig;
        public static ConfigEntry<bool> radarEnableCorpseConfig;
        public static ConfigEntry<bool> radarEnableLootConfig;
        public static ConfigEntry<KeyboardShortcut> radarEnableShortCutConfig;
        public static ConfigEntry<KeyboardShortcut> radarEnableCorpseShortCutConfig;
        public static ConfigEntry<KeyboardShortcut> radarEnableLootShortCutConfig;

        public static ConfigEntry<float> radarSizeConfig;
        public static ConfigEntry<float> radarBlipSizeConfig;
        public static ConfigEntry<float> radarDistanceScaleConfig;
        public static ConfigEntry<float> radarYHeightThreshold;
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


        internal static ManualLogSource Log { get; private set; } = null!;

        private void Awake()
        {
            Log = Logger;
            Log.LogInfo("Radar Plugin Enabled.");
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            // Add a custom configuration option for the Apply button
            radarLanguage = Config.Bind(Locales.Translate(langSettings), Locales.Translate("language"), Locales.System,
                new ConfigDescription(Locales.Translate("language_info"), null,
                    new ConfigurationManagerAttributes { IsAdvanced = true, Order = 24 }));

            #region Base Settings

            radarEnableConfig = Config.Bind(Locales.Translate(baseSettings), Locales.Translate("radar_enable"), true,
                new ConfigDescription(Locales.Translate("make_radar_enable"), null,
                    new ConfigurationManagerAttributes { IsAdvanced = false, Order = 23 }));
            radarEnablePulseConfig = Config.Bind(Locales.Translate(baseSettings),
                Locales.Translate("radar_pulse_enable"), true,
                new ConfigDescription(Locales.Translate("radar_pulse_enable_info"), null,
                    new ConfigurationManagerAttributes { IsAdvanced = false, Order = 22 }));
            radarEnableCorpseConfig = Config.Bind(Locales.Translate(baseSettings),
                Locales.Translate("radar_corpse_enable"), true,
                new ConfigDescription(Locales.Translate("make_radar_corpse_enable"), null,
                    new ConfigurationManagerAttributes { IsAdvanced = false, Order = 21 }));
            radarEnableLootConfig = Config.Bind(Locales.Translate(baseSettings), Locales.Translate("radar_loot_enable"),
                true,
                new ConfigDescription(Locales.Translate("make_radar_loot_enable"), null,
                    new ConfigurationManagerAttributes { IsAdvanced = false, Order = 20 }));

            #endregion

            #region Advanced Settins

            radarEnableShortCutConfig = Config.Bind(Locales.Translate(advancedSettings),
                Locales.Translate("radar_enable_shortcut"), new KeyboardShortcut(KeyCode.F10),
                new ConfigDescription("", null, new ConfigurationManagerAttributes { IsAdvanced = false, Order = 19 }));
            radarEnableCorpseShortCutConfig = Config.Bind(Locales.Translate(advancedSettings),
                Locales.Translate("radar_corpse_shortcut"), new KeyboardShortcut(KeyCode.F11),
                new ConfigDescription("", null, new ConfigurationManagerAttributes { IsAdvanced = false, Order = 18 }));
            radarEnableLootShortCutConfig = Config.Bind(Locales.Translate(advancedSettings),
                Locales.Translate("radar_loot_shortcut"), new KeyboardShortcut(KeyCode.F9),
                new ConfigDescription("", null, new ConfigurationManagerAttributes { IsAdvanced = false, Order = 17 }));

            #endregion

            #region Radar Settings

            radarSizeConfig = Config.Bind(Locales.Translate(radarSettings), Locales.Translate("radar_hud_size"), 0.8f,
                new ConfigDescription(Locales.Translate("radar_hud_size_info"),
                    new AcceptableValueRange<float>(0.0f, 1f),
                    new ConfigurationManagerAttributes { IsAdvanced = false, Order = 16 }));
            radarBlipSizeConfig = Config.Bind(Locales.Translate(radarSettings), Locales.Translate("radar_blip_size"),
                0.7f,
                new ConfigDescription(Locales.Translate("radar_blip_size_info"),
                    new AcceptableValueRange<float>(0.0f, 1f),
                    new ConfigurationManagerAttributes { IsAdvanced = false, Order = 15 }));
            radarDistanceScaleConfig = Config.Bind(Locales.Translate(radarSettings),
                Locales.Translate("radar_distance_scale"), 0.7f,
                new ConfigDescription(Locales.Translate("radar_distance_scale_info"),
                    new AcceptableValueRange<float>(0.1f, 2f),
                    new ConfigurationManagerAttributes { IsAdvanced = false, Order = 14 }));
            radarYHeightThreshold = Config.Bind(Locales.Translate(radarSettings),
                Locales.Translate("radar_y_height_threshold"), 1f,
                new ConfigDescription(Locales.Translate("radar_y_height_threshold_info"),
                    new AcceptableValueRange<float>(1f, 4f),
                    new ConfigurationManagerAttributes { IsAdvanced = false, Order = 13 }));
            radarOffsetXConfig = Config.Bind(Locales.Translate(radarSettings), Locales.Translate("radar_x_position"),
                0f,
                new ConfigDescription(Locales.Translate("radar_x_position_info"),
                    new AcceptableValueRange<float>(-4000f, 4000f),
                    new ConfigurationManagerAttributes { IsAdvanced = false, Order = 12 }));
            radarOffsetYConfig = Config.Bind(Locales.Translate(radarSettings), Locales.Translate("radar_y_position"),
                0f,
                new ConfigDescription(Locales.Translate("radar_y_position_info"),
                    new AcceptableValueRange<float>(-4000f, 4000f),
                    new ConfigurationManagerAttributes { IsAdvanced = false, Order = 11 }));
            radarRangeConfig = Config.Bind(Locales.Translate(radarSettings), Locales.Translate("radar_range"), 128f,
                new ConfigDescription(Locales.Translate("radar_range_info"), new AcceptableValueRange<float>(32f, 512f),
                    new ConfigurationManagerAttributes { IsAdvanced = false, Order = 10 }));
            radarScanInterval = Config.Bind(Locales.Translate(radarSettings), Locales.Translate("radar_scan_interval"),
                1f,
                new ConfigDescription(Locales.Translate("radar_scan_interval_info"),
                    new AcceptableValueRange<float>(0.1f, 30f),
                    new ConfigurationManagerAttributes { IsAdvanced = false, Order = 9 }));
            radarLootThreshold = Config.Bind(Locales.Translate(radarSettings),
                Locales.Translate("radar_loot_threshold"), 30000f,
                new ConfigDescription(Locales.Translate("radar_loot_threshold_info"),
                    new AcceptableValueRange<float>(1000f, 100000f),
                    new ConfigurationManagerAttributes { IsAdvanced = false, Order = 8 }));

            #endregion

            #region Color Settings
            
            bossBlipColor = Config.Bind(Locales.Translate(colorSettings), Locales.Translate("radar_boss_blip_color"),
                new Color(1f, 0f, 0f),
                new ConfigDescription("", null, new ConfigurationManagerAttributes { IsAdvanced = false, Order = 7 }));
            scavBlipColor = Config.Bind(Locales.Translate(colorSettings), Locales.Translate("radar_scav_blip_color"),
                new Color(0f, 1f, 0f),
                new ConfigDescription("", null, new ConfigurationManagerAttributes { IsAdvanced = false, Order = 6 }));
            usecBlipColor = Config.Bind(Locales.Translate(colorSettings), Locales.Translate("radar_usec_blip_color"),
                new Color(1f, 1f, 0f),
                new ConfigDescription("", null, new ConfigurationManagerAttributes { IsAdvanced = false, Order = 5 }));
            bearBlipColor = Config.Bind(Locales.Translate(colorSettings), Locales.Translate("radar_bear_blip_color"),
                new Color(1f, 0.5f, 0f),
                new ConfigDescription("", null, new ConfigurationManagerAttributes { IsAdvanced = false, Order = 4 }));
            corpseBlipColor = Config.Bind(Locales.Translate(colorSettings),
                Locales.Translate("radar_corpse_blip_color"), new Color(0.5f, 0.5f, 0.5f),
                new ConfigDescription("", null, new ConfigurationManagerAttributes { IsAdvanced = false, Order = 3 }));
            lootBlipColor = Config.Bind(Locales.Translate(colorSettings), Locales.Translate("radar_loot_blip_color"),
                new Color(0.9f, 0.5f, 0.5f),
                new ConfigDescription("", null, new ConfigurationManagerAttributes { IsAdvanced = false, Order = 2 }));
            backgroundColor = Config.Bind(Locales.Translate(colorSettings),
                Locales.Translate("radar_background_blip_color"), new Color(0f, 0.7f, 0.85f),
                new ConfigDescription("", null, new ConfigurationManagerAttributes { IsAdvanced = false, Order = 1 }));

            #endregion
            
            AssetBundleManager.LoadAssetBundle();
            
            new GameStartPatch().Enable();
        }
    }
}