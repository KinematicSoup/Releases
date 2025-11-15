using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using KS.SceneFusion2.Client;
using KS.SceneFusion;
using KS.SF.Reactor;
using KS.SF.Unity.Editor;
using UObject = UnityEngine.Object;

namespace KS.SceneFusion2.Unity.Editor
{
    /// <summary>
    /// Manages syncing of serialized properties. Converts between sfProperties and serialized properties.
    /// </summary>
    public class sfPropertyManager
    {
        /// <summary></summary>
        /// <returns>singleton instance.</returns>
        public static sfPropertyManager Get()
        {
            return m_instance;
        }
        private static sfPropertyManager m_instance = new sfPropertyManager();

        /// <summary>Serialized property change event handler.</summary>
        /// <param name="uobj">uobj whose property changed.</param>
        public delegate void SPropertyChangeHandler(UObject uobj);

        /// <summary>Missing object reference event handler.</summary>
        /// <param name="uobj">uobj that is referencing a missing object.</param>
        public delegate void MissingObjectHandler(UObject uobj);

        /// <summary>Invoked when a reference to a missing object that cannot be synced is found.</summary>
        public event MissingObjectHandler OnMissingObject;

        /// <summary>List change callback.</summary>
        /// <param name="isInsertion">true if elements were added, false if they were removed.</param>
        /// <param name="index">index of the insertion or removal.</param>
        /// <param name="count">number of inserted or removed elements.</param>
        public delegate void ListChangeCallback(bool isInsertion, int index, int count);

        /// <summary>Delegate for getting an sfProperty from a SerializedProperty.</summary>
        /// <param name="sproperty"></param>
        /// <returns>the property as an sfProperty.</returns>
        private delegate sfBaseProperty Getter(SerializedProperty sproperty);

        /// <summary>Delegate for setting a SerializedProperty from an sfProperty.</summary>
        /// <param name="sproperty">sproperty to set.</param>
        /// <param name="property">property value to set sproperty to.</param>
        /// <returns>true if the property value changed.</returns>
        private delegate bool Setter(SerializedProperty sproperty, sfBaseProperty property);

        private Dictionary<SerializedPropertyType, Getter> m_getters =
            new Dictionary<SerializedPropertyType, Getter>();
        private Dictionary<SerializedPropertyType, Setter> m_setters =
            new Dictionary<SerializedPropertyType, Setter>();
        private GameObject m_defaultObject;

        private Dictionary<string, sfTypeEventMap<SPropertyChangeHandler>> m_spropertyChangeHandlers =
            new Dictionary<string, sfTypeEventMap<SPropertyChangeHandler>>();

        private Dictionary<UObject, SerializedObject> m_serializedObjectMap =
            new Dictionary<UObject, SerializedObject>();
        // Maps uobjects to lists of changed properties sfBaseProperty/string tuples where the tuple is the name of the
        // removed field, or null if a field was not removed.
        private Dictionary<UObject, List<Tuple<sfBaseProperty, string>>> m_changedPropertyMap =
            new Dictionary<UObject, List<Tuple<sfBaseProperty, string>>>();
        private List<SerializedObject> m_serializedObjects = new List<SerializedObject>();
        private UndoPropertyModification[] m_delayedModifications;

        // Default value for game object name property.
        private const string DEFAULT_NAME = "GameObject";

        /// <summary>Properties to exclude from syncing.</summary>
        public sfTypePropertyNameMap Blacklist
        {
            get { return m_blacklist; }
        }
        sfTypePropertyNameMap m_blacklist = new sfTypePropertyNameMap();

        /// <summary>Hidden properties to sync.</summary>
        public sfTypePropertyNameMap SyncedHiddenProperties
        {
            get { return m_syncedHiddenProperties; }
        }
        private sfTypePropertyNameMap m_syncedHiddenProperties = new sfTypePropertyNameMap();

        /// <summary>Constructor</summary>
        private sfPropertyManager()
        {
            RegisterSerializer(SerializedPropertyType.AnimationCurve,
                (SerializedProperty sprop) => sfPropertyUtils.FromAnimationCurve(sprop.animationCurveValue),
                (SerializedProperty sprop, sfBaseProperty prop) =>
                {
                    AnimationCurve value = sfPropertyUtils.ToAnimationCurve(prop);
                    if (!sprop.animationCurveValue.Equals(value))
                    {
                        sprop.animationCurveValue = value;
                        return true;
                    }
                    return false;
                });
            RegisterSerializer(SerializedPropertyType.Boolean,
                (SerializedProperty sprop) => sprop.boolValue,
                (SerializedProperty sprop, sfBaseProperty prop) =>
                {
                    bool value = (bool)prop;
                    if (sprop.boolValue != value)
                    {
                        sprop.boolValue = value;
                        return true;
                    }
                    return false;
                });
            RegisterSerializer(SerializedPropertyType.Bounds,
                (SerializedProperty sprop) => sfValueProperty.From(sprop.boundsValue),
                (SerializedProperty sprop, sfBaseProperty prop) =>
                {
                    Bounds value = prop.As<Bounds>();
                    if (sprop.boundsValue != value)
                    {
                        sprop.boundsValue = value;
                        return true;
                    }
                    return false;
                });
            RegisterSerializer(SerializedPropertyType.BoundsInt,
                (SerializedProperty sprop) => sfValueProperty.From(sprop.boundsIntValue),
                (SerializedProperty sprop, sfBaseProperty prop) =>
                {
                    BoundsInt value = prop.As<BoundsInt>();
                    if (sprop.boundsIntValue != value)
                    {
                        sprop.boundsIntValue = value;
                        return true;
                    }
                    return false;
                });
            RegisterSerializer(SerializedPropertyType.Character,
                (SerializedProperty sprop) => sprop.intValue,
                (SerializedProperty sprop, sfBaseProperty prop) =>
                {
                    int value = (int)prop;
                    if (sprop.intValue != value)
                    {
                        sprop.intValue = value;
                        return true;
                    }
                    return false;
                });
            RegisterSerializer(SerializedPropertyType.Color,
                (SerializedProperty sprop) => sfValueProperty.From(sprop.colorValue),
                (SerializedProperty sprop, sfBaseProperty prop) =>
                {
                    Color value = prop.As<Color>();
                    if (sprop.colorValue != value)
                    {
                        sprop.colorValue = value;
                        return true;
                    }
                    return false;
                });
            RegisterSerializer(SerializedPropertyType.Enum,
                (SerializedProperty sprop) => sprop.intValue,
                (SerializedProperty sprop, sfBaseProperty prop) =>
                {
                    int value = (int)prop;
                    if (sprop.intValue != value)
                    {
                        sprop.intValue = value;
                        return true;
                    }
                    return false;
                });
            RegisterSerializer(SerializedPropertyType.ExposedReference, GetExposedReference, SetExposedReference);
            RegisterSerializer(SerializedPropertyType.Float, GetFloat, SetFloat);
            RegisterSerializer(SerializedPropertyType.Generic, GetGeneric, SetGeneric);
            RegisterSerializer(SerializedPropertyType.Gradient,
                (SerializedProperty sprop) => sfPropertyUtils.FromGradient(sfPropertyUtils.GetGradient(sprop)),
                (SerializedProperty sprop, sfBaseProperty prop) =>
                {
                    Gradient value = sfPropertyUtils.ToGradient(prop);
                    if (!sfPropertyUtils.GetGradient(sprop).Equals(value))
                    {
                        sfPropertyUtils.SetGradient(sprop, value);
                        return true;
                    }
                    return false;
                });
            RegisterSerializer(SerializedPropertyType.Integer, GetInteger, SetInteger);
            RegisterSerializer(SerializedPropertyType.LayerMask,
                (SerializedProperty sprop) => sprop.intValue,
                (SerializedProperty sprop, sfBaseProperty prop) =>
                {
                    int value = (int)prop;
                    if (sprop.intValue != value)
                    {
                        sprop.intValue = value;
                        return true;
                    }
                    return false;
                });
            RegisterSerializer(SerializedPropertyType.ObjectReference, GetObject, SetObject);
            RegisterSerializer(SerializedPropertyType.Quaternion,
                (SerializedProperty sprop) => sfValueProperty.From(sprop.quaternionValue),
                (SerializedProperty sprop, sfBaseProperty prop) =>
                {
                    Quaternion value = prop.As<Quaternion>();
                    if (sprop.quaternionValue != value)
                    {
                        sprop.quaternionValue = value;
                        return true;
                    }
                    return false;
                });
            RegisterSerializer(SerializedPropertyType.Rect,
                (SerializedProperty sprop) => sfValueProperty.From(sprop.rectValue),
                (SerializedProperty sprop, sfBaseProperty prop) =>
                {
                    Rect value = prop.As<Rect>();
                    if (sprop.rectValue != value)
                    {
                        sprop.rectValue = value;
                        return true;
                    }
                    return false;
                });
            RegisterSerializer(SerializedPropertyType.RectInt,
                (SerializedProperty sprop) => sfValueProperty.From(sprop.rectIntValue),
                (SerializedProperty sprop, sfBaseProperty prop) =>
                {
                    RectInt value = prop.As<RectInt>();
                    if (!sprop.rectIntValue.Equals(value))
                    {
                        sprop.rectIntValue = value;
                        return true;
                    }
                    return false;
                });
            RegisterSerializer(SerializedPropertyType.String,
                (SerializedProperty sprop) => sprop.stringValue,
                (SerializedProperty sprop, sfBaseProperty prop) =>
                {
                    string value = (string)prop;
                    if (sprop.stringValue != value)
                    {
                        sprop.stringValue = value;
                        return true;
                    }
                    return false;
                });
            RegisterSerializer(SerializedPropertyType.Vector2,
                (SerializedProperty sprop) => sfValueProperty.From(sprop.vector2Value),
                (SerializedProperty sprop, sfBaseProperty prop) =>
                {
                    Vector2 value = prop.As<Vector2>();
                    if (sprop.vector2Value != value)
                    {
                        sprop.vector2Value = value;
                        return true;
                    }
                    return false;
                });
            RegisterSerializer(SerializedPropertyType.Vector2Int,
                (SerializedProperty sprop) => sfValueProperty.From(sprop.vector2IntValue),
                (SerializedProperty sprop, sfBaseProperty prop) =>
                {
                    Vector2Int value = prop.As<Vector2Int>();
                    if (sprop.vector2IntValue != value)
                    {
                        sprop.vector2IntValue = value;
                        return true;
                    }
                    return false;
                });
            RegisterSerializer(SerializedPropertyType.Vector3,
                (SerializedProperty sprop) => sfValueProperty.From(sprop.vector3Value),
                (SerializedProperty sprop, sfBaseProperty prop) =>
                {
                    Vector3 value = prop.As<Vector3>();
                    if (sprop.vector3Value != value)
                    {
                        sprop.vector3Value = value;
                        return true;
                    }
                    return false;
                });
            RegisterSerializer(SerializedPropertyType.Vector3Int,
                (SerializedProperty sprop) => sfValueProperty.From(sprop.vector3IntValue),
                (SerializedProperty sprop, sfBaseProperty prop) =>
                {
                    Vector3Int value = prop.As<Vector3Int>();
                    if (sprop.vector3IntValue != value)
                    {
                        sprop.vector3IntValue = value;
                        return true;
                    }
                    return false;
                });
            RegisterSerializer(SerializedPropertyType.Vector4,
                (SerializedProperty sprop) => sfValueProperty.From(sprop.vector4Value),
                (SerializedProperty sprop, sfBaseProperty prop) =>
                {
                    Vector4 value = prop.As<Vector4>();
                    if (sprop.vector4Value != value)
                    {
                        sprop.vector4Value = value;
                        return true;
                    }
                    return false;
                });
        }

        /// <summary>
        /// Registers delegates for converting between sfProperty and serialized property for a serialized property
        /// type.
        /// </summary>
        /// <param name="type">type to register delegates for.</param>
        /// <param name="getter">getter for getting an sfProperty from a serialized property.</param>
        /// <param name="setter">setter for setting a serialized property from an sfProperty.</param>
        private void RegisterSerializer(SerializedPropertyType type, Getter getter, Setter setter)
        {
            m_getters[type] = getter;
            m_setters[type] = setter;
        }

        /// <summary>
        /// Gets the cached serialized object for a uobject if one exists, otherwise creates one and caches it.
        /// Modifications to the serialized object will be applied when Scene Fusion finishes processing updates from
        /// the server, or when ApplySerializedProperties is called. If you need to modify a UObject in response to a
        /// Scene Fusion event, you should call this function and apply your changes to the returned serialized object
        /// to avoid those changes be overwriten by later updates, or call ApplySerializedProperties before making your
        /// changes.
        /// </summary>
        /// <param name="uobj">uobj to get serialized object for.</param>
        public SerializedObject GetSerializedObject(UObject uobj)
        {
            SerializedObject so;
            if (!m_serializedObjectMap.TryGetValue(uobj, out so))
            {
                so = new SerializedObject(uobj);
                m_serializedObjectMap[uobj] = so;
                m_serializedObjects.Add(so);
            }
            return so;
        }

        /// <summary>
        /// Applies serialized properties to all cached serialized objects. Invokes post property and post uobject
        /// change events on translators for the modified uobjects.
        /// </summary>
        public void ApplySerializedProperties()
        {
            for (int i = 0; i < m_serializedObjects.Count; i++)
            {
                ApplySerializedProperties(m_serializedObjects[i]);
            }
            m_serializedObjects.Clear();
            m_serializedObjectMap.Clear();
            m_changedPropertyMap.Clear();
        }

        /// <summary>
        /// Applies serialized properties from the cached serialized object for a uobject and removes it from the cache.
        /// Invokes post property and post uobject change events on the uobject's translator for the modified
        /// properties.
        /// </summary>
        /// <param name="uobj"></param>
        public void ApplySerializedProperties(UObject uobj)
        {
            if (uobj == null)
            {
                return;
            }
            SerializedObject so;
            if (m_serializedObjectMap.Remove(uobj, out so))
            {
                ApplySerializedProperties(so);
                m_changedPropertyMap.Remove(uobj);
            }
        }

        /// <summary>
        /// Applies serialized properties to a serialized object. Invokes post property and post uobject change events
        /// on the uobject's translator for the modified properties.
        /// </summary>
        /// <param name="so"></param>
        private void ApplySerializedProperties(SerializedObject so)
        {
            if (so.targetObject != null && sfPropertyUtils.ApplyProperties(so))
            {
                sfObject obj = sfObjectMap.Get().GetSFObject(so.targetObject);
                sfBaseUObjectTranslator translator = sfObjectEventDispatcher.Get()
                    .GetTranslator<sfBaseUObjectTranslator>(obj);
                if (translator != null)
                {
                    // Strings are the name of the removed dictionary field or null.
                    List<Tuple<sfBaseProperty, string>> properties;
                    if (m_changedPropertyMap.TryGetValue(so.targetObject, out properties))
                    {
                        for (int j = 0; j < properties.Count; j++)
                        {
                            Tuple<sfBaseProperty, string> pair = properties[j];
                            translator.CallPostPropertyChangeHandlers(so.targetObject, pair.Item1, pair.Item2);
                        }
                    }
                    translator.CallPostUObjectChangeHandlers(so.targetObject);
                }
            }
        }

        /// <summary>
        /// Queues a post property change event to be invoked when properties are applied to cached serialized objects,
        /// either when Scene Fusion finishes processing updates from the server, or when ApplySerializedProperties is
        /// called.
        /// </summary>
        /// <param name="uobj">uobj with the changed property.</param>
        /// <param name="property">property that changed.</param>
        /// <param name="name">
        /// if non-empty, the name of the sub-property that was removed from a dictionary
        /// property.
        /// </param>
        public void QueuePropertyChangeEvent(UObject uobj, sfBaseProperty property, string name = null)
        {
            List<Tuple<sfBaseProperty, string>> properties;
            if (!m_changedPropertyMap.TryGetValue(uobj, out properties))
            {
                properties = new List<Tuple<sfBaseProperty, string>>();
                m_changedPropertyMap[uobj] = properties;
            }
            properties.Add(new Tuple<sfBaseProperty, string>(property, name));
        }

        /// <summary>Converts a serialized property to an sfProperty.</summary>
        /// <param name="sprop"></param>
        /// <returns></returns>
        public sfBaseProperty GetValue(SerializedProperty sprop)
        {
            if (sprop == null)
            {
                return null;
            }
            Getter getter;
            return m_getters.TryGetValue(sprop.propertyType, out getter) ? getter(sprop) : null;
        }

        /// <summary>Sets a serialized property to a value from an sfProperty.</summary>
        /// <param name="sprop">sprop to set.</param>
        /// <error>sfBaseProperty prop to get value from.</error>
        /// <returns>true if the value changed.</returns>
        public bool SetValue(SerializedProperty sprop, sfBaseProperty prop)
        {
            if (sprop == null || prop == null)
            {
                return false;
            }
            Setter setter;
            if (m_setters.TryGetValue(sprop.propertyType, out setter))
            {
                try
                {
                    return setter(sprop, prop);
                }
                catch (InvalidCastException)
                {
                    ksLog.Warning(this, "Invalid cast exception setting property " + sprop.propertyPath + " on " +
                        sprop.serializedObject.targetObject + ". This usually means you and another user have " +
                        " conflicting types for the same property.", sprop.serializedObject.targetObject);
                }
                catch (Exception e)
                {
                    ksLog.Error(this, "Exception setting property " + sprop.propertyPath + " on " +
                        sprop.serializedObject.targetObject, e, sprop.serializedObject.targetObject);
                }
            }
            return false;
        }

        /// <summary>
        /// Checks if a serialized property has its default value. For non-prefab properties, this checks against the
        /// default script value.
        /// </summary>
        /// <param name="sprop"></param>
        /// <returns>true if the property has its default value.</returns>
        public bool IsDefaultValue(SerializedProperty sprop)
        {
            return IsDefaultValue(sprop, true);
        }

        /// <summary>
        /// Checks if a serialized property has its default value. For non-prefab properties, this checks against the
        /// default script value.
        /// </summary>
        /// <param name="sprop"></param>
        /// <param name="destroyDefaultComponent">
        /// if the property is not a prefab property, we compare the value
        /// against a default instance of the component. If we don't already have a default instance, we create
        /// one. If this is true, we will destroy the default component after comparing its value. If this is
        /// false, we keep the default component around so we can reuse it.
        /// </param>
        /// <returns>true if the property has its default value.</returns>
        private bool IsDefaultValue(SerializedProperty sprop, bool destroyDefaultComponent)
        {
            if (PrefabUtility.GetCorrespondingObjectFromSource(sprop.serializedObject.targetObject) != null)
            {
                return !sprop.prefabOverride;
            }
            if (sprop.serializedObject.targetObject is GameObject)
            {
                switch (sprop.propertyPath)
                {
                    // The default object is disabled so it's scripts don't run, but we want the default active value
                    // to be true.
                    case "m_IsActive": return sprop.boolValue;
                    // The default object has a unique name that we don't want used as the default name value.
                    case "m_Name": return sprop.stringValue == DEFAULT_NAME;
                }
            }
            UObject defaultObject = GetDefaultObject(sprop.serializedObject.targetObject);
            if (defaultObject == null)
            {
                //TODO: check default type value, eg. int == 0, object reference == null.
                return false;
            }
            SerializedProperty defaultProperty = new SerializedObject(defaultObject).FindProperty(
                sprop.propertyPath);

            bool result =
#if !UNITY_2020_1_OR_NEWER
                // Unity 2019 has an uncatchable error that crashes Unity when you call DataEquals on generic module
                // types for the ParticleSystem component and possibly other components.
                !(sprop.propertyType == SerializedPropertyType.Generic && sprop.type.Contains("Module") &&
                !(sprop.serializedObject.targetObject is MonoBehaviour) &&
                !(sprop.serializedObject.targetObject is ScriptableObject)) &&
#endif
                SerializedProperty.DataEquals(sprop, defaultProperty);
            if (destroyDefaultComponent && defaultObject is Component && !(defaultObject is Transform))
            {
                DestroyDefaultComponents();
            }
            return result;
        }

        /// <summary>
        /// Sets a serialized property to its default value. For non-prefab properties, this sets it to the default
        /// script value.
        /// </summary>
        /// <param name="sprop">sprop to set to default value.</param>
        public void SetToDefaultValue(SerializedProperty sprop)
        {
            SetToDefaultValue(sprop, true);
        }

        /// <summary>
        /// Sets a serialized property to its default value. For non-prefab properties, this sets it to the default
        /// script value.
        /// </summary>
        /// <param name="sprop">sprop to set to default value.</param>
        /// <param name="destroyDefaultComponent">
        /// if the property is not a prefab property, we get the default value
        /// from a default instance of the component. If we don't already have a default instance, we create
        /// one. If this is true, we will destroy the default component after getting its value. If this is
        /// false, we keep the default component around so we can resuse it.
        /// </param>
        /// <returns>true if the value of the property changed.</returns>
        private bool SetToDefaultValue(SerializedProperty sprop, bool destroyDefaultComponent)
        {
            if (!m_getters.ContainsKey(sprop.propertyType))
            {
                return false;
            }
            if (PrefabUtility.GetCorrespondingObjectFromSource(sprop.serializedObject.targetObject) != null)
            {
                if (sprop.prefabOverride)
                {
                    sprop.prefabOverride = false;
                    // Clearing a prefab instance override will also clear HideFlags.NotEditable, so we relock all the
                    // game objects in the prefab instance on the next PreUpdate.
                    GameObject gameObject = sfUnityUtils.GetGameObject(sprop.serializedObject.targetObject);
                    if (gameObject != null && (gameObject.hideFlags & HideFlags.NotEditable) != 0)
                    {
                        sfGameObjectTranslator translator = sfObjectEventDispatcher.Get()
                            .GetTranslator<sfGameObjectTranslator>(sfType.GameObject);
                        translator.RelockPrefabNextPreUpdate(gameObject);
                    }
                    return true;
                }
                return false;
            }
            if (sprop.serializedObject.targetObject is GameObject)
            {
                switch (sprop.propertyPath)
                {
                    case "m_IsActive":
                    {
                        // The default object is disabled so it's scripts don't run, but we want the default active value
                        // to be true.
                        if (sprop.boolValue)
                        {
                            return false;
                        }
                        sprop.boolValue = true;
                        return true;
                    }
                    case "m_Name":
                    {
                        // The default object has a unique name that we don't want used as the default name value.
                        if (sprop.stringValue == DEFAULT_NAME)
                        {
                            return false;
                        }
                        sprop.stringValue = DEFAULT_NAME;
                        return true;
                    }
                }
            }
            UObject defaultObject = GetDefaultObject(sprop.serializedObject.targetObject);
            if (defaultObject == null)
            {
                return false;
            }
            SerializedProperty defaultProperty = new SerializedObject(defaultObject).FindProperty(
                sprop.propertyPath);
            bool changed = sprop.serializedObject.CopyFromSerializedPropertyIfDifferent(defaultProperty);
            if (destroyDefaultComponent && defaultObject is Component && !(defaultObject is Transform))
            {
                DestroyDefaultComponents();
            }
            return changed;
        }

        /// <summary>Gets a default instance of an object.</summary>
        /// <param name="uobj">uobj to get default instance of.</param>
        /// <returns>default instance of the object.</returns>
        private UObject GetDefaultObject(UObject uobj)
        {
            try
            {
                if (m_defaultObject == null)
                {
                    m_defaultObject = new GameObject("#SF Default Object");
                    m_defaultObject.hideFlags = HideFlags.HideAndDontSave;
                    m_defaultObject.SetActive(false);
                }
                if (uobj is GameObject)
                {
                    return m_defaultObject;
                }
                if (uobj is Component)
                {
                    Component component = m_defaultObject.GetComponent(uobj.GetType());
                    if (component == null)
                    {
                        component = m_defaultObject.AddComponent(uobj.GetType());
                    }
                    return component;
                }
                //TODO: ScriptableObject
            }
            catch (Exception e)
            {
                ksLog.Error(this, "Exception getting default object for " + uobj.GetType() + ".", e);
            }
            return null;
        }

        /// <summary>
        /// Iterates all properties of an object using Unity serialization and creates sfProperties for properties with
        /// non-default values as fields in an sfDictionaryProperty.
        /// </summary>
        /// <param name="uobj">uobj to create properties for.</param>
        /// <param name="dict">dict to add properties to.</param>
        public void CreateProperties(UObject uobj, sfDictionaryProperty dict)
        {
            if (uobj == null || dict == null)
            {
                return;
            }
            SerializedObject so = new SerializedObject(uobj);
            foreach (SerializedProperty sprop in Iterate(so.GetIterator()))
            {
                if (IsDefaultValue(sprop, false))
                {
                    continue;
                }
                string propertyName = sprop.name;
                sfBaseProperty property = GetValue(sprop);
                if (property != null)
                {
                    dict[propertyName] = property;
                }
            }
            if (uobj is Component && m_defaultObject != null && !(uobj is Transform))
            {
                DestroyDefaultComponents();
            }
        }

        /// <summary>
        /// Applies property values from an sfDictionaryProperty to an object using Unity serialization.
        /// </summary>
        /// <param name="uobj">uobj to apply property values to.</param>
        /// <param name="dict">
        /// dict to get property values from. If a value for a property is not in the
        /// dictionary, sets the property to its default value.
        /// </param>
        public void ApplyProperties(UObject uobj, sfDictionaryProperty dict)
        {
            if (uobj == null || dict == null)
            {
                return;
            }
            SerializedObject so = GetSerializedObject(uobj);
            foreach (SerializedProperty sprop in Iterate(so.GetIterator()))
            {
                sfBaseProperty property;
                if (dict.TryGetField(sprop.name, out property))
                {
                    if (SetValue(sprop, property))
                    {
                        QueuePropertyChangeEvent(uobj, property);
                    }
                }
                else if (SetToDefaultValue(sprop, false))
                {
                    QueuePropertyChangeEvent(uobj, dict, sprop.name);
                }
            }
            if (uobj is Component && m_defaultObject != null && !(uobj is Transform))
            {
                DestroyDefaultComponents();
            }
            ApplySerializedProperties(uobj);
        }

        /// <summary>
        /// Checks if a uobject has the same serialized property values as a dictionary property, and that properties
        /// that are missing from the dictionary have the default value.
        /// </summary>
        /// <param name="uobj">uobj to check property values of.</param>
        /// <param name="dict">dict to check property values against.</param>
        /// <returns>
        /// true if the uobject's serialized properties have the same values as the dictionary properties,
        /// and that properties that are missing from the dictionary have the default value.
        /// </returns>
        public bool HasSameProperties(UObject uobj, sfDictionaryProperty dict)
        {
            if (uobj == null || dict == null)
            {
                return false;
            }
            SerializedObject so = new SerializedObject(uobj);
            foreach (SerializedProperty sprop in Iterate(so.GetIterator()))
            {
                sfBaseProperty property;
                if (dict.TryGetField(sprop.name, out property))
                {
                    // Set value returns true if the value was different. We don't call ApplyProperties so the value
                    // doesn't get applied to the UObject.
                    if (SetValue(sprop, property))
                    {
                        return false;
                    }
                }
                else if (!IsDefaultValue(sprop))
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Iterates all properties of an object and updates an sfDictionaryProperty when its values are different from
        /// those on the object. Removes fields from the dictionary for properties that have their default value.
        /// </summary>
        /// <param name="uobj">uobj to iterate properties on.</param>
        /// <param name="dict">dict to update.</param>
        public void SendPropertyChanges(UObject uobj, sfDictionaryProperty dict)
        {
            if (uobj == null || dict == null)
            {
                return;
            }
            SerializedObject so = new SerializedObject(uobj);
            foreach (SerializedProperty sprop in Iterate(so.GetIterator()))
            {
                if (IsDefaultValue(sprop, false))
                {
                    dict.RemoveField(sprop.name);
                }
                else
                {
                    sfBaseProperty property = GetValue(sprop);
                    if (property == null)
                    {
                        return;
                    }

                    sfBaseProperty oldProperty;
                    if (!dict.TryGetField(sprop.name, out oldProperty) || !Copy(oldProperty, property))
                    {
                        dict[sprop.name] = property;
                    }
                }
            }
            if (uobj is Component && m_defaultObject != null && !(uobj is Transform))
            {
                DestroyDefaultComponents();
            }
        }

        /// <summary>Updates a subproperty of a dictionary if the value is different from the current value.</summary>
        /// <param name="dict">dict to update.</param>
        /// <param name="field">name of field to update.</param>
        /// <param name="property">property value to set.</param>
        public void UpdateDictProperty(sfDictionaryProperty dict, string field, sfBaseProperty property)
        {
            sfBaseProperty current;
            if (!dict.TryGetField(field, out current) || !Copy(current, property))
            {
                dict[field] = property;
            }
        }

        /// <summary>Copies the data from one property into another if they are the same property type.</summary>
        /// <param name="dest">dest to copy into.</param>
        /// <param name="src">src to copy from.</param>
        /// <returns>false if the properties were not the same type.</returns>
        public bool Copy(sfBaseProperty dest, sfBaseProperty src)
        {
            if (dest == null || src == null || dest.Type != src.Type)
            {
                return false;
            }
            switch (dest.Type)
            {
                case sfBaseProperty.Types.VALUE:
                {
                    if (!dest.Equals(src))
                    {
                        ((sfValueProperty)dest).Value = ((sfValueProperty)src).Value;
                    }
                    break;
                }
                case sfBaseProperty.Types.REFERENCE:
                {
                    if (!dest.Equals(src))
                    {
                        ((sfReferenceProperty)dest).ObjectId = ((sfReferenceProperty)src).ObjectId;
                    }
                    break;
                }
                case sfBaseProperty.Types.STRING:
                {
                    if (!dest.Equals(src))
                    {
                        ((sfStringProperty)dest).String = ((sfStringProperty)src).String;
                    }
                    break;
                }
                case sfBaseProperty.Types.LIST:
                {
                    CopyList((sfListProperty)dest, (sfListProperty)src);
                    break;
                }
                case sfBaseProperty.Types.DICTIONARY:
                {
                    CopyDict((sfDictionaryProperty)dest, (sfDictionaryProperty)src);
                    break;
                }
            }
            return true;
        }

        /// <summary>Registers event handlers for syncing property changes.</summary>
        public void Start()
        {
            sfUnityEventDispatcher.Get().OnModifyProperties += OnModifyProperties;
            sfUnityEventDispatcher.Get().OnPropertiesChanged += OnPropertiesChanged;
            SceneFusion.Get().Service.Session.PostProcessFrame += ApplySerializedProperties;
        }

        /// <summary>
        /// Destroys the default object used for getting default property values and unregisters event handlers.
        /// </summary>
        public void Stop()
        {
            sfUnityEventDispatcher.Get().OnModifyProperties -= OnModifyProperties;
            sfUnityEventDispatcher.Get().OnPropertiesChanged -= OnPropertiesChanged;
            SceneFusion.Get().Service.Session.PostProcessFrame -= ApplySerializedProperties;
            if (m_defaultObject != null)
            {
                UObject.DestroyImmediate(m_defaultObject, true);
                m_defaultObject = null;
            }
        }

        /// <summary>Gets the serialized property change event map for a property.</summary>
        /// <param name="name">name of property to get event map for.</param>
        /// <returns></returns>
        public sfTypeEventMap<SPropertyChangeHandler> OnSPropertyChange(string name)
        {
            sfTypeEventMap<SPropertyChangeHandler> map;
            if (!m_spropertyChangeHandlers.TryGetValue(name, out map))
            {
                map = new sfTypeEventMap<SPropertyChangeHandler>();
                m_spropertyChangeHandlers[name] = map;
            }
            return map;
        }

        /// <summary>
        /// Adds, removes, and/or sets elements in a destination list to make it the same as a source list.
        /// </summary>
        /// <param name="dest">dest to modify.</param>
        /// <param name="src">src to make dest a copy of.</param>
        /// <param name="changeCallback">
        /// changeCallback to call for added or removed elements. Called after adding
        /// elements and before removing elements.
        /// </param>
        public void CopyList(sfListProperty dest, sfListProperty src, ListChangeCallback changeCallback = null)
        {
            // Compares the src list values in lock step with the dest list values. When there is a discrepancy and the
            // list sizes are different, we first check for an element removal (Current src value = Next dest value).
            // Next we check for an element insertion (Next src value = Current dest value). Finally if neither of the
            // above cases were found, we replace the current dest value with the current src value.
            List<sfBaseProperty> toAdd = new List<sfBaseProperty>();
            for (int i = 0; i < src.Count; i++)
            {
                sfBaseProperty element = src[i];
                if (dest.Count <= i)
                {
                    toAdd.Add(element);
                    continue;
                }
                if (element.Equals(dest[i]))
                {
                    continue;
                }
                if (src.Count != dest.Count)
                {
                    // if the current src element matches the next next element, remove the current dest element.
                    if (dest.Count > i + 1 && element.Equals(dest[i + 1]))
                    {
                        if (changeCallback != null)
                        {
                            changeCallback(false, i, 1);
                        }
                        dest.RemoveAt(i);
                        continue;
                    }
                    // if the current dest element matches the next src element, insert the current src element.
                    if (src.Count > i + 1 && dest[i].Equals(src[i + 1]))
                    {
                        dest.Insert(i, element);
                        if (changeCallback != null)
                        {
                            changeCallback(true, i, 1);
                        }
                        i++;
                        continue;
                    }
                }
                if (!Copy(dest[i], element))
                {
                    dest[i] = element;
                }
            }
            if (toAdd.Count > 0)
            {
                dest.AddRange(toAdd);
                if (changeCallback != null)
                {
                    changeCallback(true, dest.Count - toAdd.Count, toAdd.Count);
                }
            }
            else if (dest.Count > src.Count)
            {
                if (changeCallback != null)
                {
                    changeCallback(false, src.Count, dest.Count - src.Count);
                }
                dest.RemoveRange(src.Count, dest.Count - src.Count);
            }
        }

        /// <summary>
        /// Adds, removes, and/or sets fields in a destination dictionary so to make it the same as a source dictionary.
        /// </summary>
        /// <param name="dest">dest to modify.</param>
        /// <param name="src">src to make destPtr a copy of.</param>
        private void CopyDict(sfDictionaryProperty dest, sfDictionaryProperty src)
        {
            List<string> toRemove = new List<string>();
            foreach (KeyValuePair<string, sfBaseProperty> pair in dest)
            {
                if (!src.HasField(pair.Key))
                {
                    toRemove.Add(pair.Key);
                }
            }
            foreach (string key in toRemove)
            {
                dest.RemoveField(key);
            }
            foreach (KeyValuePair<string, sfBaseProperty> pair in src)
            {
                sfBaseProperty destProp;
                if (!dest.TryGetField(pair.Key, out destProp) || !Copy(destProp, pair.Value))
                {
                    dest[pair.Key] = pair.Value;
                }
            }
        }

        /// <summary>Invokes the serialized property change event for a property.</summary>
        /// <param name="uobj">uobj whose property changed.</param>
        /// <param name="name">name of changed property.</param>
        private void CallSPropertyChangeHandlers(string name, UObject uobj)
        {
            sfTypeEventMap<SPropertyChangeHandler> map;
            if (m_spropertyChangeHandlers.TryGetValue(name, out map))
            {
                SPropertyChangeHandler handlers = map.GetHandlers(uobj.GetType());
                if (handlers != null)
                {
                    handlers(uobj);
                }
            }
        }

        /// <summary>
        /// Called when Unity serialized properties change. If the user is not dragging an asset, processes the changes
        /// now. Otherwise processes the changes at the end of the frame unless an undo occurs between now in then, in
        /// which case the changes are discarded. This is to prevent sending spam changes when dragging assets such as
        /// materials into the scene which can trigger a property change and an undo every single frame.
        /// </summary>
        /// <param name="modifications"></param>
        /// <returns>modifications that are allowed.</returns>
        public UndoPropertyModification[] OnModifyProperties(UndoPropertyModification[] modifications)
        {
            if (modifications == null || sfUndoManager.Get().UndoNextOperation)
            {
                return modifications;
            }
            if (DragAndDrop.objectReferences.Length > 0)
            {
                // The user is dragging an asset such as a material. Delay processing until the end of the frame.
                // Cancel processing if an undo is triggered before then.
                if (!sfUndoManager.Get().IsHandlingUndoRedo)
                {
                    if (m_delayedModifications == null)
                    {
                        m_delayedModifications = modifications;
                        EditorApplication.delayCall += ProcessDelayedModifications;
                        Undo.undoRedoPerformed += ClearDelayedModifications;
                    }
                    else
                    {
                        m_delayedModifications = m_delayedModifications.Concat(modifications).ToArray();
                    }
                }
            }
            else
            {
                ProcessModifiedProperties(modifications);
            }
            return modifications;
        }

        /// <summary>Clears the delayed property modifications.</summary>
        private void ClearDelayedModifications()
        {
            m_delayedModifications = null;
        }

        /// <summary>
        /// Processes property modifications that were delayed because the user was dragging an asset.
        /// </summary>
        private void ProcessDelayedModifications()
        {
            Undo.undoRedoPerformed -= ClearDelayedModifications;
            ProcessModifiedProperties(m_delayedModifications);
            m_delayedModifications = null;
        }

        /// <summary>
        /// Processes modifed Unity properties. If the uobject is locked, prevents the property from changing. Otherwise
        /// calls OnSPropertyChange on the translator for the uobject whose property changed. If that returns false,
        /// syncs the property change.
        /// </summary>
        /// <param name="modifications"></param>
        private void ProcessModifiedProperties(UndoPropertyModification[] modifications)
        {
            if (modifications == null)
            {
                return;
            }
            // Maps uobjects to modified property paths
            Dictionary<UObject, HashSet<string>> propertyMap = new Dictionary<UObject, HashSet<string>>();
            foreach (UndoPropertyModification modification in modifications)
            {
                if (modification.currentValue.target == null)
                {
                    continue;
                }
                //ksLog.Debug($"UObject {modification.currentValue.target.GetType().Name} modified {modification.currentValue.propertyPath}");
                CallSPropertyChangeHandlers(modification.currentValue.propertyPath, modification.currentValue.target);
                sfObject obj = sfObjectMap.Get().GetSFObject(modification.currentValue.target);
                if (obj == null || !obj.IsSyncing)
                {
                    // Sync the object if it can be synced.
                    sfObjectEventDispatcher.Get().TryCreateSFObject(modification.currentValue.target);
                    continue;
                }
                // Get the root property path. We reconstruct the sfProperty for the whole root property instead of the
                // subproperty.
                string path = modification.currentValue.propertyPath;
                int index = path.IndexOf(".");
                if (index >= 0)
                {
                    path = path.Substring(0, index);
                }
                // Add the path to the modified property paths set.
                HashSet<string> properties;
                if (!propertyMap.TryGetValue(modification.currentValue.target, out properties))
                {
                    properties = new HashSet<string>();
                    propertyMap[modification.currentValue.target] = properties;
                }
                properties.Add(path);
            }
            // For each uobject with modified properties
            foreach (KeyValuePair<UObject, HashSet<string>> pair in propertyMap)
            {
                sfObject obj = sfObjectMap.Get().GetSFObject(pair.Key);
                HashSet<string> paths = pair.Value;// paths of properties that were modified
                SerializedObject so = new SerializedObject(pair.Key);
                bool revertedProperties = false;
                // Iterate the properties looking for properties with paths in the modified property paths set.
                foreach (SerializedProperty sprop in Iterate(so.GetIterator()))
                {
                    if (paths.Count == 0)
                    {
                        break;
                    }
                    if (!paths.Remove(sprop.propertyPath))
                    {
                        // This property was not modified
                        continue;
                    }
                    sfBaseTranslator translator = sfObjectEventDispatcher.Get().GetTranslator(obj);
                    if (translator != null && translator.OnSPropertyChange(obj, sprop))
                    {
                        // The translator handled the property change event, so we don't need to sync it.
                        continue;
                    }
                    // Sync the property
                    sfDictionaryProperty dict = (sfDictionaryProperty)obj.Property;
                    if (obj.IsLocked)
                    {
                        // Revert to the server value
                        sfBaseProperty property;
                        if (dict.TryGetField(sprop.name, out property))
                        {
                            SetValue(sprop, property);
                        }
                        else
                        {
                            SetToDefaultValue(sprop, false);
                        }
                        revertedProperties = true;
                    }
                    else if (IsDefaultValue(sprop))
                    {
                        dict.RemoveField(sprop.propertyPath);
                    }
                    else
                    {
                        sfBaseProperty property = GetValue(sprop);
                        if (property != null)
                        {
                            sfBaseProperty oldProperty;
                            if (!dict.TryGetField(sprop.name, out oldProperty) || !Copy(oldProperty, property))
                            {
                                dict[sprop.name] = property;
                            }
                        }
                    }
                }
                if (revertedProperties)
                {
                    sfPropertyUtils.ApplyProperties(so);
                    if (pair.Key is Component && m_defaultObject != null && !(pair.Key is Transform))
                    {
                        DestroyDefaultComponents();
                    }
                }
            }
        }

        /// <summary>
        /// Called when properties on a uobject changed but we don't know which properties changed. Checks for and syncs
        /// property changes. If the object is locked, reverts the properties to the server values.
        /// </summary>
        /// <param name="uobj">uobj whose properties changed.</param>
        private void OnPropertiesChanged(UObject uobj)
        {
            sfObject obj = sfObjectMap.Get().GetSFObject(uobj);
            if (obj == null || !obj.IsSyncing)
            {
                // Sync the object if it can be synced.
                sfObjectEventDispatcher.Get().TryCreateSFObject(uobj);
                return;
            }
            sfDictionaryProperty properties = (sfDictionaryProperty)obj.Property;
            sfDictionaryProperty oldProperties = null;
            if (obj.IsLocked)
            {
                ApplyProperties(uobj, properties);
            }
            else
            {
                if (!sfUndoManager.Get().IsHandlingUndoRedo && PrefabUtility.IsPartOfPrefabInstance(uobj))
                {
                    oldProperties = (sfDictionaryProperty)properties.Clone();
                }
                SendPropertyChanges(uobj, properties);
            }

            // If a RegisterCompleteObjectUndo undo operation exists on the undo stack and a child object has been re-
            // parented between the creation of the undo operation and the reversion of the undo operation, then an
            // invalid hierarchy will result.  The child object will be restored to the original position whilst
            // simultaneously remaining as a child of the new parent (Both parents reference the same child). This
            // state cannot be recovered from and many actions will lead to crashes, so to prevent this, we remove the
            // changes for the uobject from the undo stack, reset the properties on the UObject to the old server
            // state, then back to the new state and record the changes on the undo stack. This way when you undo the
            // changes, only the properties that changed will be undone instead of the entire prefab instance reverting
            // to its previous state. This has a side affect that properties we do not sync will not be undone by an
            // undo operation. I submitted a bug for this to Unity. If they fix it we can remove this workaround.
            if (sfUndoManager.Get().IsHandlingUndoRedo || !PrefabUtility.IsPartOfPrefabInstance(uobj))
            {
                return;
            }
            Undo.ClearUndo(uobj);
            if (!obj.IsLocked)
            {
                ApplyProperties(uobj, oldProperties);
                Undo.RecordObject(uobj, Undo.GetCurrentGroupName());
                ApplyProperties(uobj, properties);
            }
        }

        /// <summary>Destroys all components on the default object.</summary>
        private void DestroyDefaultComponents()
        {
            // Iterate components backwards to ensure dependent components are destroyed first.
            Component[] components = m_defaultObject.GetComponents<Component>();
            for (int i = components.Length - 1; i >= 0; i--)
            {
                Component component = components[i];
                if (!(component is Transform))
                {
                    UObject.DestroyImmediate(component);
                }
            }
        }

        /// <summary>Gets a float serialized property value converted to an sfProperty.</summary>
        /// <param name="sprop">sprop to convert.</param>
        /// <returns>converted property.</returns>
        private sfBaseProperty GetFloat(SerializedProperty sprop)
        {
            return sprop.type == "double" ?
                sfValueProperty.From(sprop.doubleValue) : new sfValueProperty(sprop.floatValue);
        }

        /// <summary>Sets a float serialized property to a value from an sfProperty.</summary>
        /// <param name="sprop">sprop to set.</param>
        /// <param name="prop">prop to get value from.</param>
        /// <param name="bool">bool true if the property changed.</param>
        private bool SetFloat(SerializedProperty sprop, sfBaseProperty prop)
        {
            if (sprop.type == "double")
            {
                double value = prop.As<double>();
                if (sprop.doubleValue != value)
                {
                    sprop.doubleValue = value;
                    return true;
                }
            }
            else
            {
                float value = (float)prop;
                if (sprop.floatValue != value)
                {
                    sprop.floatValue = value;
                    return true;
                }
            }
            return false;
        }

        /// <summary>Gets an int serialized property value converted to an sfProperty.</summary>
        /// <param name="sprop">sprop to convert.</param>
        /// <returns>converted property.</returns>
        private sfBaseProperty GetInteger(SerializedProperty sprop)
        {
            switch (sprop.type)
            {
                default:
                case "int":
                case "short":
                {
                    return sprop.intValue;
                }
                case "uint":
                case "ushort":
                {
                    return (uint)sprop.longValue;
                }
                case "byte":
                case "sbyte":
                {
                    return (byte)sprop.intValue;
                }
                case "long":
                case "ulong":
                {
                    return sprop.longValue;
                }
            }
        }

        /// <summary>Sets an int serialized property to a value from an sfProperty.</summary>
        /// <param name="sprop">sprop to set.</param>
        /// <param name="prop">prop to get value from.</param>
        /// <returns>true if the property changed.</returns>
        private bool SetInteger(SerializedProperty sprop, sfBaseProperty prop)
        {
            switch (sprop.type)
            {
                default:
                case "int":
                case "short":
                {
                    int value = (int)prop;
                    if (sprop.intValue != value)
                    {
                        sprop.intValue = value;
                        return true;
                    }
                    return false;
                }
                case "uint":
                case "ushort":
                {
                    uint value = (uint)prop;
                    if (sprop.longValue != value)
                    {
                        sprop.longValue = value;
                        return true;
                    }
                    return false;
                }
                case "byte":
                case "sbyte":
                {
                    byte value = (byte)prop;
                    if (sprop.intValue != value)
                    {
                        sprop.intValue = value;
                        return true;
                    }
                    return false;
                }
                case "long":
                case "ulong":
                {
                    long value = (long)prop;
                    if (sprop.longValue != value)
                    {
                        sprop.longValue = value;
                        return true;
                    }
                    return false;
                }
            }
        }

        /// <summary>Gets an object reference serialized property value converted to an sfProperty.</summary>
        /// <param name="sprop">sprop to convert.</param>
        /// <returns>converted property.</returns>
        private sfBaseProperty GetObject(SerializedProperty sprop)
        {
            if (sprop.objectReferenceValue == null && sprop.objectReferenceInstanceIDValue != 0)
            {
                ksLog.Warning(this, "Reference to missing object found on " +
                        sprop.serializedObject.targetObject.name + " (" +
                        sprop.serializedObject.targetObject.GetType().Name + "). Path: " + sprop.propertyPath +
                        ". Missing objects cannot be synced and the reference will be set to null for other users.",
                        sprop.serializedObject.targetObject);
                if (OnMissingObject != null)
                {
                    OnMissingObject(sprop.serializedObject.targetObject);
                }
            }
            return sfPropertyUtils.FromReference(sprop.objectReferenceValue, sprop);
        }

        /// <summary>Sets an object reference serialized property to a value from an sfProperty.</summary>
        /// <param name="sprop">sprop to set.</param>
        /// <param name="prop">prop to get value from.</param>
        /// <returns>true if the property changed.</returns>
        private bool SetObject(SerializedProperty sprop, sfBaseProperty prop)
        {
            UObject current = sprop.objectReferenceValue;
            UObject uobj = sfPropertyUtils.ToReference(prop, current);
            if (uobj != current)
            {
                sprop.objectReferenceValue = uobj;
                return true;
            }
            return false;
        }

        /// <summary>Gets an exposed reference serialized property value converted to an sfProperty.</summary>
        /// <param name="sprop">sprop to convert.</param>
        /// <returns>converted property.</returns>
        private sfBaseProperty GetExposedReference(SerializedProperty sprop)
        {
            return sfPropertyUtils.FromReference(sprop.exposedReferenceValue);
        }

        /// <summary>Sets an exposed reference serialized property to a value from an sfProperty.</summary>
        /// <param name="sprop">sprop to set.</param>
        /// <param name="prop">prop to get value from.</param>
        /// <returns>true if the property changed.</returns>
        private bool SetExposedReference(SerializedProperty sprop, sfBaseProperty prop)
        {
            UObject current = sprop.exposedReferenceValue;
            UObject uobj = sfPropertyUtils.ToReference(prop, current);
            if (uobj != current)
            {
                sprop.exposedReferenceValue = uobj;
                return true;
            }
            return false;
        }

        /// <summary>Sets an array of references to the given uobject.</summary>
        /// <param name="uobj">uobj to set references to.</param>
        /// <param name="references">references to set to the uobject.</param>
        /// <param name="setDirty">if false, will not set objects with changed references dirty.</param>
        public void SetReferences(UObject uobj, sfReferenceProperty[] references, bool setDirty = true)
        {
            if (!setDirty)
            {
                ApplySerializedProperties();
            }
            foreach (sfReferenceProperty reference in references)
            {
                if (sfPropertyUtils.IsCustomProperty(reference))
                {
                    // Call the object event dispatcher to run the appropriate property change handler.
                    sfObjectEventDispatcher.Get().OnPropertyChange(reference);
                    continue;
                }

                UObject referencingObject = sfObjectMap.Get().GetUObject(reference.GetContainerObject());
                if (referencingObject == null)
                {
                    continue;
                }
                // Set reference
                SerializedObject so = setDirty ? 
                    GetSerializedObject(referencingObject) : new SerializedObject(referencingObject);
                SerializedProperty sprop = so.FindProperty(GetSerializedPropertyPath(reference));
                if (sprop != null)
                {
                    if (sprop.propertyType == SerializedPropertyType.ObjectReference)
                    {
                        sprop.objectReferenceValue = uobj;
                    }
                    else if (sprop.propertyType == SerializedPropertyType.ExposedReference)
                    {
                        sprop.exposedReferenceValue = uobj;
                    }
                }
                if (!setDirty)
                {
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
            }
            // If session edits are disabled, we are still processing RPCs and we will wait to apply serialized
            // properties until that is done.
            if (setDirty && (!SceneFusion.Get().Service.IsConnected ||
                !SceneFusion.Get().Service.Session.EditsDisabled))
            {
                ApplySerializedProperties();
            }
        }

        /// <summary>Gets the serialized property for an sfBaseProperty.</summary>
        /// <param name="to">to get serialized property from.</param>
        /// <param name="prop">prop to get serialized property for.</param>
        /// <returns></returns>
        public SerializedProperty GetSerializedProperty(SerializedObject so, sfBaseProperty prop)
        {
            return so.FindProperty(GetSerializedPropertyPath(prop));
        }

        /// <summary>Gets the parent property of a serialized property.</summary>
        /// <param name="prop">prop to get parent for.</param>
        /// <returns>parent property.</returns>
        public SerializedProperty GetParentProperty(SerializedProperty prop)
        {
            string path = prop.propertyPath;
            int index = path.LastIndexOf('.');
            if (index <= 0)
            {
                return null;
            }
            if (path.Substring(index + 1).StartsWith("data["))
            {
                // Subtract ".Array"
                index -= 6;
                if (index <= 0)
                {
                    return null;
                }
            }
            return prop.serializedObject.FindProperty(path.Substring(0, index));
        }

        /// <summary>Gets the serialized property for field of a sfDictionaryProperty.</summary>
        /// <param name="to">to get serialized property from.</param>
        /// <param name="dict">dict to get serialized property for.</param>
        /// <param name="name">name of the field to get serialized property for.</param>
        /// <returns></returns>
        public SerializedProperty GetSerializedProperty(SerializedObject so, sfDictionaryProperty dict, string name)
        {
            string path;
            if (dict.ParentProperty == null)
            {
                path = name;
            }
            else
            {
                path = GetSerializedPropertyPath(dict) + "." + name;
            }
            return so.FindProperty(path);
        }

        /// <summary>
        /// Gets the serialized property path for the given sfBaseProperty.
        /// Builds the full property path that connects the given property and all its ancestor's property name.
        /// If the property is an array element, then the property name will be its index. Add "Array.data[]" around
        /// it to match with the Unity array element property path.
        /// </summary>
        /// <param name="prop"></param>
        /// <returns></returns>
        private string GetSerializedPropertyPath(sfBaseProperty prop)
        {
            if (prop == null || prop.ParentProperty == null)
            {
                return "";
            }

            string path = "";
            sfBaseProperty current = prop;
            while (true)
            {
                if (current.ParentProperty is sfDictionaryProperty)
                {
                    path = current.Name + path;
                }
                else if (current.ParentProperty is sfListProperty)
                {
                    path = "Array.data[" + current.Index + "]" + path;
                }
                else
                {
                    path = current.Name + path;
                }
                current = current.ParentProperty;
                if (current.ParentProperty != null)
                {
                    path = "." + path;
                }
                else
                {
                    break;
                }
            }
            return path;
        }

        /// <summary>Gets a generic serialized property value converted to an sfProperty.</summary>
        /// <param name="sprop">sprop to convert.</param>
        /// <returns>converted property.</returns>
        public sfBaseProperty GetGeneric(SerializedProperty sprop)
        {
            sfDictionaryProperty dict = null;
            sfListProperty list = null;
            if (sprop.isArray)
            {
                list = new sfListProperty();
            }
            else
            {
                dict = new sfDictionaryProperty();
            }
            foreach (SerializedProperty subProp in Iterate(sprop))
            {
                if (!sprop.isArray && IsDefaultValue(subProp, false))
                {
                    continue;
                }
                sfBaseProperty property = GetValue(subProp);
                if (property != null)
                {
                    if (sprop.isArray)
                    {
                        list.Add(property);
                    }
                    else
                    {
                        dict[subProp.name] = property;
                    }
                }
            }
            if (sprop.serializedObject.targetObject is Component &&
                m_defaultObject != null &&
                !(sprop.serializedObject.targetObject is Transform))
            {
                DestroyDefaultComponents();
            }
            return sprop.isArray ? (sfBaseProperty)list : (sfBaseProperty)dict;
        }

        /// <summary>Sets a generic serialized property to a value from an sfProperty.</summary>
        /// <param name="sprop">sprop to set.</param>
        /// <param name="prop">prop to get value from.</param>
        /// <returns>true if the property changed.</returns>
        public bool SetGeneric(SerializedProperty sprop, sfBaseProperty prop)
        {
            sfDictionaryProperty dict =
                prop.Type == sfBaseProperty.Types.DICTIONARY ? (sfDictionaryProperty)prop : null;
            sfListProperty list =
                prop.Type == sfBaseProperty.Types.LIST ? (sfListProperty)prop : null;
            bool changed = false;
            if (sprop.isArray && list != null && sprop.arraySize != list.Count)
            {
                sprop.arraySize = list.Count;
                changed = true;
            }
            int index = 0;
            foreach (SerializedProperty subProp in Iterate(sprop))
            {
                if (sprop.isArray && subProp.propertyType != SerializedPropertyType.ArraySize && list != null)
                {
                    if (list.Count > index)
                    {
                        changed = SetValue(subProp, list[index]) || changed;
                    }
                    else
                    {
                        changed = SetToDefaultValue(subProp, false) || changed;
                    }
                    index++;
                }
                else if (dict != null)
                {
                    sfBaseProperty property;
                    if (dict.TryGetField(subProp.name, out property))
                    {
                        changed = SetValue(subProp, property) || changed;
                    }
                    else
                    {
                        changed = SetToDefaultValue(subProp, false) || changed;
                    }
                }
            }
            if (sprop.serializedObject.targetObject is Component &&
                m_defaultObject != null &&
                !(sprop.serializedObject.targetObject is Transform))
            {
                DestroyDefaultComponents();
            }
            return changed;
        }

        /// <summary>
        /// Iterates all syncable root properties or subproperties of a property. To iterate root properties, call this
        /// on the iterator returned by serializedObject.GetIterator().
        /// </summary>
        /// <param name="property">
        /// property to iterate the syncable subproperties of. If this is the iterator
        /// returned by serializedObject.GetIterator(), it will iterate the root properties.
        /// </param>
        public IEnumerable<SerializedProperty> Iterate(SerializedProperty property)
        {
            // Get the black list and hidden sync properties for this type.
            SerializedProperty iter;
            Type type = null;
            bool iteratingRootProperties = property.depth == -1;
            if (iteratingRootProperties)
            {
                iter = property;
                // If iterating root properties, get the type from the serialized object.
                type = iter.serializedObject.targetObject.GetType();
            }
            else
            {
                iter = property.Copy();
#if UNITY_2022_1_OR_NEWER
                try
                {
                    // Try to get the C# type from the boxed value if we can.
                    object obj = iter.boxedValue;
                    if (obj != null)
                    {
                        type = obj.GetType();
                    }
                }
                catch (Exception)
                {

                }
#endif
            }
            HashSet<string> blackList;
            HashSet<string> hiddenProperties;
            if (type == null)
            {
                // We could not determine the type, so use the type name from the property.
                blackList = m_blacklist.GetProperties(iter.type);
                hiddenProperties = m_syncedHiddenProperties.GetProperties(iter.type);
            }
            else
            {
                blackList = m_blacklist.GetProperties(type);
                hiddenProperties = m_syncedHiddenProperties.GetProperties(type);
            }

            // Iterate the visible properties that aren't in the black list.
            bool enterChildren = true;
            int depth = iter.depth;
            while (iter.NextVisible(enterChildren) && iter.depth > depth)
            {
                enterChildren = false;
                if (blackList == null || !blackList.Contains(iter.name))
                {
                    if (hiddenProperties != null && hiddenProperties.Contains(iter.name))
                    {
                        string typeName = iter.depth == -1 ? iter.serializedObject.targetObject.GetType().Name : iter.type;
                        ksLog.Warning(this, "Property " + iter.name + " on " + typeName +
                            " is in the synced hidden properties set, but it was not hidden.");
                    }
                    else
                    {
                        yield return iter;
                    }
                }
            }

            SerializedObject so = iter.serializedObject;

            // Iterate the synced hidden properties. We have to do this after iterating the visible properties because
            // if we do it before, calling serializedObject.FindProperty and setting the value of the returned property
            // will invalidate our property iterator because of a Unity bug.
            if (hiddenProperties != null)
            {
                foreach (string name in hiddenProperties)
                {
                    SerializedProperty sprop = iteratingRootProperties ?
                        so.FindProperty(name) : property.FindPropertyRelative(name);
                    if (sprop != null)
                    {
                        yield return sprop;
                    }
                    else
                    {
                        string typeName = iteratingRootProperties ?
                            so.targetObject.GetType().Name : property.type;
                        ksLog.Warning(this, "Could not find hidden property " + name + " on " + typeName);
                    }
                }
            }

            // Check if the object has an enabled flag
            if (iteratingRootProperties && EditorUtility.GetObjectEnabled(so.targetObject) >= 0)
            {
                SerializedProperty sprop = so.FindProperty("m_Enabled");
                if (sprop != null)
                {
                    yield return sprop;
                }
            }
        }
    }
}
