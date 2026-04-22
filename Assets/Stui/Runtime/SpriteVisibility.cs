// Modifications Copyright (c) 2026 TerminalJack
// Licensed under the MIT License. See the LICENSE.TXT file in the project root for details.
//
// Portions of this file are derived from the Spriter2UnityDX project.
// The original author provided an open-use permission statement, preserved in THIRD_PARTY_NOTICES.md.

using UnityEngine;

// This script exists to provide compatibility between a wide variety of Unity versions.

namespace Stui
{
    [ExecuteAlways]
    public class SpriteVisibility : MonoBehaviour
    {
        public float isVisible = 0f; // Bools aren't supported in the animator on newer versions of Unity so we get this hack.

        private SpriteRenderer _spriteRenderer;

        void OnEnable()
        {
            _spriteRenderer = GetComponent<SpriteRenderer>();
            ApplyVisibility();
        }

        void OnDidApplyAnimationProperties() => ApplyVisibility();
        void Update() { if (!Application.isPlaying) ApplyVisibility(); }
        void LateUpdate() { if (Application.isPlaying) ApplyVisibility(); }

        private void ApplyVisibility()
        {
            if (_spriteRenderer != null)
            {
                isVisible = Mathf.RoundToInt(isVisible);
                _spriteRenderer.enabled = isVisible != 0;
            }
        }
    }
}