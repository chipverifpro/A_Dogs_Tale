using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Controls "sniff mode" visual treatment:
/// - desaturates and darkens the world
/// - applies a purplish color tint
/// - adds vignette
/// - optional subtle blur via Depth of Field
/// </summary>
public class SniffModeVisuals : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Global Volume with ColorAdjustments + Vignette (+ optional DepthOfField) overrides.")]
    public Volume sniffVolume;

    [Header("Transition")]
    public float transitionDuration = 0.35f;

    // ----- Color adjustments targets -----
    [Header("Color Adjustments (Sniff Mode Targets)")]
    [Tooltip("Saturation in sniff mode (-100 = full grayscale).")]
    [Range(-100f, 0f)]
    public float sniffSaturation = -80f;

    [Tooltip("Post exposure in sniff mode (negative = darker).")]
    [Range(-3f, 3f)]
    public float sniffPostExposure = -0.5f;

    [Tooltip("Hue shift in sniff mode (degrees).")]
    [Range(-180f, 180f)]
    public float sniffHueShift = 20f;

    [Tooltip("Color filter tint for sniff mode (e.g. purplish grey).")]
    public Color sniffColorFilter = new Color(0.8f, 0.7f, 1.0f, 1f);

    // ----- Vignette targets -----
    [Header("Vignette (Sniff Mode Targets)")]
    [Tooltip("Vignette intensity in sniff mode.")]
    [Range(0f, 1f)]
    public float sniffVignetteIntensity = 0.35f;

    [Tooltip("Vignette smoothness in sniff mode.")]
    [Range(0f, 1f)]
    public float sniffVignetteSmoothness = 0.7f;

    [Tooltip("Vignette color in sniff mode.")]
    public Color sniffVignetteColor = new Color(0.1f, 0.0f, 0.15f, 1f);

    // ----- Depth of Field / blur targets -----
    [Header("Depth Of Field (Optional Blur)")]
    [Tooltip("Enable subtle blur in sniff mode using DepthOfField.")]
    public bool enableBlur = true;

    [Tooltip("Focus distance in sniff mode (smaller = closer focus, more background blur).")]
    public float sniffFocusDistance = 5f;

    [Tooltip("Aperture in sniff mode (smaller number = shallower DOF, more blur).")]
    public float sniffAperture = 4f;

    [Tooltip("Focal length in sniff mode (higher can exaggerate blur).")]
    public float sniffFocalLength = 50f;

    // This parameter is not implemented.  See comment in the code later in this file.
    //[Header("ScentFog layer")]
    //[Tooltip("Brighten scent fog in sniff mode.")]
    //public float sniffFogBoost = 1.5f;


    // ----- internal state -----
    private ColorAdjustments colorAdj;
    private Vignette vignette;
    private DepthOfField dof;

    private float baseSaturation;
    private float basePostExposure;
    private float baseHueShift;
    private Color baseColorFilter;

    private float baseVignetteIntensity;
    private float baseVignetteSmoothness;
    private Color baseVignetteColor;

    private float baseFocusDistance;
    private float baseAperture;
    private float baseFocalLength;

    private Coroutine transitionRoutine;
    private bool isInSniffMode = false;

    void Awake()
    {
        if (sniffVolume == null)
        {
            Debug.LogError("SniffModeVisuals: sniffVolume not assigned.");
            enabled = false;
            return;
        }

        // Color adjustments
        if (!sniffVolume.profile.TryGet(out colorAdj))
        {
            Debug.LogError("SniffModeVisuals: Volume has no ColorAdjustments override.");
            enabled = false;
            return;
        }

        // Vignette (optional but recommended)
        sniffVolume.profile.TryGet(out vignette);

        // Depth of Field (optional)
        sniffVolume.profile.TryGet(out dof);

        // Cache base (normal world) values
        baseSaturation   = colorAdj.saturation.value;
        basePostExposure = colorAdj.postExposure.value;
        baseHueShift     = colorAdj.hueShift.value;
        baseColorFilter  = colorAdj.colorFilter.value;

        if (vignette != null)
        {
            baseVignetteIntensity  = vignette.intensity.value;
            baseVignetteSmoothness = vignette.smoothness.value;
            baseVignetteColor      = vignette.color.value;
        }

        if (dof != null)
        {
            baseFocusDistance = dof.focusDistance.value;
            baseAperture      = dof.aperture.value;
            baseFocalLength   = dof.focalLength.value;
        }
    }

    /// <summary>
    /// Public API: call this when entering/exiting sniff mode.
    /// </summary>
    public void SetSniffMode(bool enabled)
    {
        if (isInSniffMode == enabled)
            return;

        isInSniffMode = enabled;

        // Apply global shader property for fog boost
        //if (sniffFogBoost != 1.0)
        //    Shader.SetGlobalFloat("_SniffFogBoost", enabled ? sniffFogBoost : 1.0f);

        // NOTE: to use the sniffFogBoost above...
        // In your fog material’s shader (ShaderGraph or HLSL), multiply your final output
        // by this global float:
        //
        /// float _SniffFogBoost;
        ///
        /// half4 Frag(Varyings IN) : SV_Target
        /// {
        ///     float4 col = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv);
        ///     col.rgb *= _SniffFogBoost;      // boost color
        ///     col.a   *= _SniffFogBoost;      // boost opacity (optional)
        ///     return col;
        /// }

        if (transitionRoutine != null)
            StopCoroutine(transitionRoutine);

        transitionRoutine = StartCoroutine(LerpSniffMode(enabled));
    }

    private IEnumerator LerpSniffMode(bool enabled)
    {
        float t = 0f;

        // Start values
        float startSat   = colorAdj.saturation.value;
        float startExp   = colorAdj.postExposure.value;
        float startHue   = colorAdj.hueShift.value;
        Color startColor = colorAdj.colorFilter.value;

        // Target values
        float targetSat   = enabled ? sniffSaturation   : baseSaturation;
        float targetExp   = enabled ? sniffPostExposure : basePostExposure;
        float targetHue   = enabled ? sniffHueShift     : baseHueShift;
        Color targetColor = enabled ? sniffColorFilter  : baseColorFilter;

        float startVigInt = vignette != null ? vignette.intensity.value  : 0f;
        float startVigSm  = vignette != null ? vignette.smoothness.value : 0f;
        Color startVigCol = vignette != null ? vignette.color.value      : Color.black;

        float targetVigInt = enabled ? sniffVignetteIntensity  : baseVignetteIntensity;
        float targetVigSm  = enabled ? sniffVignetteSmoothness : baseVignetteSmoothness;
        Color targetVigCol = enabled ? sniffVignetteColor      : baseVignetteColor;

        float startFocusDist = dof != null ? dof.focusDistance.value : 0f;
        float startAperture  = dof != null ? dof.aperture.value      : 0f;
        float startFocalLen  = dof != null ? dof.focalLength.value   : 0f;

        float targetFocusDist = enabled ? sniffFocusDistance : baseFocusDistance;
        float targetAperture  = enabled ? sniffAperture      : baseAperture;
        float targetFocalLen  = enabled ? sniffFocalLength   : baseFocalLength;

        // Ensure the volume actively contributes
        sniffVolume.weight = 1f;

        while (t < transitionDuration)
        {
            float u = (transitionDuration <= 0f) ? 1f : (t / transitionDuration);

            // Color adjustments
            colorAdj.saturation.value   = Mathf.Lerp(startSat,   targetSat,   u);
            colorAdj.postExposure.value = Mathf.Lerp(startExp,   targetExp,   u);
            colorAdj.hueShift.value     = Mathf.Lerp(startHue,   targetHue,   u);
            colorAdj.colorFilter.value  = Color.Lerp(startColor, targetColor, u);

            // Vignette
            if (vignette != null)
            {
                vignette.intensity.value  = Mathf.Lerp(startVigInt, targetVigInt, u);
                vignette.smoothness.value = Mathf.Lerp(startVigSm,  targetVigSm,  u);
                vignette.color.value      = Color.Lerp(startVigCol, targetVigCol, u);
            }

            // Depth of Field (blur)
            if (dof != null && enableBlur)
            {
                dof.focusDistance.value = Mathf.Lerp(startFocusDist, targetFocusDist, u);
                dof.aperture.value      = Mathf.Lerp(startAperture,  targetAperture,  u);
                dof.focalLength.value   = Mathf.Lerp(startFocalLen,  targetFocalLen,  u);
            }

            t += Time.unscaledDeltaTime;
            yield return null;
        }

        // Snap to final values
        colorAdj.saturation.value   = targetSat;
        colorAdj.postExposure.value = targetExp;
        colorAdj.hueShift.value     = targetHue;
        colorAdj.colorFilter.value  = targetColor;

        if (vignette != null)
        {
            vignette.intensity.value  = targetVigInt;
            vignette.smoothness.value = targetVigSm;
            vignette.color.value      = targetVigCol;
        }

        if (dof != null && enableBlur)
        {
            dof.focusDistance.value = targetFocusDist;
            dof.aperture.value      = targetAperture;
            dof.focalLength.value   = targetFocalLen;
        }

        transitionRoutine = null;
    }
}