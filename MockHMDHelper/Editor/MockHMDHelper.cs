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

        private bool _testingEnabled;

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
        }

        private void OnDisable()
        {
            ToggleMockHMD(false);
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

            string text = !_testingEnabled ? "Enable on PlayMode" : "Disable on PlayMode";
            if (GUILayout.Button(text, GUILayout.Height(EditorGUIUtility.singleLineHeight * 2)))
            {
                _testingEnabled = !_testingEnabled;
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

        public void ToggleMockHMD(bool flag)
        {
            var currentTarget = EditorUserBuildSettings.selectedBuildTargetGroup;
            
            var AllTypes = AppDomain.CurrentDomain.GetAssemblies().SelectMany(assembly => assembly.GetTypes());
            Type xrGeneralSettingsType = AllTypes.FirstOrDefault(type => type.Name == "XRGeneralSettings");
            Type xrGeneralSettingsPerBuildTargetType = AllTypes.FirstOrDefault(type => type.Name == "XRGeneralSettingsPerBuildTarget");
            Type xrPackageMetadataStoreType = AllTypes.FirstOrDefault(type => type.Name == "XRPackageMetadataStore");
            Type xrManagerType = AllTypes.FirstOrDefault(type => type.Name == "XRManager");
            Type xrManagerSettingsType = AllTypes.FirstOrDefault(type => type.Name == "XRManagerSettings");

            var buildTargetSettings = (UnityEngine.Object)ScriptableObject.CreateInstance(xrGeneralSettingsPerBuildTargetType);
            bool success = EditorBuildSettings.TryGetConfigObject("com.unity.xr.management.loader_settings", out buildTargetSettings);
            if (!success)
            {
                // TODO: handle case where TryGetConfigObject failed
                return;
            }

            object settings = xrGeneralSettingsPerBuildTargetType.GetMethod("SettingsForBuildTarget", BindingFlags.Public | BindingFlags.Instance).Invoke(buildTargetSettings, new object[] { currentTarget });
            if (settings == null)
            {
                // TODO: handle case where the result of the method call is null
                return;
            }

            PropertyInfo managerProperty = xrGeneralSettingsType.GetProperty("Manager", BindingFlags.Public | BindingFlags.Instance);
            object manager = managerProperty.GetValue(settings);

            if (flag)
            {
                MethodInfo assignLoaderMethod = xrPackageMetadataStoreType.GetMethod("AssignLoader", BindingFlags.Public | BindingFlags.Static);
                object[] assignLoaderParameters = new object[] { manager, "Unity.XR.MockHMD.MockHMDLoader", currentTarget };
                assignLoaderMethod.Invoke(null, assignLoaderParameters);
            }
            else
            {
                MethodInfo removeLoaderMethod = xrPackageMetadataStoreType.GetMethod("RemoveLoader", BindingFlags.Public | BindingFlags.Static);
                object[] removeLoaderParameters = new object[] { manager, "Unity.XR.MockHMD.MockHMDLoader", currentTarget };
                removeLoaderMethod.Invoke(null, removeLoaderParameters);
            }
        }
    }
}