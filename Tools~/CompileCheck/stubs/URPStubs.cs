// Compile-check-only stubs. Names/signatures copied from Unity-Technologies/Graphics master (URP + Core).
#if WILDLENS_URP
namespace UnityEngine.Rendering
{
    public abstract class VolumeParameter { public bool overrideState; }
    public class VolumeParameter<T> : VolumeParameter { public virtual T value { get; set; } public void Override(T x) { overrideState = true; value = x; } }
    public class BoolParameter : VolumeParameter<bool> { public BoolParameter(bool v, bool o = false) { } }
    public class IntParameter : VolumeParameter<int> { }
    public class ClampedIntParameter : IntParameter { public ClampedIntParameter(int v, int min, int max, bool o = false) { } }
    public class FloatParameter : VolumeParameter<float> { public FloatParameter(float v, bool o = false) { } }
    public class MinFloatParameter : FloatParameter { public MinFloatParameter(float v, float min, bool o = false) : base(v) { } }
    public class ClampedFloatParameter : FloatParameter { public ClampedFloatParameter(float v, float min, float max, bool o = false) : base(v) { } }
    public class ColorParameter : VolumeParameter<Color> { public ColorParameter(Color v, bool hdr = false, bool alpha = true, bool picker = true, bool o = false) { } }
    public class Vector2Parameter : VolumeParameter<Vector2> { }
    public class Vector4Parameter : VolumeParameter<Vector4> { }
    public class VolumeComponent : ScriptableObject { public bool active = true; public void SetAllOverridesTo(bool state) { } }
    public sealed class VolumeProfile : ScriptableObject
    {
        public T Add<T>(bool overrides = false) where T : VolumeComponent => default;
        public bool TryGet<T>(out T component) where T : VolumeComponent { component = default; return false; }
    }
    public class Volume : MonoBehaviour
    {
        public bool isGlobal { get; set; }
        public float priority = 0f;
        public float weight = 1f;
        public float blendDistance = 0f;
        public VolumeProfile sharedProfile = null;
        public VolumeProfile profile { get; set; }
    }
}
namespace UnityEngine.Rendering.Universal
{
    public enum DepthOfFieldMode { Off, Gaussian, Bokeh }
    public sealed class DepthOfFieldModeParameter : VolumeParameter<DepthOfFieldMode> { }
    public sealed class DepthOfField : VolumeComponent
    {
        public DepthOfFieldModeParameter mode; public MinFloatParameter gaussianStart, gaussianEnd, focusDistance;
        public ClampedFloatParameter gaussianMaxRadius, aperture, focalLength, bladeCurvature, bladeRotation;
        public BoolParameter highQualitySampling; public ClampedIntParameter bladeCount;
    }
    public sealed class Bloom : VolumeComponent { public MinFloatParameter threshold, intensity, clamp; public ClampedFloatParameter scatter; public ColorParameter tint; public BoolParameter highQualityFiltering; }
    public enum TonemappingMode { None, Neutral, ACES }
    public sealed class TonemappingModeParameter : VolumeParameter<TonemappingMode> { }
    public sealed class Tonemapping : VolumeComponent { public TonemappingModeParameter mode; }
    public sealed class ColorAdjustments : VolumeComponent { public FloatParameter postExposure; public ClampedFloatParameter contrast, hueShift, saturation; public ColorParameter colorFilter; }
    public sealed class WhiteBalance : VolumeComponent { public ClampedFloatParameter temperature, tint; }
    public sealed class Vignette : VolumeComponent { public ColorParameter color; public Vector2Parameter center; public ClampedFloatParameter intensity, smoothness; public BoolParameter rounded; }
    public sealed class ShadowsMidtonesHighlights : VolumeComponent { public Vector4Parameter shadows, midtones, highlights; public MinFloatParameter shadowsStart, shadowsEnd, highlightsStart, highlightsEnd; }
    public enum ShadowQuality { Disabled, HardShadows, SoftShadows }
    public enum ShadowResolution { _256 = 256, _512 = 512, _1024 = 1024, _2048 = 2048, _4096 = 4096 }
    public enum AntialiasingMode { None, FastApproximateAntialiasing, SubpixelMorphologicalAntiAliasing, TemporalAntiAliasing }
    public class UniversalAdditionalCameraData : MonoBehaviour { public bool renderPostProcessing { get; set; } public AntialiasingMode antialiasing { get; set; } }
    public static class CameraExtensions { public static UniversalAdditionalCameraData GetUniversalAdditionalCameraData(this Camera camera) => null; }
}
#endif
#if WILDLENS_INPUT_SYSTEM_1_9
namespace UnityEngine.InputSystem
{
    public class InputSettings { public enum ScrollDeltaBehavior { UniformAcrossAllPlatforms = 0, KeepPlatformSpecificInputRange = 1 } public ScrollDeltaBehavior scrollDeltaBehavior { get; set; } }
    public static class InputSystem { public static InputSettings settings => null; }
}
#endif
