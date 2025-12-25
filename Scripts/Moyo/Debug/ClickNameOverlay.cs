// ClickNameOverlay.cs
// 说明：使用 IMGUI 在鼠标旁边显示“单击命中的物体名称”，不创建 Canvas，不污染场景。
// 修复：不在 Awake()/Update() 中访问 GUI.*，仅在 OnGUI() 中初始化/使用 GUIStyle。

using UnityEngine;
using UnityEngine.EventSystems;

[DisallowMultipleComponent]
[AddComponentMenu("Debug/Click Name Overlay (IMGUI)")]
public class ClickNameOverlay : MonoBehaviour
{
    [Header("Raycast")]
    [Tooltip("用于发射射线的摄像机；为空则使用 Camera.main（若为空再退到 Camera.current/任意摄像机）。")]
    public Camera raycastCamera;

    [Tooltip("射线检测的层级筛选。")]
    public LayerMask raycastMask = ~0;

    [Tooltip("射线最大距离。")]
    public float maxDistance = 1000f;

    [Tooltip("是否对 2D 物体（Collider2D）进行命中检测。")]
    public bool include2D = true;

    [Header("UI 显示")]
    [Tooltip("鼠标相对偏移（像素）。")]
    public Vector2 pixelOffset = new Vector2(18f, 18f);

    [Tooltip("点击后文字保留的秒数（不使用 TimeScale，暂停时仍显示）。")]
    public float displaySeconds = 1.2f;

    [Tooltip("字体大小。")]
    public int fontSize = 16;

    [Tooltip("文字颜色。")]
    public Color textColor = Color.white;

    [Tooltip("背景颜色（含透明度）。")]
    public Color backgroundColor = new Color(0, 0, 0, 0.6f);

    [Tooltip("文字与背景的内边距。")]
    public Vector2 padding = new Vector2(8f, 4f);

    [Tooltip("将标签矩形限制在屏幕范围内。")]
    public bool clampToScreen = true;

    // 状态
    private string _lastName;
    private float _hideAtUnscaled;
    private Vector3 _lastMousePosScreen;

    // GUI 缓存（只能在 OnGUI 中创建/修改）
    private GUIStyle _labelStyle;
    private int _cachedFontSize;
    private Color _cachedTextColor;

    // 背景纹理（不依赖 GUI，可在非 OnGUI 生命周期安全创建）
    private Texture2D _bgTex;
    private Color _lastBgColor;

    // ————————————————— 生命周期 —————————————————

    private void Awake()
    {
        // 不要在这里访问 GUI.*（包括 GUI.skin），否则会报错。
        // 背景纹理不依赖 GUI，可提前创建。
        BuildBgTextureIfNeeded();
    }

    private void OnDestroy()
    {
        if (_bgTex != null)
        {
            Destroy(_bgTex);
            _bgTex = null;
        }
    }

    private void Update()
    {
        // 左键点击
        if (Input.GetMouseButtonDown(0))
        {
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                return;

            var cam = GetRaycastCamera();
            if (cam == null)
                return;

            _lastMousePosScreen = Input.mousePosition;

            string hitName = TryRaycast(cam);
            if (string.IsNullOrEmpty(hitName))
                hitName = "（未命中）";

            _lastName = hitName;
            _hideAtUnscaled = Time.unscaledTime + Mathf.Max(0.01f, displaySeconds);
        }

        // 根据颜色变更重建背景纹理（不触碰 GUI）
        if (_bgTex == null || _lastBgColor != backgroundColor)
        {
            BuildBgTextureIfNeeded();
        }
    }

    private void OnGUI()
    {
        if (string.IsNullOrEmpty(_lastName))
            return;
        if (Time.unscaledTime > _hideAtUnscaled)
            return;

        // 只在 OnGUI 内准备 GUIStyle（此处访问 GUI.skin 是安全的）
        EnsureLabelStyleOnGUI();

        // IMGUI 的 (0,0) 在左上角；Input.mousePosition 的 (0,0) 在左下角
        float guiX = _lastMousePosScreen.x + pixelOffset.x;
        float guiY = (Screen.height - _lastMousePosScreen.y) + pixelOffset.y;

        var content = new GUIContent(_lastName);
        Vector2 textSize = _labelStyle.CalcSize(content);
        Rect rect = new Rect(guiX, guiY, textSize.x + padding.x * 2f, textSize.y + padding.y * 2f);

        if (clampToScreen)
        {
            if (rect.xMax > Screen.width) rect.x = Screen.width - rect.width;
            if (rect.yMax > Screen.height) rect.y = Screen.height - rect.height;
            if (rect.x < 0) rect.x = 0;
            if (rect.y < 0) rect.y = 0;
        }

        // 背景
        if (_bgTex != null)
            GUI.DrawTexture(rect, _bgTex, ScaleMode.StretchToFill, true);

        // 文字
        Rect textRect = new Rect(rect.x + padding.x, rect.y + padding.y,
                                 rect.width - padding.x * 2f, rect.height - padding.y * 2f);
        GUI.Label(textRect, content, _labelStyle);
    }

    // ————————————————— 工具方法 —————————————————

    private Camera GetRaycastCamera()
    {
        if (raycastCamera != null) return raycastCamera;
        if (Camera.main != null) return Camera.main;
        if (Camera.current != null) return Camera.current;
        if (Camera.allCamerasCount > 0) return Camera.allCameras[0];
        return null;
    }

    private string TryRaycast(Camera cam)
    {
        Ray ray = cam.ScreenPointToRay(Input.mousePosition);

        // 3D
        if (Physics.Raycast(ray, out var hit3D, maxDistance, raycastMask, QueryTriggerInteraction.Ignore))
        {
            return hit3D.collider.gameObject.name;
        }

        // 2D
        if (include2D)
        {
            var hit2D = Physics2D.GetRayIntersection(ray, maxDistance, raycastMask);
            if (hit2D.collider != null)
                return hit2D.collider.gameObject.name;
        }

        return null;
    }

    // 只在 OnGUI 调用：安全访问 GUI.skin 以构建样式
    private void EnsureLabelStyleOnGUI()
    {
        if (_labelStyle == null || _cachedFontSize != fontSize || _cachedTextColor != textColor)
        {
            // 注意：这行必须在 OnGUI 里，外面会触发“只能在 OnGUI 调用 GUI 函数”的异常
            GUIStyle baseStyle = (GUI.skin != null) ? GUI.skin.label : new GUIStyle();
            _labelStyle = new GUIStyle(baseStyle)
            {
                alignment = TextAnchor.MiddleLeft,
                fontSize = Mathf.Max(10, fontSize),
                wordWrap = false
            };
            _labelStyle.normal.textColor = textColor;

            _cachedFontSize = fontSize;
            _cachedTextColor = textColor;
        }
    }

    // 背景纹理与 GUI 无关，可在任意生命周期调用
    private void BuildBgTextureIfNeeded()
    {
        if (_bgTex == null)
        {
            _bgTex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        }
        _bgTex.SetPixel(0, 0, backgroundColor);
        _bgTex.Apply();
        _lastBgColor = backgroundColor;
    }
}
