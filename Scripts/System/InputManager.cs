using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.EventSystems;

using Moyo.Unity;
using System.Collections.Generic;
using Sirenix.OdinInspector;

public class InputManager : MonoSingleton<InputManager>,IMoyoEventListener<AppStateEvent>
{
    public Camera RealCamera { get; private set; }
    public Vector3 MousePos { get; private set; }
    public Vector2 MouseWheelDelta { get; private set; }

    /// <summary>全局鼠标主键点击（排除 UI 命中）。</summary>
    public event Action<Vector2> OnMousePrimaryClick;


    /// <summary>全局指针位置变更（无状态限制，用于别处需要）</summary>
    public event Action<Vector2> OnMouseMove;

    // —— 建造流程专用事件（与 .inputactions: BuildingInstance map 对齐）——
    public event Action<Vector2> Building_OnChangeCoordinates;
    public event Action Building_OnConfirmPlacement;
    public event Action Building_OnConfirmConstruction;


    /// <summary>仅在 GamePlay.MoveCamera 激活时为 true。</summary>
    public event Action<bool> GamePlay_OnMoveCamera;

    private LOControlsMaps inputActionMap;

    public bool IsGamePlayActive => inputActionMap != null && inputActionMap.GamePlay.enabled;
    
    
    //标记UI
    private bool _pendingMousePrimaryClick;
    //应用于标记UI的
    private Vector2 _pendingClickPosition;


    protected override void Initialize()
    {
        base.Initialize();
        RealCamera = Camera.main;

        inputActionMap = new LOControlsMaps();

        // Global 常开
        inputActionMap.Global.Enable();

        // 指针位置（PassThrough）
        inputActionMap.Global.MousePostionChange.performed += ctx =>
        {
            MousePos = ctx.ReadValue<Vector2>();
            OnMouseMove?.Invoke(MousePos);

            // 仅当 BuildingInstance map 已启用时，转发为“建造坐标改变”
            if (inputActionMap.Building.enabled)
            {
                Building_OnChangeCoordinates?.Invoke(MousePos);
            }
        };

        inputActionMap.Global.MouseWheelChanges.performed += ctx =>
        {
            Vector2 delta = ctx.ReadValue<Vector2>();
            if (delta.sqrMagnitude <= 0f) return;

            MouseWheelDelta = delta;
            for (int i = 0; i < _mouseWheelHandlers.Count; i++)
            {
                var handler = _mouseWheelHandlers[i];
                if (handler == null)
                {
                    _mouseWheelHandlers.RemoveAt(i);
                    i--;
                    continue;
                }
                if (handler.TryHandleSlide(delta.y)) return;
            }
        };


        inputActionMap.Global.MousePrimaryClick.performed += ctx =>
        {
            Vector2 clickPosition = MousePos;
            if (Mouse.current != null)
            {
                clickPosition = Mouse.current.position.ReadValue();
                MousePos = clickPosition;
            }

            _pendingClickPosition = clickPosition;
            _pendingMousePrimaryClick = true;
        };






        inputActionMap.Global.Back.performed += ctx =>
        {
            foreach (IBackHandler h in _backHandlers)
                if (h.TryHandleBack())
                {
                    Debug.Log($"返回被 {h.Priority} 消费");
                    return;
                }
        };


        // BuildingInstance 默认关闭，进入建造模式时再打开
        inputActionMap.Building.Disable();

        // 确认/取消：只订 performed；确认前做 UI 命中过滤
        inputActionMap.Building.ConfirmPlacement.performed += ctx =>
        {

            Building_OnConfirmPlacement?.Invoke();
        };



        inputActionMap.Building.ConfirmConstruction.performed += ctx =>
        {

            Building_OnConfirmConstruction?.Invoke();
        };

        // GamePlay 默认关闭，仅在游戏主体激活时启用
        inputActionMap.GamePlay.Disable();
        inputActionMap.GamePlay.MoveCamera.started += ctx => GamePlay_OnMoveCamera?.Invoke(true);
        inputActionMap.GamePlay.MoveCamera.performed += ctx => GamePlay_OnMoveCamera?.Invoke(true);
        inputActionMap.GamePlay.MoveCamera.canceled += ctx => GamePlay_OnMoveCamera?.Invoke(false);


    }



    // 供外部开关建造 map
    public void EnableBuildingMap() => inputActionMap.Building.Enable();
    public void DisableBuildingMap() => inputActionMap.Building.Disable();
    public bool IsBuildingMap() => inputActionMap.Building.enabled;

    [Button]
    public void EnableGamePlayMap() => inputActionMap.GamePlay.Enable();
    public void DisableGamePlayMap()
    {
        inputActionMap.GamePlay.Disable();
        GamePlay_OnMoveCamera?.Invoke(false);
    }
    public bool IsGamePlayMap() => inputActionMap.GamePlay.enabled;

    private void OnEnable()
    {
        this.MoyoEventStartListening<AppStateEvent>();    
    }
    
    private void LateUpdate()
    {
        if (!_pendingMousePrimaryClick)
        {
            return;
        }

        bool shouldInvoke = true;
        EventSystem currentEventSystem = EventSystem.current;
        if (currentEventSystem != null)
        {
            if (currentEventSystem.IsPointerOverGameObject())
            {
                shouldInvoke = false;
            }
        }

        if (shouldInvoke)
        {
            OnMousePrimaryClick?.Invoke(_pendingClickPosition);
        }

        _pendingMousePrimaryClick = false;
    }

    private void OnDisable()
    {
        this.MoyoEventStopListening<AppStateEvent>();
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
    }
    public void OnMoyoEvent(AppStateEvent eventType)
    {
        switch (eventType.State)
        {
            case AppState.游戏加载完成:
                break;
            case AppState.游戏场景加载完成:

               
                    RealCamera = Camera.main;
                

                break;
            case AppState.开始游戏:
                break;
            case AppState.游戏进行中:
                break;
            case AppState.结束游戏:
                break;
            default:
                break;
        }
    }



    private readonly List<IBackHandler> _backHandlers = new();
    public void Register(IBackHandler h)
    {
        _backHandlers.Add(h);
        _backHandlers.Sort((a, b) => b.Priority.CompareTo(a.Priority));

    }
    public void UnRegister(IBackHandler handler)
    {
        if (handler == null) return;
        _backHandlers.Remove(handler);
    }
    
     private readonly List<ISlideHandler> _mouseWheelHandlers = new();
    public void Register(ISlideHandler handler)
    {
        if (handler == null) return;
        if (_mouseWheelHandlers.Contains(handler)) return;

        _mouseWheelHandlers.Add(handler);
        _mouseWheelHandlers.Sort((a, b) => b.Priority.CompareTo(a.Priority));
    }
    public void UnRegister(ISlideHandler handler)
    {
        if (handler == null) return;
        _mouseWheelHandlers.Remove(handler);
    }

   
}
