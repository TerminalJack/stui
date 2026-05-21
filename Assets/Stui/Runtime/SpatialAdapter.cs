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

        [NonSerialized] public Vector2 runningScale = Vector2.one; // The scale of this component * its ancestors' scales.

        private SpatialAdapter _parentSpatialAdapter = null; // If non-null then found on direct parent.
        private SpatialAdapter _virtualParentSpatialAdapter = null; // If non-null then found thru virtual parent.

        private readonly List<ScaleTracker> _cachedScaleTrackers = new List<ScaleTracker>();

        private readonly List<VirtualParent> _cachedVirtualParents = new List<VirtualParent>();
        private readonly List<int> _cachedVpVersions = new List<int>();

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

            if (_spatialController == null)
            {   // This is a programming error and shouldn't happen in production.  (Or the user deleted it.)
                Debug.LogWarning("A SpatialController component could not be found.");
            }
        }

        void OnEnable()
        {
            ResolveAncestorChain();
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

            Vector2 ancestorScale = GetAncestorScale();

            runningScale = Scale * ancestorScale; // Save this for any children.

            Vector2 finalLocalScale = new Vector2(Mathf.Abs(ancestorScale.x), Mathf.Abs(ancestorScale.y));

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

        private Vector2 GetAncestorScale()
        {
            ResolveAncestorChainIfNeeded();

            if (_parentSpatialAdapter != null)
            {
                return _parentSpatialAdapter.runningScale;
            }
            else if (_virtualParentSpatialAdapter != null)
            {
                return _virtualParentSpatialAdapter.runningScale;
            }
            else if (_cachedScaleTrackers.Count > 0)
            {
                Vector2 resultScale = Vector2.one;

                foreach (var st in _cachedScaleTrackers)
                {
                    resultScale *= st.RawScale;
                }

                return resultScale;
            }
            else
            {
                return Vector2.one;
            }
        }

        private void ResolveAncestorChainIfNeeded() // Don't call this without calling ResolveAncestorChain() at least once.
        {
            if (_parentSpatialAdapter != null)
            {
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

                if (vp != null && vp.Version != _cachedVpVersions[i])
                {
                    ResolveAncestorChain();

                    return;
                }
            }
        }

        private void ResolveAncestorChain()
        {
            _parentSpatialAdapter = null;
            _virtualParentSpatialAdapter = null;

            _cachedScaleTrackers.Clear();
            _cachedVirtualParents.Clear();
            _cachedVpVersions.Clear();

            Transform t = transform.parent;

            if (t.TryGetComponent(out _parentSpatialAdapter))
            {
                return;
            }

            int depth = 0; // Used to guard against cycles.

            while (t != null && depth < 100)
            {
                if (t.TryGetComponent(out ScaleTracker tracker))
                {
                    _cachedScaleTrackers.Add(tracker);
                    t = t.parent;
                }
                else if (t.TryGetComponent(out VirtualParent vp)) // Cache VirtualParent and its version, if present.
                {
                    _cachedVirtualParents.Add(vp);
                    _cachedVpVersions.Add(vp.Version);

                    // Follow virtual parent redirection.  Note that this component will run during an import and, in
                    // that case, the virtual parent's possibleParents list can be empty for a short time.  The
                    // following call will return the real parent in that case, which is fine for importing.

                    t = vp.GetVirtualParentTransform();
                }
                else if (t.TryGetComponent(out _virtualParentSpatialAdapter))
                {
                    if (depth != 1)
                    {
                        Debug.LogWarning("SpatialAdapter.GetAncestorScale(): An invalid ancestor chain was encountered.");
                    }

                    return;
                }
                else
                {
                    return;
                }

                ++depth;
            }
        }
    }
}