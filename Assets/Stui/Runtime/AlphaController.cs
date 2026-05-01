// Modifications Copyright (c) 2026 TerminalJack
// Licensed under the MIT License. See the LICENSE.TXT file in the project root for details.
//
// Portions of this file are derived from the Spriter2UnityDX project.
// The original author provided an open-use permission statement, preserved in THIRD_PARTY_NOTICES.md.

using System.Collections.Generic;
using UnityEngine;

namespace Stui
{
    // This component is only used when a bone has an alpha value other than 1 in any of its timelines.  In this case,
    // an Alpha Controller component will be added to each of the bones that use alpha as well as any Sprite Renderer
    // game objects that are children of the bones, with virtual parents being taken into consideration.

    [ExecuteAlways]
    public class AlphaController : MonoBehaviour
    {
        [Tooltip("Controls the transparency of this bone or sprite renderer.")]
        [Range(0f, 1f)]
        public float Alpha = 1f;

        private SpriteRenderer _spriteRenderer;

        private readonly List<AlphaController> _cachedAlphaControllers = new List<AlphaController>();
        private readonly List<VirtualParent> _cachedVirtualParents = new List<VirtualParent>();
        private readonly List<int> _cachedVersions = new List<int>();

        private bool _needInitialResolution = true;

        void OnEnable()
        {
            // If _spriteRenderer is null then the component is on a bone.  In which case the component only needs to
            // track the alpha value and does't need to do anything else.
            TryGetComponent(out _spriteRenderer);

            _needInitialResolution = true;

            ApplyAlpha();
        }

        void OnDidApplyAnimationProperties() => ApplyAlpha();
        void Update() { if (!Application.isPlaying) ApplyAlpha(); }
        void LateUpdate() { if (Application.isPlaying) ApplyAlpha(); }

        private void ApplyAlpha()
        {
            if (_spriteRenderer == null)
            {
                return;
            }

            ResolveChainIfNeeded();

            float finalAlpha = Alpha;

            // Multiply all parent AlphaControllers...
            foreach (var _alphaController in _cachedAlphaControllers)
            {
                finalAlpha *= _alphaController.Alpha;
            }

            var color = _spriteRenderer.color;
            color.a = finalAlpha;
            _spriteRenderer.color = color;
        }

        private void ResolveChainIfNeeded()
        {
            if (_needInitialResolution)
            {
                ResolveChain();
                _needInitialResolution = false;

                return;
            }

            // If there are no VirtualParents in the chain then the chain will always be valid and will need to be
            // resolved only once.
            if (_cachedVirtualParents.Count == 0)
            {
                return;
            }

            // Otherwise, check the version numbers of each of the virtual parents and rebuild the chain if any of them
            // have changed.
            for (int i = 0; i < _cachedVirtualParents.Count; i++)
            {
                var vp = _cachedVirtualParents[i];

                if (vp != null && vp.version != _cachedVersions[i])
                {
                    ResolveChain();

                    return;
                }
            }
        }

        private void ResolveChain()
        {
            _cachedAlphaControllers.Clear();
            _cachedVirtualParents.Clear();
            _cachedVersions.Clear();

            Transform t = transform.parent;

            int depth = 0; // Used to guard against cycles.

            while (t != null && ++depth < 100)
            {
                // Cache AlphaController, if present.
                if (t.TryGetComponent(out AlphaController ac))
                {
                    _cachedAlphaControllers.Add(ac);
                }

                // Cache VirtualParent and its version, if present.
                if (t.TryGetComponent(out VirtualParent vp))
                {
                    _cachedVirtualParents.Add(vp);
                    _cachedVersions.Add(vp.version);

                    // Follow virtual parent redirection.  Note that this component will run during an import and, in
                    // that case, the possibleParents list can be empty for a short time.  We guard against that here.
                    // We rely on the 'version' changing once the list is updated.

                    t = vp.possibleParents.Count > 0
                        ? vp.possibleParents[vp.parentIndex]
                        : t.parent;
                }
                else
                {
                    t = t.parent;
                }
            }
        }
    }
}