using System;
using UnityEngine;
using UnityEngine.UI;
using Moyo.Unity;

/// <summary>
/// 建筑放置确认条面板，负责处理确认/取消按钮与定位。
/// </summary>
public class BuildingConfirmPanel : PanelBase
{
    public class Args
    {
        public Action OnConfirm;
        public Action OnCancel;
    }

    private const float DefaultYOffsetMultiplier = 0.6f;

    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private Button confirmButton;
    [SerializeField] private Button cancelButton;

    private RectTransform rectTransform;
    private Action onConfirm;
    private Action onCancel;
    private bool listenersBound;

    protected override void Awake()
    {
        base.Awake();

        rectTransform = transform as RectTransform;
        EnsureCanvasGroup();
        EnsureButtons();
        BindButtonListeners();
        SetInteractable(false);
    }

    public override void OnPanelCreated(params object[] args)
    {
        base.OnPanelCreated(args);
        ApplyArgs(args);
    }

    public override void Show()
    {
        SetInteractable(true);
    }

    public override void Show(params object[] args)
    {
        base.Show(args);
        SetInteractable(true);
    }

    public override void Hide()
    {
    
        SetInteractable(false);
    }

    /// <summary>
    /// 设置世界坐标锚点，并转换为当前画布的锚点位置。
    /// </summary>
    public void SetWorldAnchor(Vector3 anchorWorldPos)
    {
        if (rectTransform == null || canvas == null) return;

        var canvasRect = canvas.GetComponent<RectTransform>();
        if (canvasRect == null) return;

        var cam = InputManager.Instance != null ? InputManager.Instance.RealCamera : Camera.main;
        var grid = GridSystem.Instance;
        var yOffset = grid != null ? grid.mapGrid.cellSize.y * DefaultYOffsetMultiplier : 0f;

        var screenPoint = cam != null
            ? cam.WorldToScreenPoint(anchorWorldPos + new Vector3(0f, yOffset, 0f))
            : Vector3.zero;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect,
            screenPoint,
            canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera,
            out var localPoint);

        rectTransform.anchoredPosition = localPoint;
    }

    private void ApplyArgs(object[] args)
    {
        onConfirm = null;
        onCancel = null;

        if (args == null || args.Length == 0) return;

        foreach (var arg in args)
        {
            switch (arg)
            {
                case Args typed:
                    onConfirm = typed.OnConfirm;
                    onCancel = typed.OnCancel;
                    return;
                case Action act when onConfirm == null:
                    onConfirm = act;
                    break;
                case Action act when onCancel == null:
                    onCancel = act;
                    break;
            }
        }
    }

    private void EnsureCanvasGroup()
    {
        if (canvasGroup != null) return;
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }
    }

    private void EnsureButtons()
    {
        if (confirmButton != null && cancelButton != null) return;

        var buttons = GetComponentsInChildren<Button>(true);
        foreach (var btn in buttons)
        {
            if (confirmButton == null && btn.name.Contains("Confirm", StringComparison.OrdinalIgnoreCase))
            {
                confirmButton = btn;
                continue;
            }

            if (cancelButton == null && btn.name.Contains("Cancel", StringComparison.OrdinalIgnoreCase))
            {
                cancelButton = btn;
            }
        }
    }

    private void BindButtonListeners()
    {
        if (listenersBound) return;
        listenersBound = true;

        if (confirmButton != null)
        {
            confirmButton.onClick.AddListener(OnConfirmClicked);
        }

        if (cancelButton != null)
        {
            cancelButton.onClick.AddListener(OnCancelClicked);
        }
    }

    private void OnConfirmClicked()
    {
        onConfirm?.Invoke();
    }

    private void OnCancelClicked()
    {
        onCancel?.Invoke();
    }

    private void SetInteractable(bool value)
    {
        if (canvasGroup == null) return;
        canvasGroup.alpha = value ? 1f : 0f;
        canvasGroup.interactable = value;
        canvasGroup.blocksRaycasts = value;
    }
}
