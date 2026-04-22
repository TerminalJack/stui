// Modifications Copyright (c) 2026 TerminalJack
// Licensed under the MIT License. See the LICENSE.TXT file in the project root for details.
//
// Portions of this file are derived from the Spriter2UnityDX project.
// The original author provided an open-use permission statement, preserved in THIRD_PARTY_NOTICES.md.

using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System;

namespace Stui
{
    public static class CurveBuilder
    {
        public static AnimationCurve CreateInstantCurve(float startTime = 0f, float endTime = 1f,
            float startValue = 0f, float endValue = 1f)
        {
            // For instant (aka constant value) curves, we just need to set the time and
            // value of the keyframes.  AnimationUtility.SetKey___TangentMode() will set
            // weightedMode, weights, and tangents to appropriate values.  Times and
            // values can be scaled and offset and the AnimationUtility class will
            // calculate the proper values for tangents, etc.
            //
            // Note: The curve's value will start at 'startValue' and remain at that value until
            // 'endTime'.  The curve's value will instantly take the value 'endValue' at 'endTime.'
            // 'startValue' and 'endValue' can be the same value.

            Keyframe keyInfo0 = new Keyframe(startTime, startValue);
            Keyframe keyInfo1 = new Keyframe(endTime, endValue);

            Keyframe[] keyframes = new Keyframe[] { keyInfo0, keyInfo1 };

            AnimationCurve animCurve = new AnimationCurve(keyframes);

            AnimationUtility.SetKeyBroken(animCurve, 0, true);
            AnimationUtility.SetKeyRightTangentMode(animCurve, 0, AnimationUtility.TangentMode.Constant);

            AnimationUtility.SetKeyBroken(animCurve, 1, true);
            AnimationUtility.SetKeyLeftTangentMode(animCurve, 1, AnimationUtility.TangentMode.Constant);

            return animCurve;
        }

        public static AnimationCurve CreateLinearCurve(float startTime = 0f, float endTime = 1f,
            float startValue = 0f, float endValue = 1f)
        {
            // For linear curves, we just need to set the time and value of the keyframes.
            // AnimationUtility.SetKey___TangentMode() will set weightedMode, weights, and
            // tangents to appropriate values.  Times and values can be scaled and offset
            // and the AnimationUtility class will calculate the proper values for tangents,
            // etc.

            Keyframe keyInfo0 = new Keyframe(startTime, startValue);
            Keyframe keyInfo1 = new Keyframe(endTime, endValue);

            Keyframe[] keyframes = new Keyframe[] { keyInfo0, keyInfo1 };

            AnimationCurve animCurve = new AnimationCurve(keyframes);

            AnimationUtility.SetKeyBroken(animCurve, 0, true);
            AnimationUtility.SetKeyRightTangentMode(animCurve, 0, AnimationUtility.TangentMode.Linear);

            AnimationUtility.SetKeyBroken(animCurve, 1, true);
            AnimationUtility.SetKeyLeftTangentMode(animCurve, 1, AnimationUtility.TangentMode.Linear);

            return animCurve;
        }

        public static AnimationCurve Create1dQuadraticCurve(float c1, float startTime = 0f, float endTime = 1f,
            float startValue = 0f, float endValue = 1f)
        {
            // Basically just a 2d cubic Bezier curve but the weights are set to 1 / 3 and the
            // tangents are based on the following formulas:
            //
            //   outTangent = 2 * c1
            //   inTangent = 2 - outTangent
            //
            // Where c1 is the control point from Spriter.

            float outTangent = 2f * c1;
            float inTangent = 2f - outTangent;

            Keyframe keyInfo0 = new Keyframe(0f, 0f, 0f, outTangent, 0f, 1f / 3f);
            Keyframe keyInfo1 = new Keyframe(1f, 1f, inTangent, 0f, 1f / 3f, 0f);

            keyInfo0.weightedMode = WeightedMode.Out;
            keyInfo1.weightedMode = WeightedMode.In;

            Keyframe[] keyframes = new Keyframe[] { keyInfo0, keyInfo1 };

            AnimationCurve animCurve = new AnimationCurve(keyframes);

            AnimationUtility.SetKeyBroken(animCurve, 0, true);
            AnimationUtility.SetKeyRightTangentMode(animCurve, 0, AnimationUtility.TangentMode.Free);

            AnimationUtility.SetKeyBroken(animCurve, 1, true);
            AnimationUtility.SetKeyLeftTangentMode(animCurve, 1, AnimationUtility.TangentMode.Free);

            return ScaleAndOffsetAnimCurve(animCurve, startTime, endTime, startValue, endValue);
        }

        public static AnimationCurve Create1dCubicCurve(float c1, float c2,
            float startTime = 0f, float endTime = 1f, float startValue = 0f, float endValue = 1f)
        {
            // Basically just a 2d cubic Bezier curve but the weights are set to 1 / 3 and the
            // tangents are based on the following formulas:
            //
            //   outTangent = 3 * c1
            //   inTangent = 3 - 3 * c2
            //
            // Where c1 and c2 are the two control points from Spriter.

            float outTangent = 3f * c1;
            float inTangent = 3f - 3f * c2;

            Keyframe keyInfo0 = new Keyframe(0f, 0f, 0f, outTangent, 0f, 1f / 3f);
            Keyframe keyInfo1 = new Keyframe(1f, 1f, inTangent, 0f, 1f / 3f, 0f);

            keyInfo0.weightedMode = WeightedMode.Out;
            keyInfo1.weightedMode = WeightedMode.In;

            Keyframe[] keyframes = new Keyframe[] { keyInfo0, keyInfo1 };

            AnimationCurve animCurve = new AnimationCurve(keyframes);

            AnimationUtility.SetKeyBroken(animCurve, 0, true);
            AnimationUtility.SetKeyRightTangentMode(animCurve, 0, AnimationUtility.TangentMode.Free);

            AnimationUtility.SetKeyBroken(animCurve, 1, true);
            AnimationUtility.SetKeyLeftTangentMode(animCurve, 1, AnimationUtility.TangentMode.Free);

            return ScaleAndOffsetAnimCurve(animCurve, startTime, endTime, startValue, endValue);
        }

        public static AnimationCurve Create2dCubicBezierCurve(float x1, float y1, float x2, float y2,
            float startTime = 0f, float endTime = 1f, float startValue = 0f, float endValue = 1f)
        {
            // A Spriter Bezier curve is the same as Unity's except Unity uses weights and
            // tangents [(outWeight, outTangent) and (inWeight, inTangent)] for the control
            // points whereas Spriter uses Cartesian points [(inX, inY) and (outX, outY)]
            // for the control points.
            //
            // Spriter's Bezier curves (like all other Spriter curves types) are 'normalized'
            // in the sense that both the x-axis (aka time) and y-axis (aka animated value)
            // start at zero and end at 1.  The x value of control points is restricted to
            // the range [0, 1].  The y value of control points can take values outside of
            // this range.
            //
            // Spriter's control points map to Unity's Bezier representation as follows:
            //
            // outWeight = x1
            // outTangent = y1 / outWeight
            // inWeight = 1 - x2
            // inTangent = (1 - y2) / inWeight
            //
            // Note that, to prevent division by 0, the weights will not be allowed to have
            // a value of 0.

            float outWeight = x1 == 0f ? 0.00001f : x1;
            float outTangent = y1 / outWeight;
            float inWeight = x2 == 1f ? 0.00001f : 1f - x2;
            float inTangent = (1f - y2) / inWeight;

            Keyframe keyInfo0 = new Keyframe(0f, 0f, 0f, outTangent, 0f, outWeight);
            Keyframe keyInfo1 = new Keyframe(1f, 1f, inTangent, 0f, inWeight, 0f);

            keyInfo0.weightedMode = WeightedMode.Out;
            keyInfo1.weightedMode = WeightedMode.In;

            Keyframe[] keyframes = new Keyframe[] { keyInfo0, keyInfo1 };

            AnimationCurve animCurve = new AnimationCurve(keyframes);

            AnimationUtility.SetKeyBroken(animCurve, 0, true);
            AnimationUtility.SetKeyRightTangentMode(animCurve, 0, AnimationUtility.TangentMode.Free);

            AnimationUtility.SetKeyBroken(animCurve, 1, true);
            AnimationUtility.SetKeyLeftTangentMode(animCurve, 1, AnimationUtility.TangentMode.Free);

            return ScaleAndOffsetAnimCurve(animCurve, startTime, endTime, startValue, endValue);
        }

        public static AnimationCurve Create1dQuarticCurve(float c1, float c2, float c3,
            float startTime = 0f, float endTime = 1f, float startValue = 0f, float endValue = 1f)
        {
            float[] Q = new float[] { 0f, c1, c2, c3, 1f };

            AnimationCurve animCurve = QuarticOrQuinticToAnimCurve1D.Convert(Q);

            return ScaleAndOffsetAnimCurve(animCurve, startTime, endTime, startValue, endValue);
        }

        public static AnimationCurve Create1dQuinticCurve(float c1, float c2, float c3, float c4,
            float startTime = 0f, float endTime = 1f, float startValue = 0f, float endValue = 1f)
        {
            float[] Q = new float[] { 0f, c1, c2, c3, c4, 1f };

            AnimationCurve animCurve = QuarticOrQuinticToAnimCurve1D.Convert(Q);

            return ScaleAndOffsetAnimCurve(animCurve, startTime, endTime, startValue, endValue);
        }

        public static AnimationCurve ScaleAndOffsetAnimCurve(AnimationCurve animCurve, float startTime, float endTime, float startValue, float endValue)
        {
            if (startTime == 0f && startValue == 0f && endTime == 1f && endValue == 1f)
            {
                return animCurve;
            }

            float timeScale = endTime - startTime; // Must always be positive and non-zero.
            float valueScale = endValue - startValue; // May be negative, positive, or zero.

            float tangentScale = timeScale <= 0f ? 1f : valueScale / timeScale;

            AnimationCurve newCurve = new AnimationCurve();

            for (int i = 0; i < animCurve.keys.Length; ++i)
            {
                Keyframe keyframe = animCurve.keys[i];

                keyframe.time = timeScale * keyframe.time + startTime;
                keyframe.value = valueScale * keyframe.value + startValue;
                keyframe.inTangent *= tangentScale;
                keyframe.outTangent *= tangentScale;

                newCurve.AddKey(keyframe);
            }

            return newCurve;
        }

        /// <summary>
        /// Populates an existing AnimationCurve by concatenating multiple curves.
        /// Filters duplicate boundary keys, preserves tangents, weights,
        /// left/right tangent modes, and broken flags (Editor only).
        /// </summary>
        /// <param name="target">Curve to write into (will be overwritten).</param>
        /// <param name="curves">Time-ordered input curves.</param>
        public static void ConcatenateCurvesInto(AnimationCurve target, params AnimationCurve[] curves)
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            var keyframes = new List<Keyframe>();
            var leftModes = new List<AnimationUtility.TangentMode>();
            var rightModes = new List<AnimationUtility.TangentMode>();
            var broken = new List<bool>();
            float lastTime = float.NaN;

            // 1) Gather and merge/copy keys from all curves.
            for (int curveIdx = 0; curveIdx < curves.Length; ++curveIdx)
            {
                var curve = curves[curveIdx];

                if (curve == null || curve.length == 0)
                {
                    continue;
                }

                for (int keyIdx = 0; keyIdx < curve.length; ++keyIdx)
                {
                    var src = curve.keys[keyIdx];

                    // Read editor‐only modes and broken flags.
                    var lMode = AnimationUtility.GetKeyLeftTangentMode(curve, keyIdx);
                    var rMode = AnimationUtility.GetKeyRightTangentMode(curve, keyIdx);
                    var isBroken = AnimationUtility.GetKeyBroken(curve, keyIdx);

                    if (curveIdx > 0 && keyIdx == 0 && Mathf.Approximately(src.time, lastTime))
                    {   // Duplicate boundary.  Merge with previous key.
                        int prevKeyIdx = keyframes.Count - 1;
                        var prevKey = keyframes[prevKeyIdx];

                        // Build merged key:
                        var merged = new Keyframe(
                            src.time,
                            src.value,
                            prevKey.inTangent,
                            src.outTangent,
                            prevKey.inWeight,
                            src.outWeight
                        );

                        keyframes[prevKeyIdx] = merged;

                        // Use this key's outgoing mode, preserve incoming mode of previous key.
                        // leftModes[prevKeyIdx] remains as-is.
                        rightModes[prevKeyIdx] = rMode;
                        broken[prevKeyIdx] = broken[prevKeyIdx] | isBroken;
                    }
                    else
                    {   // Simple copy
                        keyframes.Add(src);
                        leftModes.Add(lMode);
                        rightModes.Add(rMode);
                        broken.Add(isBroken);
                    }

                    lastTime = src.time;
                }
            }

            // 2) Overwrite target AnimationCurve.
            target.keys = keyframes.ToArray();
            target.preWrapMode = curves.Length > 0 ? curves[0].preWrapMode : WrapMode.Once;
            target.postWrapMode = curves.Length > 0 ? curves[0].postWrapMode : WrapMode.Once;

            // 3) Re‐apply tangent modes and broken flags.
            for (int i = 0; i < target.length; ++i)
            {
                AnimationUtility.SetKeyBroken(target, i, broken[i]);
                AnimationUtility.SetKeyLeftTangentMode(target, i, leftModes[i]);
                AnimationUtility.SetKeyRightTangentMode(target, i, rightModes[i]);
            }
        }

        public static class CurveFitter
        {
            /// <summary>
            /// Take raw samples and build a key at each sample, then force linear tangents.
            /// </summary>
            public static AnimationCurve FromRawSamples(List<float> samples, float duration, float timeOffset)
            {
                int count = samples.Count;
                float dt = duration / (count - 1);
                var keys = new Keyframe[count];

                for (int i = 0; i < count; i++)
                {
                    float time = timeOffset + i * dt;
                    float value = samples[i];
                    keys[i] = new Keyframe(time, value, 0f, 0f);
                }

                var curve = new AnimationCurve(keys);
                ApplyLinearMode(curve);
                return curve;
            }

            /// <summary>
            /// Adaptively pick points until error threshold, but still interpolate linearly.
            /// </summary>
            public static AnimationCurve FromAdaptiveFit(List<float> samples, float duration, float timeOffset, float maxError)
            {
                int totalSamples = samples.Count;
                float dt = duration / (totalSamples - 1);
                var keys = new List<Keyframe>();

                int i0 = 0;
                while (i0 < totalSamples - 1)
                {
                    int i3 = Mathf.Min(i0 + 1, totalSamples - 1);
                    float t0 = i0 * dt;
                    float p0 = samples[i0];

                    // grow i3 until error exceeds maxError
                    while (i3 < totalSamples - 1)
                    {
                        float tNext = (i3 + 1) * dt;
                        float pNext = samples[i3 + 1];
                        float frac = (tNext - t0) / ((i3 + 1 - i0) * dt);
                        float estimate = Mathf.Lerp(p0, pNext, frac);
                        float actual = samples[i3];
                        if (Mathf.Abs(actual - estimate) > maxError)
                            break;
                        i3++;
                    }

                    float t3 = i3 * dt;
                    float p3 = samples[i3];

                    keys.Add(new Keyframe(t0 + timeOffset, p0, 0f, 0f));
                    keys.Add(new Keyframe(t3 + timeOffset, p3, 0f, 0f));

                    i0 = i3;
                }

                var curve = new AnimationCurve(keys.ToArray());
                ApplyLinearMode(curve);
                return curve;
            }

            /// <summary>
            /// Editor-only helper: marks every key as broken and sets tangent modes to Linear.
            /// </summary>
            private static void ApplyLinearMode(AnimationCurve curve)
            {
                for (int i = 0; i < curve.length; i++)
                {
                    var key = curve[i];
                    key.inTangent = 0f;
                    key.outTangent = 0f;
                    curve.MoveKey(i, key);

                    AnimationUtility.SetKeyBroken(curve, i, true);
                    AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
                    AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
                }
            }
        }

        private static class QuarticOrQuinticToAnimCurve1D
        {
            /// <summary>
            /// Convert a 1D quartic Bézier (Q[0..4]) or 1D quintic Bézier (Q[0..5]) into
            /// a single AnimationCurve made up of cubic‐Hermite segments, minimizing segment count.
            /// </summary>
            /// <param name="Q">Array of 5 or 6 floats: the quartic/quintic Bézier control points.</param>
            /// <param name="tolerance">Max allowed deviation per segment.</param>
            /// <param name="maxDepth">Max recursion depth.</param>
            /// <returns>AnimationCurve approximating the input quartic/quintic.</returns>
            public static AnimationCurve Convert(float[] Q, float tolerance = 0.001f, int maxDepth = 10)
            {
                int curveOrder = Q.Length - 1;

                // 1) Build quartic/quintic derivative control polygon dC[i] = curveOrder * (Q[i+1] - Q[i])
                float[] dC = new float[curveOrder];

                for (int i = 0; i < curveOrder; ++i)
                {
                    dC[i] = curveOrder * (Q[i + 1] - Q[i]);
                }

                // 2) Recursively subdivide into Hermite cubics.
                var segments = new List<HermiteSegment>();
                Subdivide(Q, dC, 0f, 1f, tolerance, maxDepth, 0, segments);

                // 3) Sort by start-time and build the AnimationCurve.
                segments.Sort((a, b) => a.t0.CompareTo(b.t0));
                var curve = new AnimationCurve();
                bool firstKey = true;

                foreach (var seg in segments)
                {
                    // At each segment boundary we know:
                    //  P0 = seg.P0, P3 = seg.P3
                    //  d0 = seg.d0, d1 = seg.d1
                    float t0 = seg.t0;
                    float t1 = seg.t1;
                    float P0 = seg.P0;
                    float P3 = seg.P3;
                    float d0 = seg.d0;
                    float d1 = seg.d1;

                    if (firstKey)
                    {
                        // Add start key with slope d0.
                        curve.AddKey(new Keyframe(t0, P0, d0, d0));
                        firstKey = false;
                    }

                    // Add end key with slope d1.
                    curve.AddKey(new Keyframe(t1, P3, d1, d1));
                }

                return curve;
            }

            // Recursively split [t0,t1] until a Hermite‐cubic fits within tolerance.
            private static void Subdivide(
                float[] Q, float[] dC,
                float t0, float t1,
                float tol, int maxDepth, int depth,
                List<HermiteSegment> outSegs)
            {
                // Evaluate endpoints and slopes.
                float P0 = DeCasteljau(Q, t0);
                float P3 = DeCasteljau(Q, t1);
                float d0 = DeCasteljau(dC, t0);
                float d1 = DeCasteljau(dC, t1);

                // Estimate error of this cubic vs. the original quartic/quintic.
                float err = EstimateError(Q, P0, P3, d0, d1, t0, t1, 12);
                if (err <= tol || depth >= maxDepth)
                {
                    outSegs.Add(new HermiteSegment
                    {
                        t0 = t0,
                        t1 = t1,
                        P0 = P0,
                        P3 = P3,
                        d0 = d0,
                        d1 = d1
                    });
                }
                else
                {
                    // Split in half and recurse.
                    float tm = 0.5f * (t0 + t1);
                    Subdivide(Q, dC, t0, tm, tol, maxDepth, depth + 1, outSegs);
                    Subdivide(Q, dC, tm, t1, tol, maxDepth, depth + 1, outSegs);
                }
            }

            // Measure max |quartic(t) or quintic(t) - HermiteCubic(t)| over 'samples' points.
            private static float EstimateError(
                float[] Q,
                float P0, float P3, float d0, float d1,
                float t0, float t1,
                int samples)
            {
                float maxErr = 0f;
                float dt = t1 - t0;

                for (int i = 0; i <= samples; ++i)
                {
                    float u = i / (float)samples;
                    float t = Mathf.Lerp(t0, t1, u);

                    // Original quartic/quintic value.
                    float Qv = DeCasteljau(Q, t);

                    // Hermite cubic basis on [0,1].
                    float u2 = u * u;
                    float u3 = u2 * u;
                    float h0 = 2f * u3 - 3f * u2 + 1f;
                    float h1 = u3 - 2f * u2 + u;
                    float h2 = -2f * u3 + 3f * u2;
                    float h3 = u3 - u2;
                    // Build Hermite value.
                    float Hv = h0 * P0
                             + h1 * (dt * d0)
                             + h2 * P3
                             + h3 * (dt * d1);

                    maxErr = Mathf.Max(maxErr, Mathf.Abs(Qv - Hv));
                }

                return maxErr;
            }

            // 1D De Casteljau: works for any-degree control array.
            private static float DeCasteljau(float[] C, float t)
            {
                float[] tmp = (float[])C.Clone();
                int n = tmp.Length;

                for (int r = 1; r < n; ++r)
                {
                    for (int i = 0; i < n - r; ++i)
                    {
                        tmp[i] = tmp[i] * (1 - t) + tmp[i + 1] * t;
                    }
                }

                return tmp[0];
            }

            // Holds one cubic‐Hermite segment’s data.
            private class HermiteSegment
            {
                public float t0, t1;   // Global parameter range.
                public float P0, P3;   // Endpoint values.
                public float d0, d1;   // Endpoint slopes.
            }
        }
    }
}