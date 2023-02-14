using UnityEngine;
using UnityEditor;
using UnityEditor.PackageManager;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;

namespace NAK.MockHMDHelper.Editor
{
    [InitializeOnLoad]
    public class MockHMDHelperEditorWindow : EditorWindow
    {   
        public static Type MockHMDLoaderType;
        public MockHMDHelperEditorWindow()
        {
            MockHMDLoaderType = AppDomain.CurrentDomain
                    .GetAssemblies()
                    .SelectMany(assembly => assembly.GetTypes())
                    .FirstOrDefault(type => type.Name == "MockHMDLoader");
        }

        //Package checking stuff
        private static readonly string[] packageIDs = { "com.unity.xr.management", "com.unity.xr.mock-hmd" };
        private Dictionary<string, bool> packageInstalled = new Dictionary<string, bool>();
        private Dictionary<string, bool> packageInstalling = new Dictionary<string, bool>();

        private static bool _testingEnabled;

        // Add a menu item to create the window
        [MenuItem("NotAKid/MockHMDHelper")]
        public static void ShowWindow()
        {
            // Show the window
            EditorWindow.GetWindow(typeof(MockHMDHelperEditorWindow), false, "MockHMDHelper");
        }

        private void OnEnable()
        {
            foreach (var packageID in packageIDs)
            {
                packageInstalled[packageID] = IsPackageInstalled(packageID);
                packageInstalling[packageID] = false;
            }
            EditorApplication.playModeStateChanged += PlayModeState;
        }

        private void OnDisable()
        {
            EditorApplication.playModeStateChanged -= PlayModeState;
        }

        private void OnGUI()
        {
            //Check if packages are installed.
            bool packagesMissing = false;
            foreach (var packageID in packageIDs)
            {
                if (InstallPackageGUI(packageID)) 
                {
                    packagesMissing = true;
                    break; //one at a time
                }
            }

            //enable testing stuf here
            EditorGUI.BeginDisabledGroup(packagesMissing || Application.isPlaying);

            _testingEnabled = EditorPrefs.GetBool("MockHMDHelper_OnPlayMode", _testingEnabled);

            string text = !_testingEnabled ? "Enable on PlayMode" : "Disable on PlayMode";
            if (GUILayout.Button(text, GUILayout.Height(EditorGUIUtility.singleLineHeight * 2)))
            {
                _testingEnabled = !_testingEnabled;
                EditorPrefs.SetBool("MockHMDHelper_OnPlayMode", _testingEnabled);
                ToggleMockHMD(_testingEnabled);
            }

            EditorGUI.EndDisabledGroup();
        }

        private bool InstallPackageGUI(string packageID)
        {
            if (packageInstalled[packageID]) return false;
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.HelpBox($"{packageID} is missing from the project.", MessageType.Warning);
            EditorGUI.BeginDisabledGroup(packageInstalling[packageID]);
            if (GUILayout.Button($"Install {packageID}", GUILayout.Width(200), GUILayout.Height(EditorGUIUtility.singleLineHeight * 2)))
            {
                packageInstalling[packageID] = true;
                Client.Add(packageID);
            }
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndHorizontal();
            return true;
        }

        public bool IsPackageInstalled(string package)
        {
            if (!File.Exists("Packages/manifest.json"))
                return false;
            string jsonText = File.ReadAllText("Packages/manifest.json");
            return jsonText.Contains(package);
        }

        public void StartXRLoader()
        {
            var currentTarget = EditorUserBuildSettings.selectedBuildTargetGroup;
            var allTypes = AppDomain.CurrentDomain.GetAssemblies().SelectMany(assembly => assembly.GetTypes());
            Type xrGeneralSettingsType = allTypes.FirstOrDefault(type => type.Name == "XRGeneralSettings");
            Type xrManagerType = allTypes.FirstOrDefault(type => type.Name == "XRManager");
            Type xrManagerSettingsType = allTypes.FirstOrDefault(type => type.Name == "XRManagerSettings");
            Type xrGeneralSettingsPerBuildTargetType = allTypes.FirstOrDefault(type => type.Name == "XRGeneralSettingsPerBuildTarget");

            var buildTargetSettings = (UnityEngine.Object)ScriptableObject.CreateInstance(xrGeneralSettingsType);
            bool success = EditorBuildSettings.TryGetConfigObject("com.unity.xr.management.loader_settings", out buildTargetSettings);
            if (!success)
            {
                // TODO: handle case where TryGetConfigObject failed
                return;
            }

            object settings = xrGeneralSettingsPerBuildTargetType
                .GetMethod("SettingsForBuildTarget", BindingFlags.Public | BindingFlags.Instance)
                .Invoke(buildTargetSettings, new object[] { currentTarget });

            PropertyInfo managerProperty = xrGeneralSettingsType.GetProperty("Manager", BindingFlags.Public | BindingFlags.Instance);
            object manager = managerProperty.GetValue(settings);


            PropertyInfo activeLoaderProperty = xrManagerSettingsType.GetProperty("activeLoader", BindingFlags.Public | BindingFlags.Instance);
            object activeLoader = activeLoaderProperty.GetValue(manager);


            if (activeLoader != null)
            {
                MethodInfo stopSubsystemsMethod = xrManagerSettingsType.GetMethod("StopSubsystems", BindingFlags.Public | BindingFlags.Instance);
                stopSubsystemsMethod.Invoke(manager, null);

                MethodInfo deinitializeLoaderMethod = xrManagerSettingsType.GetMethod("DeinitializeLoader", BindingFlags.Public | BindingFlags.Instance);
                deinitializeLoaderMethod.Invoke(manager, null);
            }

            MethodInfo initializeLoaderSyncMethod = xrManagerSettingsType.GetMethod("InitializeLoaderSync", BindingFlags.Public | BindingFlags.Instance);
            initializeLoaderSyncMethod.Invoke(manager, null);

            MethodInfo startSubsystemsMethod = xrManagerSettingsType.GetMethod("StartSubsystems", BindingFlags.Public | BindingFlags.Instance);
            startSubsystemsMethod.Invoke(manager, null);
        }


        public void ToggleMockHMD(bool flag)
        {
            var currentTarget = EditorUserBuildSettings.selectedBuildTargetGroup;

            var assemblyTypes = AppDomain.CurrentDomain
                .GetAssemblies()
                .SelectMany(assembly => assembly.GetTypes());

            Type xrGeneralSettingsType = assemblyTypes.FirstOrDefault(type => type.Name == "XRGeneralSettings");
            Type xrGeneralSettingsPerBuildTargetType = assemblyTypes.FirstOrDefault(type => type.Name == "XRGeneralSettingsPerBuildTarget");
            Type xrPackageMetadataStoreType = assemblyTypes.FirstOrDefault(type => type.Name == "XRPackageMetadataStore");

            var buildTargetSettings = (UnityEngine.Object)ScriptableObject.CreateInstance(xrGeneralSettingsPerBuildTargetType);
            if (!EditorBuildSettings.TryGetConfigObject("com.unity.xr.management.loader_settings", out buildTargetSettings))
            {
                // TODO: handle case where TryGetConfigObject failed
                return;
            }

            object settings = xrGeneralSettingsPerBuildTargetType
                .GetMethod("SettingsForBuildTarget", BindingFlags.Public | BindingFlags.Instance)
                .Invoke(buildTargetSettings, new object[] { currentTarget });

            if (settings == null)
            {
                // TODO: handle case where the result of the method call is null
                return;
            }

            PropertyInfo managerProperty = xrGeneralSettingsType.GetProperty("Manager", BindingFlags.Public | BindingFlags.Instance);
            object manager = managerProperty.GetValue(settings);

            MethodInfo methodInfo = xrPackageMetadataStoreType.GetMethod(flag ? "AssignLoader" : "RemoveLoader", BindingFlags.Public | BindingFlags.Static);
            object[] parameters = new object[] { manager, "Unity.XR.MockHMD.MockHMDLoader", currentTarget };
            methodInfo.Invoke(null, parameters);
        }

        private void PlayModeState(PlayModeStateChange state) 
        {
            if (state == PlayModeStateChange.ExitingEditMode)
            {
                if (_testingEnabled) StartXRLoader();
            }
        }
    }
}