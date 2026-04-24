// Modifications Copyright (c) 2026 TerminalJack
// Licensed under the MIT License. See the LICENSE.TXT file in the project root for details.
//
// Portions of this file are derived from the Spriter2UnityDX project.
// The original author provided an open-use permission statement, preserved in THIRD_PARTY_NOTICES.md.

using UnityEngine;

namespace Stui
{
    [DisallowMultipleComponent]
    public class SpriterTag : MonoBehaviour
    {
        [Tooltip("The name of this tag.")]
        public string tagName;

        [Tooltip("Is this tag currently active?  This is the animated property.  Use the isActive property to get the " +
            "tag's state.")]
        public float isActiveFloat = 0f;

        public bool isActive => isActiveFloat > 0.5f;
    }
}
