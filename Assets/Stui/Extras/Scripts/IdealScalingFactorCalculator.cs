// Modifications Copyright (c) 2026 TerminalJack
// Licensed under the MIT License. See the LICENSE.TXT file in the project root for details.
//
// Portions of this file are derived from the Spriter2UnityDX project.
// The original author provided an open-use permission statement, preserved in THIRD_PARTY_NOTICES.md.

using UnityEngine;

// -----------------------------------------------------------------------------
// How to determine the ideal scaling factor and PPU for an imported Spriter entity
// -----------------------------------------------------------------------------
//
// TL;DR — Quick Use:
//   1. Place prefab in scene with your final orthographic camera.
//   2. Attach this script to the prefab root.
//   3. Set target resolution, assign camera.
//   4. Uniform-scale prefab to final in-game size.
//   5. Read Scaling Factor & Recommended PPU in Inspector.
//   6. Use Scaling Factor when resizing Spriter project, and Recommended PPU when importing.
//
// Full Guide:
// 1. In your scene, ensure the orthographic camera you intend to use in-game
//    (the "Target Camera") is set to its final orthographic size.
// 2. Drag your Spriter prefab into the scene.
// 3. Attach this script to the root GameObject of the instantiated prefab.
// 4. In the Inspector, set:
//      • Target Resolution = the resolution you are targeting for the resize.
//      • Target Camera     = your in-game orthographic camera.
// 5. Scale the prefab in the scene to match its intended final in-game size.
//    Only uniform scaling (same X and Y) is supported.
// 6. While the scene is running or in Edit mode, the script will display:
//      • Scaling Factor  = the ideal scaling factor to use when generating a
//                          resized Spriter project.
//      • Recommended PPU = the Pixels Per Unit value to use when importing
//                          the resized Spriter project.
// 7. Use the calculated Scaling Factor with either the 'Resize Spriter Project'
//    utility or Spriter's 'Save as resized project' feature.
// 8. Use the Recommended PPU when importing the .scml file generated in step 7.
// -----------------------------------------------------------------------------

[ExecuteAlways]
public class IdealScalingFactorCalculator : MonoBehaviour
{
    [Header("Target Resolution")]
    [Tooltip("The screen resolution you are targeting.")]
    public int targetScreenWidth = 3840;
    public int targetScreenHeight = 2160;

    [Header("Camera")]
    [Tooltip("The orthographic camera used for display.")]
    public Camera targetCamera;

    [Header("Results (read-only)")]
    [Tooltip("The ideal scaling factor--that which produces images that are 'pixel-perfect' at the target resolution.  " +
        "Use this value when generating the resized Spriter project.")]
    public float scalingFactor;

    [Tooltip("Recommended PPU to use when importing the resized Spriter project.")]
    public float recommendedPPU;

    private void Awake()
    {
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }
    }

    private void OnValidate()
    {
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        UpdateInfo();
    }

    private void Update()
    {
        UpdateInfo();
    }

    private void UpdateInfo()
    {
        Sprite sprite = null;

        foreach (var sr in GetComponentsInChildren<SpriteRenderer>(includeInactive: false))
        {
            if (sr.enabled && sr.sprite != null)
            {
                sprite = sr.sprite;
                break;
            }
        }

        if (sprite == null || targetCamera == null || !targetCamera.orthographic)
        {
            scalingFactor = 0;
            recommendedPPU = 0;
        }
        else
        {
            float origPPU = sprite.pixelsPerUnit;

            recommendedPPU = targetScreenHeight / (2f * targetCamera.orthographicSize);

            scalingFactor = transform.localScale.y * recommendedPPU / origPPU;
        }
    }
}
