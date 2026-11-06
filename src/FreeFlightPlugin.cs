using System;
using System.IO;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace Oddhatter.FreeFlight
{
    [BepInPlugin(Guid, Name, Version)]
    [BepInProcess("valheim.exe")]
    public sealed class FreeFlightPlugin : BaseUnityPlugin
    {
        public const string Guid = "com.oddhatter.freeflight";
        public const string Name = "FreeFlight";
        public const string Version = "1.1.0";

        internal static ManualLogSource Log;

        internal static ConfigEntry<bool> Enabled;
        internal static ConfigEntry<KeyCode> ToggleKey;
        internal static ConfigEntry<bool> GamepadToggle;
        internal static ConfigEntry<float> FlySpeed;
        internal static ConfigEntry<float> SprintMultiplier;
        internal static ConfigEntry<bool> DragHarpooned;
        internal static ConfigEntry<float> RopeLength;

        /// <summary>Wait this long after the last file event before reloading (editors write in several steps).</summary>
        private const float ConfigReloadDelay = 0.5f;

        private Harmony _harmony;
        private FileSystemWatcher _configWatcher;
        private volatile bool _configFileChanged; // set on a thread-pool thread by the watcher
        private float _configReloadAt = -1f;
        private bool _configLockedLogged;

        private void Awake()
        {
            Log = Logger;

            Enabled = Config.Bind("General", "Enabled", true,
                "Master switch. When false, flight is unavailable and the harpoon works as in vanilla.");

            ToggleKey = Config.Bind("General", "ToggleKey", KeyCode.H,
                "Key that turns flying on and off. While flying: Jump (Space) climbs, Crouch (Ctrl) descends, Run (Shift) is faster.");

            GamepadToggle = Config.Bind("General", "GamepadToggle", true,
                "Controller: press both thumbsticks in together (L3 + R3) to turn flying on and off.");

            FlySpeed = Config.Bind("Speed", "FlySpeed", 20f,
                new ConfigDescription(
                    "Flying speed in meters per second (normal running is about 7).",
                    new AcceptableValueRange<float>(1f, 100f)));

            SprintMultiplier = Config.Bind("Speed", "SprintMultiplier", 2.5f,
                new ConfigDescription(
                    "Speed multiplier while holding Run. Flying never costs stamina.",
                    new AcceptableValueRange<float>(1f, 10f)));

            DragHarpooned = Config.Bind("Harpoon", "DragHarpooned", true,
                "true: an enemy you harpoon is towed through the air while you fly (the line doesn't snap and costs no stamina). " +
                "Release it the vanilla way, by attacking or blocking. false: vanilla harpoon.");

            RopeLength = Config.Bind("Harpoon", "RopeLength", 5f,
                new ConfigDescription(
                    "How far below/behind you a towed enemy hangs, in meters.",
                    new AcceptableValueRange<float>(2f, 30f)));

            _harmony = new Harmony(Guid);
            _harmony.PatchAll(typeof(FreeFlightPlugin).Assembly);
            foreach (System.Reflection.MethodBase method in _harmony.GetPatchedMethods())
            {
                Log.LogInfo($"Patched {method.DeclaringType?.Name}.{method.Name}");
            }
            WatchConfigFile();
            Log.LogInfo($"{Name} {Version} loaded.");
        }

        private void Update()
        {
            Flight.UpdateToggle();

            if (_configFileChanged)
            {
                _configFileChanged = false;
                _configReloadAt = Time.realtimeSinceStartup + ConfigReloadDelay;
            }
            if (_configReloadAt >= 0f && Time.realtimeSinceStartup >= _configReloadAt)
            {
                _configReloadAt = -1f;
                ReloadConfigFromDisk();
            }
        }

        private void OnDestroy()
        {
            _configWatcher?.Dispose();
            _harmony?.UnpatchSelf();
        }

        /// <summary>
        /// Picks up edits to the .cfg file made while the game runs (text editor, r2modman config editor).
        /// Settings are read every physics step, so reloaded values apply immediately.
        /// </summary>
        private void WatchConfigFile()
        {
            string path = Config.ConfigFilePath;
            _configWatcher = new FileSystemWatcher(Path.GetDirectoryName(path), Path.GetFileName(path))
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size,
                IncludeSubdirectories = false,
            };
            _configWatcher.Changed += OnConfigFileEvent;
            _configWatcher.Created += OnConfigFileEvent;
            _configWatcher.Renamed += OnConfigFileEvent;
            _configWatcher.EnableRaisingEvents = true;
        }

        private void OnConfigFileEvent(object sender, FileSystemEventArgs e)
        {
            _configFileChanged = true; // handled on the main thread in Update
        }

        private void ReloadConfigFromDisk()
        {
            if (!File.Exists(Config.ConfigFilePath))
            {
                return;
            }
            // ConfigFile.Reload sets each entry; with SaveOnConfigSet every changed value would rewrite the file
            // and re-trigger this watcher. The file already holds these values, so don't save while reloading.
            bool saveOnSet = Config.SaveOnConfigSet;
            Config.SaveOnConfigSet = false;
            try
            {
                Config.Reload();
                _configLockedLogged = false;
                Log.LogInfo("Configuration reloaded from disk.");
            }
            catch (IOException ex)
            {
                // File still being written or locked by an editor: keep retrying, but only log once.
                if (!_configLockedLogged)
                {
                    _configLockedLogged = true;
                    Log.LogWarning($"Config file busy, will retry: {ex.Message}");
                }
                _configReloadAt = Time.realtimeSinceStartup + ConfigReloadDelay;
            }
            catch (Exception ex)
            {
                Log.LogError($"Failed to reload configuration: {ex.Message}");
            }
            finally
            {
                Config.SaveOnConfigSet = saveOnSet;
            }
        }
    }
}
