// Modifications Copyright (c) 2026 TerminalJack
// Licensed under the MIT License. See the LICENSE.TXT file in the project root for details.
//
// Portions of this file are derived from the Spriter2UnityDX project.
// The original author provided an open-use permission statement, preserved in THIRD_PARTY_NOTICES.md.

using UnityEngine;

namespace Stui
{
    [DisallowMultipleComponent]
    public class SpriterInt : MonoBehaviour
    {
        [Tooltip("The name of this int variable.")]
        public string variableName;

        [Tooltip("The int variable's default value.")]
        public int defaultValue = -1;

        [Tooltip("The variable's current value as a float.  This is the animated property.  Use the 'value' property " +
            "to get the variable's current value as an int.")]
        public float valueAsFloat = -1f;

        public int value { get { return (int)valueAsFloat; } }
     }
}
