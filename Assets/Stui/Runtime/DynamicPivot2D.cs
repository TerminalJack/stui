// Modifications Copyright (c) 2026 TerminalJack
// Licensed under the MIT License. See the LICENSE.TXT file in the project root for details.
//
// Portions of this file are derived from the Spriter2UnityDX project.
// The original author provided an open-use permission statement, preserved in THIRD_PARTY_NOTICES.md.

using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Spriter2UnityDX
{
    /// <summary>
    /// Attach this to a parent GameObject.
    /// It repositions its child SpriteRenderer so that the sprite
    /// pivots around a normalized pivot point (0,0)=bottom-left,
    /// (1,1)=top-right. Values outside [0..1] are allowed.
    /// Takes the child’s localScale into account.
    /// </summary>
    [ExecuteAlways]
    public class DynamicPivot2D : MonoBehaviour
    {
        [Tooltip("Pivot in normalized coords, (0,0)=bottom-left, (1,1)=top-right. Values outside [0..1] allowed.")]
        public Vector2 pivot = new Vector2(0.5f, 0.5f);

        SpriteRenderer _spriteRenderer;
        Transform _spriteTransform;

        void OnEnable()
        {
            GatherReferences();
            ApplyPivot();
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            GatherReferences();
            ApplyPivot();
        }
#endif

        void OnDidApplyAnimationProperties() => ApplyPivot();
        void Update() { if (!Application.isPlaying) ApplyPivot(); }
        void LateUpdate() { if (Application.isPlaying) ApplyPivot(); }

        void GatherReferences()
        {
            if (_spriteRenderer == null)
            {
                _spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            }

            if (_spriteRenderer != null)
            {
                _spriteTransform = _spriteRenderer.transform;
            }
        }

        void ApplyPivot()
        {
            if (_spriteTransform == null || _spriteRenderer?.sprite == null)
            {
                return;
            }

            var sprite = _spriteRenderer.sprite;

            Vector2 pxPivot = sprite.pivot;      // imported pivot in pixels (BL origin)
            Vector2 pxSize = sprite.rect.size;   // sprite size in pixels
            Vector2 importBL = new Vector2(      // normalized import pivot (BL origin)
                pxPivot.x / pxSize.x,
                pxPivot.y / pxSize.y
            );

            Vector2 normDiff = importBL - pivot; // how much to shift in normalized space

            if (Mathf.Approximately(normDiff.x, 0f) && Mathf.Approximately(normDiff.y, 0f))
            {   // Just use the sprite's import pivot.
                _spriteTransform.localPosition = new Vector3(0f, 0f, _spriteTransform.localPosition.z);
            }
            else
            {
                // sprite size in world units before scale
                Vector2 worldSize = new Vector2(pxSize.x / sprite.pixelsPerUnit, pxSize.y / sprite.pixelsPerUnit);

                // account for the child’s localScale
                Vector2 scaledSize = Vector2.Scale(
                    worldSize,
                    new Vector2(_spriteTransform.localScale.x, _spriteTransform.localScale.y)
                );

                // final offset in world units
                Vector2 worldOffset = Vector2.Scale(normDiff, scaledSize);

                // apply to child’s localPosition, keep its Z
                _spriteTransform.localPosition = new Vector3(
                    worldOffset.x,
                    worldOffset.y,
                    _spriteTransform.localPosition.z
                );
            }
        }
    }
}