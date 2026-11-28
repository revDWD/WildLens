// Compile-check-only stubs mirroring the uGUI + Input System API surface used by WildLens.
namespace UnityEngine.UI
{
    public class Graphic : UnityEngine.MonoBehaviour { public UnityEngine.Color color { get; set; } public bool raycastTarget { get; set; } public UnityEngine.RectTransform rectTransform => null; }
    public class MaskableGraphic : Graphic { }
    public class Image : MaskableGraphic { public enum Type { Simple, Sliced, Tiled, Filled } public UnityEngine.Sprite sprite { get; set; } public Type type { get; set; } }
    public class RawImage : MaskableGraphic { public UnityEngine.Texture texture { get; set; } }
    public class Text : MaskableGraphic { public UnityEngine.Font font { get; set; } public string text { get; set; } public int fontSize { get; set; } public UnityEngine.TextAnchor alignment { get; set; } public UnityEngine.FontStyle fontStyle { get; set; } public UnityEngine.HorizontalWrapMode horizontalOverflow { get; set; } public UnityEngine.VerticalWrapMode verticalOverflow { get; set; } public bool supportRichText { get; set; } public float lineSpacing { get; set; } public float preferredWidth => 0f; public float preferredHeight => 0f; public bool resizeTextForBestFit { get; set; } public int resizeTextMinSize { get; set; } public int resizeTextMaxSize { get; set; } }
    public class BaseMeshEffect : UnityEngine.MonoBehaviour { }
    public class Shadow : BaseMeshEffect { public UnityEngine.Color effectColor { get; set; } public UnityEngine.Vector2 effectDistance { get; set; } }
    public class Outline : Shadow { }
    public class CanvasScaler : UnityEngine.MonoBehaviour { public enum ScaleMode { ConstantPixelSize, ScaleWithScreenSize, ConstantPhysicalSize } public ScaleMode uiScaleMode { get; set; } public UnityEngine.Vector2 referenceResolution { get; set; } public float matchWidthOrHeight { get; set; } public float dynamicPixelsPerUnit { get; set; } }
    public class AspectRatioFitter : UnityEngine.MonoBehaviour { public enum AspectMode { None, WidthControlsHeight, HeightControlsWidth, FitInParent, EnvelopeParent } public AspectMode aspectMode { get; set; } public float aspectRatio { get; set; } }
}
#if WILDLENS_INPUT_SYSTEM
namespace UnityEngine.InputSystem.Controls
{
    public class ButtonControl { public bool isPressed => false; public bool wasPressedThisFrame => false; }
    public class KeyControl : ButtonControl { }
    public class Vector2Control { public UnityEngine.Vector2 ReadValue() => default; }
}
namespace UnityEngine.InputSystem
{
    using UnityEngine.InputSystem.Controls;
    public class Keyboard { public static Keyboard current => null;
        public KeyControl wKey, sKey, aKey, dKey, upArrowKey, downArrowKey, leftShiftKey, fKey, jKey, tabKey, escapeKey, hKey, cKey, leftCtrlKey, eKey, qKey, f3Key, f4Key, rKey, tKey, mKey, leftArrowKey, rightArrowKey; }
    public class Mouse { public static Mouse current => null; public Vector2Control delta, scroll; public ButtonControl leftButton, rightButton; }
}
#endif
