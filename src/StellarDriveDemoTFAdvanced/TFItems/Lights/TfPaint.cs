using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace StellarDriveDemoTFAdvanced.TFItems.Lights
{
    /// <summary>
    /// A lamp painted with the TF mod's paint gun takes that color: its paint data is read from the
    /// TF mod, by name, when it is installed (PaintNet.Client.TryGet(PartKey, out PaintData)).
    /// </summary>
    internal static class TfPaint
    {
        private static bool _resolved;
        private static object _client;
        private static MethodInfo _tryGet;
        private static ConstructorInfo _key;
        private static FieldInfo _color;
        private static PropertyInfo _colorProperty;

        public static bool TryGet(uint shipId, ushort partId, out Color color)
        {
            color = default;
            if (TFItemsMod.ToolsActive)
            {
                // This mod's own paint (the TF mod is not installed)
                if (!Paint.PaintNet.Client.TryGet(new Common.PartKey(shipId, partId), out Paint.PaintData paint))
                    return false;
                color = paint.Color;
                return true;
            }
            if (!_resolved)
                Resolve();
            if (_tryGet == null)
                return false;
            try
            {
                object[] args = { _key.Invoke(new object[] { shipId, partId }), null };
                if (!(bool)_tryGet.Invoke(_client, args) || args[1] == null)
                    return false;
                object value = _color != null ? _color.GetValue(args[1]) : _colorProperty?.GetValue(args[1], null);
                if (value is Color c)
                    color = c;
                else if (value is Color32 c32)
                    color = c32;
                else
                    return false;
                return true;
            }
            catch (Exception)
            {
                _tryGet = null;
                return false;
            }
        }

        private static void Resolve()
        {
            _resolved = true;
            try
            {
                Type net = AccessTools.TypeByName("StellarDriveDemoTF.Paint.PaintNet");
                Type keyType = AccessTools.TypeByName("StellarDriveDemoTF.Paint.PartKey") ?? AccessTools.TypeByName("StellarDriveDemoTF.Common.PartKey");
                Type data = AccessTools.TypeByName("StellarDriveDemoTF.Paint.PaintData");
                if (net == null || keyType == null || data == null)
                    return;
                FieldInfo clientField = AccessTools.Field(net, "Client");
                PropertyInfo clientProperty = clientField == null ? AccessTools.Property(net, "Client") : null;
                _client = clientField != null ? clientField.GetValue(null) : clientProperty?.GetValue(null, null);
                if (_client == null)
                    return;
                _tryGet = AccessTools.Method(_client.GetType(), "TryGet", new[] { keyType, data.MakeByRefType() });
                _key = AccessTools.Constructor(keyType, new[] { typeof(uint), typeof(ushort) });
                _color = AccessTools.Field(data, "Color");
                _colorProperty = _color == null ? AccessTools.Property(data, "Color") : null;
                if (_key == null)
                    _tryGet = null;
            }
            catch (Exception)
            {
                _tryGet = null;
            }
        }
    }
}
