using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace StellarDriveDemoTFAdvanced
{
    /// <summary>
    /// Reading Arsenal's objects by reflection, cached: its bullets hit many times a second and a
    /// field lookup by name each time cost more than the hit itself. Tolerant of Arsenal versions:
    /// a member that is not there (renamed field or property) reads as 0 instead of throwing.
    /// </summary>
    internal static class ArsenalReflection
    {
        private static readonly Dictionary<Type, Func<object, uint>> ShipIdReaders = new Dictionary<Type, Func<object, uint>>();

        /// <summary>The ShipId of an Arsenal key (PanelKey and the like: field or property).</summary>
        public static uint ShipIdOf(object key)
        {
            if (key == null)
                return 0;
            Type type = key.GetType();
            if (!ShipIdReaders.TryGetValue(type, out Func<object, uint> read))
            {
                FieldInfo field = AccessTools.Field(type, "ShipId");
                PropertyInfo property = field == null ? AccessTools.Property(type, "ShipId") : null;
                if (field != null)
                    read = o => Convert.ToUInt32(field.GetValue(o));
                else if (property != null)
                    read = o => Convert.ToUInt32(property.GetValue(o, null));
                else
                {
                    read = o => 0u;
                    AdvancedMod.Log.Warning($"Arsenal: {type.FullName} has no ShipId, its hits are not counted");
                }
                ShipIdReaders[type] = read;
            }
            return read(key);
        }

        /// <summary>A float argument, whatever numeric type this Arsenal version passes.</summary>
        public static float AsFloat(object value)
        {
            switch (value)
            {
                case float f: return f;
                case double d: return (float)d;
                case int i: return i;
                default: return 0f;
            }
        }
    }
}
