using System;
using UnityEditor;
using UnityEngine;

namespace Hapio.Editor
{
    /// <summary>Immutable native icon tables, including absent overrides. JSON overwrite is unsafe for unrelated native paths.</summary>
    internal sealed class ShowcaseIconSettingsSnapshot
    {
        private readonly UnityEngine.Object settings;
        private readonly Group[] legacy, platform;
        private sealed class Group
        {
            public string target;
            public Slot[] slots;
        }
        private sealed class Slot
        {
            public int width, height, kind;
            public string subKind;
            public UnityEngine.Object icon;
            public UnityEngine.Object[] textures;
        }

        private ShowcaseIconSettingsSnapshot(UnityEngine.Object settings, Group[] legacy, Group[] platform)
        { this.settings = settings; this.legacy = legacy; this.platform = platform; }

        public static ShowcaseIconSettingsSnapshot Capture()
        {
            var settings = Unsupported.GetSerializedAssetInterfaceSingleton("PlayerSettings");
            if (settings == null) throw new InvalidOperationException("The native PlayerSettings instance is unavailable.");
            using (var serialized = new SerializedObject(settings))
            {
                serialized.Update();
                return new ShowcaseIconSettingsSnapshot(settings,
                    Read(Array(serialized.FindProperty("m_BuildTargetIcons")), false),
                    Read(Array(serialized.FindProperty("m_BuildTargetPlatformIcons")), true));
            }
        }

        public void Restore()
        {
            if (settings == null) throw new InvalidOperationException("The captured native PlayerSettings instance is unavailable.");
            using (var serialized = new SerializedObject(settings))
            {
                serialized.Update();
                var legacyProperty = Array(serialized.FindProperty("m_BuildTargetIcons"));
                var platformProperty = Array(serialized.FindProperty("m_BuildTargetPlatformIcons"));
                // Validate existing tables before editing the serialized buffer. Only Apply mutates the native object.
                Read(legacyProperty, false); Read(platformProperty, true);
                Write(legacyProperty, legacy, false);
                Write(platformProperty, platform, true);
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static Group[] Read(SerializedProperty groups, bool modern)
        {
            var result = new Group[groups.arraySize];
            for (int i = 0; i < result.Length; i++)
            {
                var group = groups.GetArrayElementAtIndex(i);
                var slots = Array(group.FindPropertyRelative("m_Icons"));
                var record = new Group { target = Field(group, "m_BuildTarget", SerializedPropertyType.String).stringValue,
                    slots = new Slot[slots.arraySize] };
                for (int j = 0; j < record.slots.Length; j++)
                {
                    var slot = slots.GetArrayElementAtIndex(j);
                    var item = new Slot { width = Int(slot, "m_Width").intValue, height = Int(slot, "m_Height").intValue,
                        kind = Int(slot, "m_Kind").intValue };
                    if (modern)
                    {
                        item.subKind = Field(slot, "m_SubKind", SerializedPropertyType.String).stringValue;
                        var textures = Array(slot.FindPropertyRelative("m_Textures"));
                        item.textures = new UnityEngine.Object[textures.arraySize];
                        for (int k = 0; k < item.textures.Length; k++)
                            item.textures[k] = Reference(textures.GetArrayElementAtIndex(k)).objectReferenceValue;
                    }
                    else item.icon = Field(slot, "m_Icon", SerializedPropertyType.ObjectReference).objectReferenceValue;
                    record.slots[j] = item;
                }
                result[i] = record;
            }
            return result;
        }

        private static void Write(SerializedProperty groups, Group[] records, bool modern)
        {
            groups.arraySize = records.Length;
            for (int i = 0; i < records.Length; i++)
            {
                var group = groups.GetArrayElementAtIndex(i);
                Field(group, "m_BuildTarget", SerializedPropertyType.String).stringValue = records[i].target;
                var slots = Array(group.FindPropertyRelative("m_Icons"));
                slots.arraySize = records[i].slots.Length;
                for (int j = 0; j < records[i].slots.Length; j++)
                {
                    var slot = slots.GetArrayElementAtIndex(j); var item = records[i].slots[j];
                    Int(slot, "m_Width").intValue = item.width;
                    Int(slot, "m_Height").intValue = item.height;
                    Int(slot, "m_Kind").intValue = item.kind;
                    if (modern)
                    {
                        Field(slot, "m_SubKind", SerializedPropertyType.String).stringValue = item.subKind;
                        var textures = Array(slot.FindPropertyRelative("m_Textures"));
                        textures.arraySize = item.textures.Length;
                        for (int k = 0; k < item.textures.Length; k++)
                            Reference(textures.GetArrayElementAtIndex(k)).objectReferenceValue = item.textures[k];
                    }
                    else Field(slot, "m_Icon", SerializedPropertyType.ObjectReference).objectReferenceValue = item.icon;
                }
            }
        }

        private static SerializedProperty Array(SerializedProperty property)
        {
            if (property == null || !property.isArray || property.propertyType == SerializedPropertyType.String)
                throw new InvalidOperationException("The native icon array schema is unavailable.");
            return property;
        }
        private static SerializedProperty Field(SerializedProperty parent, string name, SerializedPropertyType type)
        {
            var property = parent.FindPropertyRelative(name);
            if (property == null || property.propertyType != type)
                throw new InvalidOperationException("The native icon field schema changed: " + name);
            return property;
        }
        private static SerializedProperty Int(SerializedProperty parent, string name)
        {
            var property = parent.FindPropertyRelative(name);
            if (property == null || property.propertyType != SerializedPropertyType.Integer && property.propertyType != SerializedPropertyType.Enum)
                throw new InvalidOperationException("The native icon integer schema changed: " + name);
            return property;
        }
        private static SerializedProperty Reference(SerializedProperty property)
        {
            if (property == null || property.propertyType != SerializedPropertyType.ObjectReference)
                throw new InvalidOperationException("The native icon texture reference schema changed.");
            return property;
        }
    }
}
