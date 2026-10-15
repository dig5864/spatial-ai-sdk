using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SpatialAI.Editor
{
    [Serializable]
    public sealed class SpatialUvcDeviceId
    {
        [Tooltip("USB vendor ID in decimal (0..65535). HXQ: 4555 / 0x11cb.")]
        public int vendorId = 4555;
        [Tooltip("USB product ID in decimal (0..65535). HXQ: 10267 / 0x281b.")]
        public int productId = 10267;
    }

    [FilePath("ProjectSettings/SpatialAIUvcSettings.asset", FilePathAttribute.Location.ProjectFolder)]
    public sealed class SpatialUvcSettings : ScriptableSingleton<SpatialUvcSettings>
    {
        [SerializeField, Tooltip("Register the main Activity for matching USB attachments. Android may prompt to select this app or launch it automatically when set as default. Requires rebuilding the APK.")]
        private bool associateOnAttach = false;
        [SerializeField] private List<SpatialUvcDeviceId> devices = new List<SpatialUvcDeviceId> { new SpatialUvcDeviceId() };
        public bool AssociateOnAttach { get { return associateOnAttach; } }
        public IReadOnlyList<SpatialUvcDeviceId> Devices { get { return devices; } }

        [SettingsProvider]
        public static SettingsProvider CreateSettingsProvider()
        {
            return new SettingsProvider("Project/Spatial AI/UVC", SettingsScope.Project)
            {
                label = "UVC",
                keywords = new HashSet<string> { "USB", "UVC", "VID", "PID", "Camera" },
                guiHandler = context =>
                {
                    EditorGUILayout.HelpBox("USB Host support is declared automatically as optional. Device association is disabled by default. Enabling it may launch your app when a matching camera is plugged in; it does not guarantee silent permanent USB permission.", MessageType.Info);
                    var settings = new SerializedObject(instance);
                    settings.Update();
                    EditorGUILayout.PropertyField(settings.FindProperty("associateOnAttach"), new GUIContent("Associate On Attach"));
                    EditorGUILayout.PropertyField(settings.FindProperty("devices"), new GUIContent("Devices (decimal VID / PID)"), true);
                    if (settings.ApplyModifiedProperties()) instance.Save(true);
                }
            };
        }
    }
}
