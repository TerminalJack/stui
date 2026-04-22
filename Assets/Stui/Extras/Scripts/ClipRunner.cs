// Modifications Copyright (c) 2026 TerminalJack
// Licensed under the MIT License. See the LICENSE.TXT file in the project root for details.
//
// Portions of this file are derived from the Spriter2UnityDX project.
// The original author provided an open-use permission statement, preserved in THIRD_PARTY_NOTICES.md.

using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Stui.Extras
{
    [RequireComponent(typeof(Animator))]
    public class ClipRunner : MonoBehaviour
    {
        public enum ClipPlayOrder
        {
            OrderByName,
            RandomOrder
        }

        [Tooltip("The number of seconds to play each clip.")]
        public float timePerClip = 5f;

        [Tooltip("If enabled, the animations will blend when transitioning between each other.  This may " +
            "cause visual anomalies in some cases.")]
        public bool crossFade = false;

        [Tooltip("The order in which to play the clips.")]
        public ClipPlayOrder playOrder = ClipPlayOrder.RandomOrder;

        [Tooltip("Drag this around in-scene to move the label.")]
        public Transform labelAnchor;

        // Vertical pixel offset above/below the bottom of the prefab for initial placement of the label.
        // A value of -30 means 30px down.  After creation, move the Label Anchor transform to make adjustments.
        private float _defaultLabelYOffset = -30f;

        private List<AnimationClip> _clips = new List<AnimationClip>();
        private int _clipIndex;

        private Animator _animator;
        private Camera _mainCam;

        void Reset()
        {
            EnsureLabelAnchorIsCreated();
        }

        void OnEnable()
        {
            _mainCam = Camera.main;
            EnsureLabelAnchorIsCreated();
        }

        void Awake()
        {
            _animator = GetComponent<Animator>();
            _mainCam = Camera.main;

            var rtController = _animator.runtimeAnimatorController;
            if (rtController != null)
            {
                _clips = rtController.animationClips
                    .GroupBy(c => c.name)
                    .Select(g => g.First())
                    .OrderBy(c => c.name)
                    .ToList();

                if (playOrder == ClipPlayOrder.RandomOrder)
                {
                    int n = _clips.Count;

                    while (n > 1)
                    {
                        n--;
                        int k = Random.Range(0, n + 1);

                        AnimationClip temp = _clips[k];
                        _clips[k] = _clips[n];
                        _clips[n] = temp;
                    }
                }
            }
        }

        IEnumerator Start()
        {
            if (_clips.Count == 0)
            {
                yield break;
            }

            while (true)
            {
                _clipIndex = Mathf.Clamp(_clipIndex, 0, _clips.Count - 1);

                if (crossFade)
                {
                    _animator.CrossFade(_clips[_clipIndex].name, 0.3f, 0, 0f);
                }
                else
                {
                    _animator.Play(_clips[_clipIndex].name, 0, 0f);
                }

                _animator.Update(0f);

                yield return new WaitForSeconds(timePerClip);

                _clipIndex = (_clipIndex + 1) % _clips.Count;
            }
        }

        private void EnsureLabelAnchorIsCreated()
        {
            if (labelAnchor != null)
            {
                return;
            }

            if (_mainCam == null)
            {
                _mainCam = Camera.main;
            }

            var go = transform.Find("LabelAnchor")?.gameObject ?? new GameObject("LabelAnchor");
            go.transform.SetParent(transform, false);

            // World point of the bottom of the prefab
            float bottomLocalY = GetBottomMostLocalY();
            Vector3 bottomWorld = transform.TransformPoint(0f, bottomLocalY, 0f);

            // To screen-space, add pixel offset
            Vector3 screenPt = _mainCam.WorldToScreenPoint(bottomWorld);
            screenPt += new Vector3(0f, _defaultLabelYOffset, 0f);

            // Back to world
            Vector3 adjustedWorld = _mainCam.ScreenToWorldPoint(screenPt);

            // Convert that to local
            Vector3 local = transform.InverseTransformPoint(adjustedWorld);

            go.transform.localPosition = new Vector3(0f, local.y, 0f);

            labelAnchor = go.transform;
        }

        private float GetBottomMostLocalY()
        {
            float minWorldY = float.PositiveInfinity;
            Transform root = transform;

            // Iterate all active, enabled SpriteRenderers in children
            foreach (var sr in GetComponentsInChildren<SpriteRenderer>(includeInactive: false))
            {
                if (!sr.enabled || sr.sprite == null)
                {
                    continue;
                }

                // Grab the 2D vertex array from the sprite
                Vector2[] verts2D = sr.sprite.vertices;
                Matrix4x4 localToWorld = sr.transform.localToWorldMatrix;

                // Transform each vertex to world space and track the minimum Y
                for (int i = 0; i < verts2D.Length; i++)
                {
                    Vector3 worldPos = localToWorld.MultiplyPoint3x4(verts2D[i]);
                    if (worldPos.y < minWorldY)
                    {
                        minWorldY = worldPos.y;
                    }
                }
            }

            // If no sprites were found, fall back to this object’s world Y
            if (minWorldY == float.PositiveInfinity)
            {
                minWorldY = root.position.y;
            }

            // Convert the bottom-most world Y back into this transform’s local Y
            Vector3 bottomWorld = new Vector3(0f, minWorldY, 0f);

            return root.InverseTransformPoint(bottomWorld).y;
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!Application.isPlaying && labelAnchor != null)
            {
                GUIStyle style = new GUIStyle
                {
                    fontSize = 12,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter, // note: alignment is ignored in 2019.x
                    normal = new GUIStyleState { textColor = Color.yellow }
                };

                Handles.Label(labelAnchor.position, "Clip name [1 of X]", style);
            }
        }
#endif

        private void OnGUI()
        {
            if (_clips != null && _clips.Count > 0 && _clipIndex < _clips.Count)
            {
                string labelText = $"{_clips[_clipIndex].name}  [{_clipIndex + 1} of {_clips.Count}]";
                SetLabelText(labelText);
            }
        }

        private void SetLabelText(string labelText)
        {
            if (_mainCam == null)
            {
                _mainCam = Camera.main;
            }

            Vector3 worldPos = labelAnchor.position;
            Vector3 screenPos = _mainCam.WorldToScreenPoint(worldPos);

            float desiredWorldHeight = 0.8f; // tweak this to taste
            float pixelsPerUnit = Screen.height / (_mainCam.orthographicSize * 2f);
            int fontSize = Mathf.RoundToInt(pixelsPerUnit * desiredWorldHeight);

            GUIStyle style = new GUIStyle
            {
                fontSize = fontSize,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = new GUIStyleState { textColor = Color.yellow }
            };

            if (screenPos.z > 0f) // only if in front of camera
            {
                // flip Y for GUI
                float guiY = Screen.height - screenPos.y;

                GUI.Label(
                    new Rect(screenPos.x - 300f, guiY - 30f, 600f, 60f),
                    labelText,
                    style
                );
            }
        }
    }
}