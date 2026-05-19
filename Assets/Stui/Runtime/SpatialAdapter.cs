// Modifications Copyright (c) 2026 TerminalJack
// Licensed under the MIT License. See the LICENSE.TXT file in the project root for details.
//
// Portions of this file are derived from the Spriter2UnityDX project.
// The original author provided an open-use permission statement, preserved in THIRD_PARTY_NOTICES.md.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Stui
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class SpatialAdapter : MonoBehaviour, ITransformModifier
    {
        public Vector2 Position = Vector2.zero;
        public Vector2 Scale = Vector2.one;

        private readonly List<SpatialAdapter> _cachedSpatialAdapters = new List<SpatialAdapter>();
        private readonly List<ScaleTracker> _cachedScaleTrackers = new List<ScaleTracker>();

        private readonly List<VirtualParent> _cachedVirtualParents = new List<VirtualParent>();
        private readonly List<int> _cachedVersions = new List<int>();

        private bool _isSpritePivotOrCollider; // If false then the component is on a bone or action point.
        private SpatialController _spatialController;

        void Awake()
        {
            if (!GetComponentInParent<DependencyResolver>())
            {
                Debug.LogWarning("A SpatialAdapter component was created but a DependencyResolver component wasn't " +
                    "found.  The SpatialAdapter component will not work without a corresponding DependencyResolver.");
            }

            _isSpritePivotOrCollider =
                GetComponent<SpriteRenderer>() != null ||
                GetComponent<DynamicPivot2D>() != null ||
                GetComponent<BoxCollider2D>() != null;

            // The animation curves will use the Spatial Controller component to control whether to use Spriter
            // scaling or not.  When importing, it should be created before any SpatialAdapter components.
            _spatialController = GetComponentInParent<SpatialController>();
        }

        void OnEnable()
        {
            if (_spatialController == null)
            {   // This is a programming error and shouldn't happen in production.  (Or the user deleted it.)
                Debug.LogWarning("A SpatialController component could not be found.");
            }

            ResolveChain();
        }

        public void ApplyTransformModifier()
        {
            bool useSpriterScaling = _spatialController != null
                ? _spatialController.UseSpriterScaling
                : false;

            if (!useSpriterScaling)
            {   // The values in Position and Scale are already baked.  They just need to be assigned to the transform.
                transform.localPosition = Position;
                transform.localScale = new Vector3(Scale.x, Scale.y, 1f);

                return;
            }

            ResolveChainIfNeeded();

            Vector2 finalLocalScale = Vector2.one;

            foreach (var spatialAdapter in _cachedSpatialAdapters)
            {
                finalLocalScale *= spatialAdapter.Scale;
            }

            foreach (var scaleTracker in _cachedScaleTrackers)
            {
                finalLocalScale *= scaleTracker.RawScale;
            }

            finalLocalScale = new Vector2(Mathf.Abs(finalLocalScale.x), Mathf.Abs(finalLocalScale.y));

            transform.localPosition = Position * finalLocalScale;

            transform.localScale = _isSpritePivotOrCollider
                ? new Vector3(
                    Scale.x * finalLocalScale.x,
                    Scale.y * finalLocalScale.y,
                    1f)
                : new Vector3(
                    Scale.x > 0f ? 1f : -1f,
                    Scale.y > 0f ? 1f : -1f,
                    1f);
        }

        private void ResolveChainIfNeeded()
        {
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

                if (vp != null && vp.Version != _cachedVersions[i])
                {
                    ResolveChain();

                    return;
                }
            }
        }

        private void ResolveChain()
        {
            _cachedSpatialAdapters.Clear();
            _cachedScaleTrackers.Clear();
            _cachedVirtualParents.Clear();
            _cachedVersions.Clear();

            Transform t = transform.parent;

            int depth = 0; // Used to guard against cycles.

            while (t != null && ++depth < 100)
            {
                if (t.TryGetComponent(out SpatialAdapter s))
                {
                    _cachedSpatialAdapters.Add(s);
                }

                if (t.TryGetComponent(out ScaleTracker tracker))
                {
                    _cachedScaleTrackers.Add(tracker);
                }

                // Cache VirtualParent and its version, if present.
                if (t.TryGetComponent(out VirtualParent vp))
                {
                    _cachedVirtualParents.Add(vp);
                    _cachedVersions.Add(vp.Version);

                    // Follow virtual parent redirection.  Note that this component will run during an import and, in
                    // that case, the virtual parent's possibleParents list can be empty for a short time.  The
                    // following call will return the real parent in that case, which is fine for importing.

                    t = vp.GetVirtualParentTransform();
                }
                else
                {
                    t = t.parent;
                }
            }
        }
    }
}