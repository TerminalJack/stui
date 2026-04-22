// Modifications Copyright (c) 2026 TerminalJack
// Licensed under the MIT License. See the LICENSE.TXT file in the project root for details.
//
// Portions of this file are derived from the Spriter2UnityDX project.
// The original author provided an open-use permission statement, preserved in THIRD_PARTY_NOTICES.md.

using UnityEngine;

namespace Stui
{
    [DisallowMultipleComponent]
    public class SpriterFloat : MonoBehaviour
    {
        [Tooltip("The name of this float variable.")]
        public string variableName;

        [Tooltip("The float variable's default value.")]
        public float defaultValue = -1.0f;

        [Tooltip("The float variable's current value.  This is the animated property.")]
        public float value = -1.0f;
    }
}