using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class DebugManager : MonoBehaviour
{
    [Header("是否显示鼠标所在单元格坐标")]
    public bool b_显示鼠标位置的坐标;

    [Header("是否显示鼠标射线点击信息")]
    public bool b_显示鼠标射线点击信息;

    [Header("字体设置")]
    public int fontSize = 20; // 字体大小

    [Header("射线检测设置")]
    [Tooltip("用于射线检测的相机，不设置则使用 Camera.main")]
    public Camera raycastCamera;

    private GUIStyle _guiStyle;

    // 射线点击信息
    private string _lastClickHitText;
    private Vector2 _lastClickGuiPos; // 记录点击时的 GUI 坐标位置

    private void Awake()
    {
        InitGuiStyle();
    }

    private void Update()
    {
        if (b_显示鼠标射线点击信息 && Input.GetMouseButtonDown(0))
        {
            UpdateRaycastInfo();
        }
    }

    private void OnGUI()
    {
        if (_guiStyle == null)
            InitGuiStyle();

        Show_鼠标坐标();
        Show_射线点击信息();
    }

    #region GUI 绘制

    private void Show_鼠标坐标()
    {
        if (!b_显示鼠标位置的坐标)
            return;

        if (GridSystem.Instance == null)
            return;

        // 获取鼠标所在单元格坐标（网格坐标）
        var cellCoor = GridSystem.Instance.GetMouseCubeCoor();

        // 屏幕坐标转换为 GUI 坐标（Y 轴反向）
        Vector3 mousePos = Input.mousePosition;
        mousePos.y = Screen.height - mousePos.y;

        string text = $"Cell: ({cellCoor.r}, {cellCoor.q}, {cellCoor.s})";

        GUIContent content = new GUIContent(text);
        Vector2 size = _guiStyle.CalcSize(content);

        Rect rect = new Rect(
            mousePos.x + 15f,
            mousePos.y + 15f,
            size.x + 10f,
            size.y + 10f
        );

        GUI.Box(rect, content, _guiStyle);
    }

    private void Show_射线点击信息()
    {
        if (!b_显示鼠标射线点击信息)
            return;

        if (string.IsNullOrEmpty(_lastClickHitText))
            return;

        GUIContent content = new GUIContent(_lastClickHitText);
        Vector2 size = _guiStyle.CalcSize(content);

        Rect rect = new Rect(
            _lastClickGuiPos.x + 15f,
            _lastClickGuiPos.y + 15f,
            size.x + 10f,
            size.y + 10f
        );

        GUI.Box(rect, content, _guiStyle);
    }

    #endregion

    #region 射线检测逻辑

    private void UpdateRaycastInfo()
    {
        Camera cam = raycastCamera != null ? raycastCamera : Camera.main;
        if (cam == null)
        {
            _lastClickHitText = "射线检测失败：无有效相机";
            return;
        }

        Vector3 mousePos = Input.mousePosition;

        // 记录点击时的 GUI 坐标（用于 OnGUI 绘制）
        _lastClickGuiPos = new Vector2(mousePos.x, Screen.height - mousePos.y);

        List<string> hitNames = new List<string>();

        // 1. 3D 射线检测
        Ray ray = cam.ScreenPointToRay(mousePos);
        if (Physics.Raycast(ray, out RaycastHit hit3D))
        {
            hitNames.Add($"{hit3D.collider.gameObject.name} (3D)");
        }

        // 2. 2D 射线检测
        Vector2 worldPoint2D = cam.ScreenToWorldPoint(mousePos);
        RaycastHit2D hit2D = Physics2D.Raycast(worldPoint2D, Vector2.zero);
        if (hit2D.collider != null)
        {
            hitNames.Add($"{hit2D.collider.gameObject.name} (2D)");
        }

        // 3. UI 射线检测（uGUI）
        if (EventSystem.current != null)
        {
            var graphicRaycasters = FindObjectsOfType<GraphicRaycaster>();
            if (graphicRaycasters.Length > 0)
            {
                PointerEventData ped = new PointerEventData(EventSystem.current)
                {
                    position = mousePos
                };

                List<RaycastResult> uiResults = new List<RaycastResult>();

                foreach (var gr in graphicRaycasters)
                {
                    gr.Raycast(ped, uiResults);
                }

                foreach (var r in uiResults)
                {
                    hitNames.Add($"{r.gameObject.name} (UI)");
                }
            }
        }

        if (hitNames.Count == 0)
        {
            _lastClickHitText = "Click Hit: None";
        }
        else
        {
            _lastClickHitText = "Click Hit: " + string.Join(", ", hitNames);
        }
    }

    #endregion

    #region GUIStyle / 工具

    private void InitGuiStyle()
    {
        _guiStyle = new GUIStyle
        {
            fontSize = fontSize,
            alignment = TextAnchor.MiddleCenter,
            padding = new RectOffset(5, 5, 5, 5)
        };

        _guiStyle.normal.textColor = Color.white;
        _guiStyle.normal.background = MakeTex(2, 2, new Color(0f, 0f, 0f, 0.7f));
    }

    // 创建一个纯色纹理用于背景
    private Texture2D MakeTex(int width, int height, Color col)
    {
        Color[] pix = new Color[width * height];
        for (int i = 0; i < pix.Length; i++)
        {
            pix[i] = col;
        }

        Texture2D result = new Texture2D(width, height);
        result.SetPixels(pix);
        result.Apply();
        return result;
    }

    // 在 Inspector 中修改 fontSize 时实时更新样式
    private void OnValidate()
    {
        if (_guiStyle != null)
        {
            _guiStyle.fontSize = fontSize;
        }
    }

    #endregion



    #region 输入模式
    [ShowInInspector,FoldoutGroup("输入模式")]
    public bool EableGamePlayMap {
        get
        {
            if (!InputManager.HasInstance)
            {
                return false;
            }
            else
            {
                return InputManager.Instance.IsGamePlayMap();
            }

        }
       
    }
    #endregion
}
