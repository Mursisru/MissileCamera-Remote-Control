using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace MissileCameraRemoteControl.Access
{
    /// <summary>Rewired keyboard + player via reflection — no Rewired.dll reference.</summary>
    internal static class RcRewiredInput
    {
        private static bool _resolved;
        private static PropertyInfo? _playerProp;
        private static FieldInfo? _playerField;
        private static PropertyInfo? _controllersProp;
        private static PropertyInfo? _keyboardProp;
        private static MethodInfo? _keyboardGetKeyMethod;
        private static MethodInfo? _keyboardGetKeyDownMethod;
        private static readonly Dictionary<KeyCode, bool> _heldPrev = new Dictionary<KeyCode, bool>(16);

        internal static bool IsKeyHeld(KeyCode key)
        {
            if (key == KeyCode.None)
                return false;

            Ensure();
            object? keyboard = TryGetKeyboard();
            if (keyboard == null || _keyboardGetKeyMethod == null)
                return false;

            try
            {
                object? raw = _keyboardGetKeyMethod.Invoke(keyboard, new object[] { key });
                return raw is bool b && b;
            }
            catch
            {
                return false;
            }
        }

        internal static bool IsKeyDown(KeyCode key)
        {
            if (key == KeyCode.None)
                return false;

            Ensure();
            object? keyboard = TryGetKeyboard();
            if (keyboard != null && _keyboardGetKeyDownMethod != null)
            {
                try
                {
                    object? raw = _keyboardGetKeyDownMethod.Invoke(keyboard, new object[] { key });
                    if (raw is bool b && b)
                        return true;
                }
                catch
                {
                    // fall through to edge detect
                }
            }

            bool held = IsKeyHeld(key);
            _heldPrev.TryGetValue(key, out bool prev);
            _heldPrev[key] = held;
            return held && !prev;
        }

        internal static void EndFrame()
        {
            if (_heldPrev.Count == 0)
                return;

            foreach (KeyCode key in new List<KeyCode>(_heldPrev.Keys))
                _heldPrev[key] = IsKeyHeld(key);
        }

        internal static bool KeyHeld(KeyCode key) =>
            Input.GetKey(key) || IsKeyHeld(key);

        internal static bool KeyDown(KeyCode key) =>
            Input.GetKeyDown(key) || IsKeyDown(key);

        private static void Ensure()
        {
            if (_resolved)
                return;
            _resolved = true;

            try
            {
                Type gm = typeof(GameManager);
                _playerProp = gm.GetProperty(
                    "playerInput",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                _playerField = gm.GetField(
                    "playerInput",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

                Type? playerType = _playerProp?.PropertyType ?? _playerField?.FieldType;
                if (playerType == null)
                    return;

                _controllersProp = playerType.GetProperty(
                    "controllers",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                if (_controllersProp == null)
                    return;

                Type? controllersType = _controllersProp.PropertyType;
                _keyboardProp = controllersType?.GetProperty(
                    "Keyboard",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                Type? keyboardType = _keyboardProp?.PropertyType;
                if (keyboardType == null)
                    return;

                _keyboardGetKeyMethod = keyboardType.GetMethod(
                    "GetKey",
                    BindingFlags.Instance | BindingFlags.Public,
                    null,
                    new[] { typeof(KeyCode) },
                    null);
                _keyboardGetKeyDownMethod = keyboardType.GetMethod(
                    "GetKeyDown",
                    BindingFlags.Instance | BindingFlags.Public,
                    null,
                    new[] { typeof(KeyCode) },
                    null);
            }
            catch
            {
                _keyboardGetKeyMethod = null;
                _keyboardGetKeyDownMethod = null;
            }
        }

        private static object? TryGetPlayer()
        {
            try
            {
                if (_playerProp != null)
                    return _playerProp.GetValue(null);
                return _playerField?.GetValue(null);
            }
            catch
            {
                return null;
            }
        }

        private static object? TryGetKeyboard()
        {
            object? player = TryGetPlayer();
            if (player == null || _controllersProp == null || _keyboardProp == null)
                return null;

            try
            {
                object? controllers = _controllersProp.GetValue(player);
                return controllers == null ? null : _keyboardProp.GetValue(controllers);
            }
            catch
            {
                return null;
            }
        }
    }
}
