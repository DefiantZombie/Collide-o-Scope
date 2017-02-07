﻿#if DEBUG
#define ENABLE_PROFILER
#define DEVELOPMENT
#endif
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using KSP.UI.Screens;

using StreamWriter = System.IO.StreamWriter;
// ReSharper disable ForCanBeConvertedToForeach

namespace ColliderHelper
{
    [KSPAddon(KSPAddon.Startup.FlightAndEditor, false)]
    public class ColliderHelper : MonoBehaviour
    {
        // ReSharper disable once InconsistentNaming
        private const string _oldSettingsURL = "GameData/Collide-o-Scope/settings.cfg";
        // ReSharper disable once InconsistentNaming
        private const string SettingsURL = "GameData/Collide-o-Scope/PluginData/settings.cfg";

        private static ApplicationLauncherButton _appButton;
        private readonly Texture2D _offTexture = GameDatabase.Instance.GetTexture("Collide-o-Scope/Icons/AppIconOff_38", false);
        private readonly Texture2D _onTexture = GameDatabase.Instance.GetTexture("Collide-o-Scope/Icons/AppIconOn_38", false);

        private static StringBuilder _sb = new StringBuilder();

        private bool _enabled;

        private KeyCode _hotkey = KeyCode.O;

        private bool _defaultEnabled = true;

        private static bool _flightMarkersEnabled;

        private static ModuleColliderHelper AddModule(Part p)
        {
            if (!p.Modules.Contains<ModuleColliderHelper>())
            {
                return p.AddModule("ModuleColliderHelper") as ModuleColliderHelper;
            }
            return p.Modules.GetModule<ModuleColliderHelper>();
        }

        private static void RemoveModule(Part p)
        {
            var modules = p.Modules.GetModules<ModuleColliderHelper>();
            for (var i = 0; i < modules.Count; i++)
            {
                modules[i].SetOff(false);
                p.RemoveModule(modules[i]);
            }
        }

        private static void AddModules()
        {
            if (HighLogic.LoadedSceneIsFlight)
            {
                for (var i = 0; i < FlightGlobals.Vessels.Count; i++)
                {
                    for (var j = 0; j < FlightGlobals.Vessels[i].parts.Count; j++)
                    {
                        AddModule(FlightGlobals.Vessels[i].parts[j]);
                    }
                }
            }
            else if(HighLogic.LoadedSceneIsEditor)
            {
                for (var i = 0; i < EditorLogic.SortedShipList.Count; i++)
                {
                    AddModule(EditorLogic.SortedShipList[i]);
                }
            }
        }

        private static void RemoveModules()
        {
            var components = FindObjectsOfType<ModuleColliderHelper>();
            for (var i = 0; i < components.Length; i++)
            {
                RemoveModule(components[i].part);
            }
        }

        public void EditorStarted()
        {
            AddModules();
        }

        public void VesselLoaded(Vessel v)
        {
            AddModules();
        }

        public void FlightReady()
        {
            AddModules();
        }

        public void CrewOnEva(GameEvents.FromToAction<Part, Part> fta)
        {
            AddModule(fta.to);
        }

        public void FlagPlant(Vessel v)
        {
            for (var i = 0; i < v.Parts.Count; i++)
            {
                AddModule(v.Parts[i]);
            }
        }

        public void EditorPartEvent(ConstructionEventType eventType, Part part)
        {
            // ReSharper disable once SwitchStatementMissingSomeCases
            switch (eventType)
            {
                case ConstructionEventType.PartAttached:
                case ConstructionEventType.PartCreated:
                case ConstructionEventType.PartTweaked:
                    AddModule(part);
                    if (part.symmetryCounterparts.Count > 0)
                    {
                        foreach (var p in part.symmetryCounterparts)
                        {
                            AddModule(p);
                        }
                    }
                    break;

                case ConstructionEventType.PartCopied:
                case ConstructionEventType.PartDeleted:
                case ConstructionEventType.PartDetached:
                case ConstructionEventType.PartDropped:
                    RemoveModule(part);
                    break;
            }
        }

        public void EditorUndoRedo(ShipConstruct construct)
        {
            AddModules();
        }

        public void SettingsApplied()
        {
            if (GameSettings.ADVANCED_TWEAKABLES)
            {
                GuiApplicationLauncherReady();
            }
            else
            {
                if (_appButton == null) return;

                _appButton.SetFalse();
                ApplicationLauncher.Instance.RemoveModApplication(_appButton);
                _appButton = null;
            }
        }

        public void OnGameSceneSwitchRequested(GameEvents.FromToAction<GameScenes, GameScenes> tfa)
        {
            RemoveModules();
        }

        public static void DumpGameObjectChilds(GameObject go, string pre)
        {
            _sb = new StringBuilder();
            DumpGameObjectChilds(go, pre, _sb);
        }

        public static void DumpGameObjectChilds(GameObject go, string pre, StringBuilder sb)
        {
            var first = pre == "";
            var neededChilds = new List<GameObject>();
            var count = go.transform.childCount;
            for (var i = 0; i < count; i++)
            {
                var child = go.transform.GetChild(i).gameObject;
                if (!child.GetComponent<Part>() && child.name != "main camera pivot")
                    neededChilds.Add(child);
            }

            count = neededChilds.Count;

            sb.Append(pre);
            if (!first)
            {
                sb.Append(count > 0 ? "--+" : "---");
            }
            else
            {
                sb.Append("+");
            }
            sb.AppendFormat("{0} T:{1} L:{2} ({3})\n", go.name, go.tag, go.layer, LayerMask.LayerToName(go.layer));

            var front = first ? "" : "  ";
            var preComp = pre + front + (count > 0 ? "| " : "  ");

            var comp = go.GetComponents<Component>();

            for (var i = 0; i < comp.Length; i++)
            {
                if (comp[i] is Transform)
                {
                    sb.AppendFormat("{0}  {1} - {2}\n", preComp, comp[i].GetType().Name, go.transform.name);
                }
                else
                {
                    sb.AppendFormat("{0}  {1} - {2}\n", preComp, comp[i].GetType().Name, comp[i].name);
                }
            }

            sb.AppendLine(preComp);

            for (var i = 0; i < count; i++)
            {
                DumpGameObjectChilds(neededChilds[i], i == count - 1 ? pre + front + " " : pre + front + "|", sb);
            }

            using (var writer = new StreamWriter("dump.txt", false))
            {
                writer.WriteLine(sb.ToString());
            }
        }

        private void LoadSettings(string url)
        {
            if (!System.IO.File.Exists(KSPUtil.ApplicationRootPath + url))
            {
                SaveSettings(url);
                return;
            }

            try
            {
                var settings = ConfigNode.Load(KSPUtil.ApplicationRootPath + url);

                foreach (var node in settings.GetNodes("Collide-o-ScopeSettings"))
                {
                    try
                    {
                        _hotkey = (KeyCode) Enum.Parse(typeof (KeyCode), node.GetValue("_hotkey"));
                        _defaultEnabled = bool.Parse(node.GetValue("_defaultEnabled"));
                    }
                    catch
                    {
                        Debug.LogWarning("[CH] Error loading settings: field");
                        throw;
                    }
                }
            }
            catch (Exception)
            {
                Debug.LogWarning("[CH] Error loading settings: file");
                throw;
            }
        }

        private void SaveSettings(string url)
        {
            var settings = new ConfigNode();
            var node = new ConfigNode {name = "Collide-o-ScopeSettings"};

            node.AddValue("_hotkey", _hotkey);
            node.AddValue("_defaultEnabled", _defaultEnabled);

            settings.AddNode(node);

            settings.Save(KSPUtil.ApplicationRootPath + url, "Collide-o-Scope Settings");
        }

        public void GuiApplicationLauncherReady()
        {
            if (!GameSettings.ADVANCED_TWEAKABLES) return;

            if (_appButton != null) return;

            _appButton = ApplicationLauncher.Instance.AddModApplication(
                AppTrue, AppFalse,
                null, null,
                null, null,
                ApplicationLauncher.AppScenes.VAB | ApplicationLauncher.AppScenes.SPH |
                ApplicationLauncher.AppScenes.FLIGHT,
                _offTexture);

            if (_defaultEnabled)
                _appButton.SetTrue();
            else
                _appButton.SetFalse();
        }

        public void AppTrue()
        {
            AddModules();

            GameEvents.onCrewOnEva.Add(CrewOnEva);
            GameEvents.onEditorPartEvent.Add(EditorPartEvent);
            GameEvents.onEditorRedo.Add(EditorUndoRedo);
            GameEvents.onEditorStarted.Add(EditorStarted);
            GameEvents.onEditorUndo.Add(EditorUndoRedo);
            GameEvents.onFlagPlant.Add(FlagPlant);
            GameEvents.onFlightReady.Add(FlightReady);
            GameEvents.onGameSceneSwitchRequested.Add(OnGameSceneSwitchRequested);
            GameEvents.onHideUI.Add(HideUI);
            GameEvents.onShowUI.Add(ShowUI);
            GameEvents.onVesselLoaded.Add(VesselLoaded);

            _enabled = true;

            _appButton.SetTexture(_onTexture);
        }

        public void AppFalse()
        {
            _enabled = false;

            RemoveModules();

            RemoveFlightMarkers();

            CleanupHooks();

            _appButton.SetTexture(_offTexture);
        }

        private void CleanupHooks()
        {
            GameEvents.onCrewOnEva.Remove(CrewOnEva);
            GameEvents.onEditorPartEvent.Remove(EditorPartEvent);
            GameEvents.onEditorRedo.Remove(EditorUndoRedo);
            GameEvents.onEditorStarted.Remove(EditorStarted);
            GameEvents.onEditorUndo.Remove(EditorUndoRedo);
            GameEvents.onFlagPlant.Remove(FlagPlant);
            GameEvents.onFlightReady.Remove(FlightReady);
            GameEvents.onGameSceneSwitchRequested.Remove(OnGameSceneSwitchRequested);
            GameEvents.onHideUI.Remove(HideUI);
            GameEvents.onShowUI.Remove(ShowUI);
            GameEvents.onVesselLoaded.Remove(VesselLoaded);
        }

        public static bool ToggleFlightMarkers(Vessel vessel)
        {
            if (HighLogic.LoadedScene != GameScenes.FLIGHT) return false;

            _flightMarkersEnabled = !_flightMarkersEnabled;

            if (_flightMarkersEnabled)
            {
                vessel.gameObject.AddOrGetComponent<FlightMarkersComponent>();
            }
            else
            {
                var components = vessel.gameObject.GetComponents<FlightMarkersComponent>();
                for (var i = 0; i < components.Length; i++)
                {
                    Destroy(components[i]);
                }
            }

            return _flightMarkersEnabled;
        }

        private static void RemoveFlightMarkers()
        {
            var components = FindObjectsOfType<FlightMarkersComponent>();
            for (var i = 0; i < components.Length; i++)
            {
                Destroy(components[i]);
            }

            _flightMarkersEnabled = false;
        }

        private static void SetVisible(bool visible)
        {
            var colliders = FindObjectsOfType<WireframeComponent>();
            var markers = FindObjectsOfType<FlightMarkersComponent>();

            for (var i = 0; i < colliders.Length; i++)
            {
                colliders[i].SetEnabled(visible);
            }

            for (var i = 0; i < colliders.Length; i++)
            {
                markers[i].SetEnabled(visible);
            }
        } 

        // ReSharper disable once InconsistentNaming
        public void HideUI()
        {
            SetVisible(false);
        }

        // ReSharper disable once InconsistentNaming
        public void ShowUI()
        {
            SetVisible(true);
        }

        /// <summary>
        /// Added in 1.0.7 to fix the ModuleManager cache reload issue.
        /// </summary>
        private static void FixSettingsFile()
        {
            if (!System.IO.File.Exists(KSPUtil.ApplicationRootPath + _oldSettingsURL)) return;

            var cosPath = KSPUtil.ApplicationRootPath + "GameData/Collide-o-Scope/";

            if (!System.IO.Directory.Exists(cosPath + "PluginData"))
            {
                System.IO.Directory.CreateDirectory(cosPath + "PluginData");
            }

            System.IO.File.Copy(cosPath + "settings.cfg", cosPath + "PluginData/settings.cfg");

            if (System.IO.File.Exists(cosPath + "PluginData/settings.cfg"))
            {
                System.IO.File.Delete(cosPath + "settings.cfg");
            }

            Debug.Log("[Collide-o-Scope] Fixed settings.cfg location. (1.0.7)");
        }

        public void Awake()
        {
            FixSettingsFile();

            LoadSettings(SettingsURL);

            GameEvents.onGUIApplicationLauncherReady.Add(GuiApplicationLauncherReady);

            GameEvents.OnGameSettingsApplied.Add(SettingsApplied);

        }
        
        public void Update()
        {
            if (!_enabled) return;

            if (InputLockManager.IsAllLocked(ControlTypes.KEYBOARDINPUT)) return;

            if (HighLogic.LoadedSceneIsEditor)
            {
                if (EditorLogic.fetch.NameOrDescriptionFocused()) return;
            }

            if (!Input.GetKeyDown(_hotkey)) return;

            if (Mouse.HoveredPart != null)
            {
                var component = Mouse.HoveredPart.GetComponent<ModuleColliderHelper>() ?? AddModule(Mouse.HoveredPart);
                component.CycleState();
            }
            else
            {
                var components = FindObjectsOfType<ModuleColliderHelper>();
                for (var i = 0; i < components.Length; i++)
                {
                    components[i].SetOff(false);
                }
            }
        }

        // ReSharper disable once InconsistentNaming
        public void OnGUI()
        {
            DrawTools.NewFrame();
        }

        public void OnDestroy()
        {
            if (_appButton != null)
            {
                ApplicationLauncher.Instance.RemoveModApplication(_appButton);
                _appButton = null;
            }

            GameEvents.onGUIApplicationLauncherReady.Remove(GuiApplicationLauncherReady);

            GameEvents.OnGameSettingsApplied.Remove(SettingsApplied);

            CleanupHooks();

            SaveSettings(SettingsURL);
        }
    }
}
