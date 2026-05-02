// Modifications Copyright (c) 2026 TerminalJack
// Licensed under the MIT License. See the LICENSE.TXT file in the project root for details.
//
// Portions of this file are derived from the Spriter2UnityDX project.
// The original author provided an open-use permission statement, preserved in THIRD_PARTY_NOTICES.md.

using System.Collections.Generic;
using UnityEngine;

namespace Stui
{
    [ExecuteAlways]
    [DefaultExecutionOrder(300)]
    [DisallowMultipleComponent]
    public class VirtualParent : MonoBehaviour
    {
        [Tooltip("List of Transforms whose local coordinate space this component will adopt when selected as its virtual parent.")]
        public List<Transform> possibleParents = new List<Transform>();

        [Tooltip("Selects which index from the Possible Parents list is currently active as the virtual parent.  " +
            "The importer reserves index 0 as the component's actual parent but you don't have to follow this norm.  " +
            "An invalid index will use the component's actual parent.")]
        public int parentIndex = -1;

        [HideInInspector] public int version => _version;

        private int _version = 1;
        private int _lastParentIndex = -1;

        void OnEnable() => ApplyVirtualParent();
        void OnValidate() => ApplyVirtualParent();
        void OnDidApplyAnimationProperties() => ApplyVirtualParent();

#if UNITY_EDITOR
        void Update() { if (!Application.isPlaying) ApplyVirtualParent(); }
#endif

        void LateUpdate()
        {
#if UNITY_EDITOR
            if (Application.isPlaying)
#endif
            {
                ApplyVirtualParent();
            }
        }

        void ApplyVirtualParent()
        {
            if (parentIndex != _lastParentIndex)
            {
                _version++;
                _lastParentIndex = parentIndex;
            }

            if (parentIndex < 0 ||
                parentIndex >= possibleParents.Count ||
                possibleParents[parentIndex] == null ||
                possibleParents[parentIndex] == transform.parent)
            {
                // Either the parentIndex is invalid, its transform is null, or its transform is this transform's
                // actual parent.
                transform.localPosition = Vector3.zero;
                transform.localRotation = Quaternion.identity;
                transform.localScale = Vector3.one;

                return;
            }

            // For any other index, do manual "parent constraint" in 2D...

            var src = possibleParents[parentIndex];
            var virtualParent = transform;
            var realParent = transform.parent;

            if (src == null || realParent == null)
            {
                return;
            }

            Reframe2D.AdoptSpace2D(virtualParent, src);
        }

        private static class Reframe2D
        {
            public static void AdoptSpace2D(Transform parent, Transform space)
            {
                if (parent == null || space == null)
                {
                    return;
                }

                // world-to-local of grandparent
                var gp = parent.parent;
                var Mginv = gp ? gp.worldToLocalMatrix : Matrix4x4.identity;
                var Ms = space.localToWorldMatrix;

                // compute local TRS
                var Mlocal = Mginv * Ms;
                DecomposeLocal2D(
                    in Mlocal,
                    parent.localPosition.z,
                    parent.localScale.z,
                    out var lp,
                    out var rotZ,
                    out var ls);

                parent.localPosition = lp;
                parent.localRotation = Quaternion.AngleAxis(rotZ, Vector3.forward);
                parent.localScale = ls;
            }

            static void DecomposeLocal2D(
                in Matrix4x4 m,
                float currentZPos,
                float currentZScale,
                out Vector3 localPos,
                out float rotZDeg,
                out Vector3 localScale)
            {
                // XY position + preserved Z
                localPos = new Vector3(m.m03, m.m13, currentZPos);

                // XY basis
                Vector2 X = new Vector2(m.m00, m.m10);
                Vector2 Y = new Vector2(m.m01, m.m11);

                float sx = X.magnitude;
                float sy = Y.magnitude;
                // handle degenerate
                if (sx <= 1e-12f)
                {
                    sx = 0f;
                }

                if (sy <= 1e-12f)
                {
                    sy = 0f;
                }

                // handedness
                bool mirrored = (X.x * Y.y - X.y * Y.x) < 0f;
                // normalize for rotation
                Vector2 Xn = (sx > 0f) ? (X / sx) : Vector2.right;

                // mirror on Y axis only
                if (mirrored)
                {
                    sy = -sy;
                }

                // rotation from Xn
                rotZDeg = Mathf.Atan2(Xn.y, Xn.x) * Mathf.Rad2Deg;
                localScale = new Vector3(sx, sy, currentZScale);
            }
        }
    }
}