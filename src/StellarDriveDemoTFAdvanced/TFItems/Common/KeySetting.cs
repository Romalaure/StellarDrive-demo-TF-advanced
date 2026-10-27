using System;
using MelonLoader;
using UnityEngine.InputSystem;

namespace StellarDriveDemoTFAdvanced.TFItems.Common
{
    /// <summary>A key read from a setting holding a UnityEngine.InputSystem.Key name, parsed once per change.</summary>
    internal sealed class KeySetting
    {
        private readonly Func<MelonPreferences_Entry<string>> _entry;
        private string _name;
        private Key _key = Key.None;

        public KeySetting(Func<MelonPreferences_Entry<string>> entry)
        {
            _entry = entry;
        }

        public Key Key
        {
            get
            {
                MelonPreferences_Entry<string> entry = _entry();
                string name = entry?.Value;
                if (name == _name)
                    return _key;
                _name = name;
                _key = Key.None;
                if (!string.IsNullOrWhiteSpace(name) && !Enum.TryParse(name.Trim(), true, out _key))
                {
                    TFMod.Log.Warning($"unknown key '{name}' for {entry.Identifier}");
                    _key = Key.None;
                }
                return _key;
            }
        }

        public bool WasPressed
        {
            get
            {
                Key key = Key;
                Keyboard keyboard = Keyboard.current;
                return key != Key.None && keyboard != null && keyboard[key].wasPressedThisFrame;
            }
        }

        public bool IsHeld
        {
            get
            {
                Key key = Key;
                Keyboard keyboard = Keyboard.current;
                return key != Key.None && keyboard != null && keyboard[key].isPressed;
            }
        }

        public string Label => Key == Key.None ? "?" : Key.ToString().ToUpperInvariant();
    }
}
