using System;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using Enviro;
using HarmonyLib;
using JetBrains.Annotations;
using UnityEngine;

namespace DayAndNight
{
    [BepInPlugin(ModGUID, ModName, ModVersion)]
    public class DayAndNightPlugin : BaseUnityPlugin
    {
        internal const string ModName = "DayAndNight";
        internal const string ModVersion = "1.0.3";
        internal const string Author = "Azumatt";
        private const string ModGUID = Author + "." + ModName;
        private static string ConfigFileName = ModGUID + ".cfg";
        private static string ConfigFileFullPath = Paths.ConfigPath + Path.DirectorySeparatorChar + ConfigFileName;
        private readonly Harmony _harmony = new(ModGUID);
        public static readonly ManualLogSource DayAndNightLogger = BepInEx.Logging.Logger.CreateLogSource(ModName);


        public enum Toggle
        {
            On = 1,
            Off = 0
        }

        public void Awake()
        {
            FreezeTimeHotkey = Config.Bind("2 - Hotkeys", "Freeze Time Hotkey", new KeyboardShortcut(KeyCode.F, KeyCode.LeftControl), "This is the hotkey that will toggle the Freeze Time option. This will only work if the game is not paused.");
            FreezeTime = Config.Bind("1 - Freeze", "Freeze Time", Toggle.Off, "If on, the other values in this section take effect. They will control the time of day that is in the game.");
            Seconds = Config.Bind("1 - Freeze", "Seconds", 0, "If Freeze Time is on, this will control the seconds of the day. 0 is the default, 30 is half a minute, 60 is a minute.");
            Minutes = Config.Bind("1 - Freeze", "Minutes", 0, "If Freeze Time is on, this will control the minutes of the day. 0 is the default, 30 is half an hour, 60 is an hour.");
            Hours = Config.Bind("1 - Freeze", "Hours", 12, "If Freeze Time is on, this will control the hours of the day. 12 noon is the default, 0 is midnight, 24 is midnight");
            Days = Config.Bind("1 - Freeze", "Days", 1, "If Freeze Time is on, this will control the days in your game. 1 is the default");
            Years = Config.Bind("1 - Freeze", "Years", 1, "If Freeze Time is on, this will control the years of your game. 1 is the default.");

            DayModifier = Config.Bind("1 - Modifiers", "Day Modifier", -1f, "If this value is not -1, it will control the length of the day. 1 is normal, 2 is twice as long, 0.5 is half as long.");
            NightModifier = Config.Bind("1 - Modifiers", "Night Modifier", -1f, "If this value is not -1, it will control the length of the night. 1 is normal, 2 is twice as long, 0.5 is half as long.");
            CycleLengthInMinutes = Config.Bind("1 - Modifiers", "Cycle Length In Minutes", -1f, "If this value is not -1, it will control the cycle length of the day. The default for the game is 5 minutes. This will change the length of the day & night cycle. The game states this as the Full 24h cycle in realtime minutes.");


            Assembly assembly = Assembly.GetExecutingAssembly();
            _harmony.PatchAll(assembly);
            SetupWatcher();
        }

        private void Update()
        {
            // Check if the hotkey is pressed
            if (FreezeTimeHotkey.Value.IsKeyDown())
            {
                // Toggle the freeze time option
                FreezeTime.Value = FreezeTime.Value == Toggle.On ? Toggle.Off : Toggle.On;
            }
        }

        private void OnDestroy()
        {
            Config.Save();
        }

        private void SetupWatcher()
        {
            FileSystemWatcher watcher = new(Paths.ConfigPath, ConfigFileName);
            watcher.Changed += ReadConfigValues;
            watcher.Created += ReadConfigValues;
            watcher.Renamed += ReadConfigValues;
            watcher.IncludeSubdirectories = true;
            watcher.SynchronizingObject = ThreadingHelper.SynchronizingObject;
            watcher.EnableRaisingEvents = true;
        }

        private void ReadConfigValues(object sender, FileSystemEventArgs e)
        {
            if (!File.Exists(ConfigFileFullPath)) return;
            try
            {
                DayAndNightLogger.LogDebug("ReadConfigValues called");
                Config.Reload();
            }
            catch
            {
                DayAndNightLogger.LogError($"There was an issue loading your {ConfigFileName}");
                DayAndNightLogger.LogError("Please check your config entries for spelling and format!");
            }
        }


        #region ConfigOptions

        internal static ConfigEntry<KeyboardShortcut> FreezeTimeHotkey = null!;
        internal static ConfigEntry<Toggle> FreezeTime = null!;
        internal static ConfigEntry<float> DayModifier = null!;
        internal static ConfigEntry<float> NightModifier = null!;
        internal static ConfigEntry<float> CycleLengthInMinutes = null!;
        internal static ConfigEntry<int> Seconds = null!;
        internal static ConfigEntry<int> Minutes = null!;
        internal static ConfigEntry<int> Hours = null!;
        internal static ConfigEntry<int> Days = null!;
        internal static ConfigEntry<int> Years = null!;

        private class ConfigurationManagerAttributes
        {
            [UsedImplicitly] public int? Order = null!;
            [UsedImplicitly] public bool? Browsable = null!;
            [UsedImplicitly] public string? Category = null!;
            [UsedImplicitly] public Action<ConfigEntryBase>? CustomDrawer = null!;
        }

        internal ConfigEntry<T> TextEntryConfig<T>(string group, string name, T value, string desc)
        {
            ConfigurationManagerAttributes attributes = new()
            {
                CustomDrawer = TextAreaDrawer
            };
            return Config.Bind(group, name, value, new ConfigDescription(desc, null, attributes));
        }

        internal static void TextAreaDrawer(ConfigEntryBase entry)
        {
            GUILayout.ExpandHeight(true);
            GUILayout.ExpandWidth(true);
            entry.BoxedValue = GUILayout.TextArea((string)entry.BoxedValue, GUILayout.ExpandWidth(true),
                GUILayout.ExpandHeight(true));
        }

        class AcceptableShortcuts : AcceptableValueBase
        {
            public AcceptableShortcuts() : base(typeof(KeyboardShortcut))
            {
            }

            public override object Clamp(object value) => value;
            public override bool IsValid(object value) => true;

            public override string ToDescriptionString() =>
                "# Acceptable values: " + string.Join(", ", UnityInput.Current.SupportedKeyCodes);
        }

        #endregion
    }

    public static class KeyboardExtensions
    {
        public static bool IsKeyDown(this KeyboardShortcut shortcut)
        {
            return shortcut.MainKey != KeyCode.None && Input.GetKeyDown(shortcut.MainKey) &&
                   shortcut.Modifiers.All(Input.GetKey);
        }

        public static bool IsKeyHeld(this KeyboardShortcut shortcut)
        {
            return shortcut.MainKey != KeyCode.None && Input.GetKey(shortcut.MainKey) &&
                   shortcut.Modifiers.All(Input.GetKey);
        }
    }


    [HarmonyPatch(typeof(EnviroTimeModule), nameof(EnviroTimeModule.UpdateModule))]
    static class EnviroCoreUpdateTimePatch
    {
        static void Prefix(EnviroTimeModule __instance)
        {
            if (Math.Abs(DayAndNightPlugin.DayModifier.Value - (-1f)) > 0.001)
            {
                // prevent the value from being 0
                if (DayAndNightPlugin.DayModifier.Value < 0.001)
                {
                    DayAndNightPlugin.DayModifier.Value = 0.001f;
                }

                __instance.Settings.dayLengthModifier = DayAndNightPlugin.DayModifier.Value;
            }

            if (Math.Abs(DayAndNightPlugin.NightModifier.Value - (-1f)) > 0.001)
            {
                // prevent the value from being 0
                if (DayAndNightPlugin.NightModifier.Value < 0.001)
                {
                    DayAndNightPlugin.NightModifier.Value = 0.001f;
                }

                __instance.Settings.nightLengthModifier = DayAndNightPlugin.NightModifier.Value;
            }

            if (Math.Abs(DayAndNightPlugin.CycleLengthInMinutes.Value - (-1f)) > 0.001)
            {
                // prevent the value from being 0
                if (DayAndNightPlugin.CycleLengthInMinutes.Value < 0.001)
                {
                    DayAndNightPlugin.CycleLengthInMinutes.Value = 0.001f;
                }

                __instance.Settings.cycleLengthInMinutes = DayAndNightPlugin.CycleLengthInMinutes.Value;
            }

            if (DayAndNightPlugin.FreezeTime.Value == DayAndNightPlugin.Toggle.On)
            {
                // Set the time progression to none
                __instance.Settings.simulate = false;

                // Set the time to a forced time for days minutes etc.
                __instance.Settings.secSerial = DayAndNightPlugin.Seconds.Value;
                __instance.Settings.minSerial = DayAndNightPlugin.Minutes.Value;
                __instance.Settings.hourSerial = DayAndNightPlugin.Hours.Value;
                __instance.Settings.daySerial = DayAndNightPlugin.Days.Value;
                __instance.Settings.yearSerial = DayAndNightPlugin.Years.Value;
            }
            else
            {
                // Reset the time progression to the default
                __instance.Settings.simulate = true;
            }
        }
    }
}