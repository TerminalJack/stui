// Modifications Copyright (c) 2026 TerminalJack
// Licensed under the MIT License. See the LICENSE.TXT file in the project root for details.
//
// Portions of this file are derived from the Spriter2UnityDX project.
// The original author provided an open-use permission statement, preserved in THIRD_PARTY_NOTICES.md.

using System.Collections.Generic;
using UnityEngine;

namespace Stui.Extensions
{
    public static class CustomExtensions
    {
        /// <summary>
        /// Gets the component of type T from the Transform's GameObject, or adds it if missing.
        /// </summary>
        /// <typeparam name="T">Type of the component</typeparam>
        /// <param name="transform">Target Transform</param>
        /// <returns>Existing or newly added component of type T</returns>
        public static T GetOrAddComponent<T>(this Transform transform) where T : Component
        {
            T component = transform.GetComponent<T>();
            if (component == null)
            {
                component = transform.gameObject.AddComponent<T>();
            }
            return component;
        }

        /// <summary>
        /// Gets the component of type T if it exists, otherwise adds it and returns the new instance.
        /// </summary>
        /// <typeparam name="T">Type of the component</typeparam>
        /// <param name="gameObject">Target GameObject</param>
        /// <returns>Existing or newly added component of type T</returns>
        public static T GetOrAddComponent<T>(this GameObject gameObject) where T : Component
        {
            T component = gameObject.GetComponent<T>();
            if (component == null)
            {
                component = gameObject.AddComponent<T>();
            }
            return component;
        }

        // GetOrDefault() is a version of GetValueOrDefault() that is compatible with Unity
        // versions 2019 and 2020 (and later.)

        public static TValue GetOrDefault<TKey, TValue>(this IDictionary<TKey, TValue> dict, TKey key)
        {
            if (dict == null)
            {
                throw new System.ArgumentNullException(nameof(dict));
            }

            return dict.TryGetValue(key, out var value) ? value : default;
        }

        public static TValue GetOrDefault<TKey, TValue>(this IDictionary<TKey, TValue> dict, TKey key, TValue defaultValue)
        {
            if (dict == null)
            {
                throw new System.ArgumentNullException(nameof(dict));
            }

            return dict.TryGetValue(key, out var value) ? value : defaultValue;
        }
    }
}