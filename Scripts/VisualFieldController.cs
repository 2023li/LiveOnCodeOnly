using Cinemachine;
using UnityEngine;
using Moyo.Unity;
using Sirenix.OdinInspector;
using System.Collections;
using System;


public class VisualFieldController : MonoBehaviour, ISlideHandler
{


    #region 字段与属性




    private CinemachineVirtualCamera virtualCamera; // 用于平移的虚拟相机或相机父物体
    private Camera realCamera; // 主相机引用

    [Header("屏幕边缘平移设置")]
    [SerializeField] private float screenEdgeThreshold_Vert = 50f;
    [SerializeField] private float screenEdgeThreshold_Hor = 50f;
    [SerializeField] private float maxPanSpeed = 25f;
    [Header("视野缩放设置")]
    [SerializeField] private float zoomSpeed = 10f;
    [SerializeField] private float minOrthoSize = 3f;  // 改为正交大小
    [SerializeField] private float maxOrthoSize = 10f; // 改为正交大小
    [SerializeField] private float smoothTime = 0.1f;
    private float targetOrthoSize; // 改为目标正交大小
    private float currentZoomVelocity; // SmoothDamp所需的当前速度值

    // 私有状态变量
    private Vector2 mousePosition;
    private Vector3 targetPanVelocity = Vector3.zero;
    private bool isPanning = false;



    private Vector3 manualControl = new Vector3(0, 0, -999);

    #endregion

    #region Unity生命周期方法

    // 在编辑器中重置组件时自动绑定字段

    private void OnEnable()
    {
        var inputManager = InputManager.Instance;
        if (inputManager != null)
        {
            inputManager.Register(this);
        }
    }


    private void Start()
    {
        // 获取主相机并初始化OrthoSize
        realCamera = InputManager.Instance.RealCamera;
        virtualCamera = transform.GetComponent<CinemachineVirtualCamera>();
        // 初始化targetOrthoSize为虚拟相机的当前正交大小
        if (virtualCamera != null)
        {
            targetOrthoSize = virtualCamera.m_Lens.OrthographicSize;
          
        }
        else
        {
            targetOrthoSize = (minOrthoSize + maxOrthoSize) / 2f; // 默认值
            Debug.LogWarning("virtualCamera未赋值，使用默认正交大小");
        }

    }


    private void Update()
    {
       


    }


    // LateUpdate用于处理相机相关的逻辑，确保在所有Update之后执行
    private void LateUpdate()
    {

        if (lookToTargetPos.z == 0)
        {
            HandleLookTo();
        }

        if (InputManager.Instance.IsGamePlayMap())
        {
            if (manualControl.z == 0)
            {
                Debug.Log("手动控制逻辑没实现");
            }
            else
            {
                //没有任何输入的时候才会直执行这个
                HandleScreenEdgePan();
            }
            //即使手动控制也可以缩放
            ApplySmoothZoom();
        }
    }

    private void OnDisable()
    {
        if (!InputManager.HasInstance) return;
        InputManager.Instance.UnRegister(this);
    }

    #endregion

    public short Priority { get; set; } = LOConstant.InputPriority.Priority_相机监听鼠标滚轮;
    public bool TryHandleSlide(float scrollInput)
    {
       
        if (scrollInput == 0)
        {
           
            return true;
        }

        // 对于正交相机：向上滚动(正值)减小OrthoSize(拉近)，向下滚动(负值)增大OrthoSize(拉远)
        targetOrthoSize -= scrollInput * zoomSpeed * Time.deltaTime;
        targetOrthoSize = Mathf.Clamp(targetOrthoSize, minOrthoSize, maxOrthoSize);

      
        return true;
    }


    #region 相机控制逻辑
    private void HandleScreenEdgePan()
    {

        mousePosition = InputManager.Instance.MousePos;
        if (virtualCamera == null)
        {
            Debug.LogError("请赋值virtualCameraTransform（虚拟相机的Transform）");
            return;
        }

        targetPanVelocity = Vector3.zero;
        isPanning = false;

        float horizontalFactor = 0;
        if (mousePosition.x < screenEdgeThreshold_Hor)
        {
            horizontalFactor = -(1 - (mousePosition.x / screenEdgeThreshold_Hor));
            isPanning = true;
        }
        else if (mousePosition.x > Screen.width - screenEdgeThreshold_Hor)
        {
            horizontalFactor = 1 - ((Screen.width - mousePosition.x) / screenEdgeThreshold_Hor);
            isPanning = true;
        }

        float verticalFactor = 0;
        if (mousePosition.y < screenEdgeThreshold_Vert)
        {
            verticalFactor = -(1 - (mousePosition.y / screenEdgeThreshold_Vert));
            isPanning = true;
        }
        else if (mousePosition.y > Screen.height - screenEdgeThreshold_Vert)
        {
            verticalFactor = 1 - ((Screen.height - mousePosition.y) / screenEdgeThreshold_Vert);
            isPanning = true;
        }


        horizontalFactor = Mathf.Clamp(horizontalFactor, -1, 1);
        verticalFactor = Mathf.Clamp(verticalFactor, -1, 1);

        if (virtualCamera.transform.position.x < realCamera.transform.position.x - 0.5f && horizontalFactor < 0)
        {
            horizontalFactor = 0;
        }
        if (virtualCamera.transform.position.x > realCamera.transform.position.x + 0.5f && horizontalFactor > 0)
        {
            horizontalFactor = 0;
        }
        if (virtualCamera.transform.position.y < realCamera.transform.position.y - 0.5f && verticalFactor < 0)
        {
            verticalFactor = 0;
        }
        if (virtualCamera.transform.position.y > realCamera.transform.position.y + 0.5f && verticalFactor > 0)
        {
            verticalFactor = 0;
        }

        targetPanVelocity = new Vector3(horizontalFactor * maxPanSpeed, verticalFactor * maxPanSpeed, 0);
        virtualCamera.transform.Translate(targetPanVelocity * Time.deltaTime, Space.World);

    }

    /// <summary>
    /// 【新增】在LateUpdate中平滑地应用视野缩放
    /// </summary>
    private void ApplySmoothZoom()
    {
        if (virtualCamera == null) return;

        // 使用Mathf.SmoothDamp平滑地改变当前正交大小
        float currentOrthoSize = virtualCamera.m_Lens.OrthographicSize;
        float newOrthoSize = Mathf.SmoothDamp(currentOrthoSize, targetOrthoSize, ref currentZoomVelocity, smoothTime);
        virtualCamera.m_Lens.OrthographicSize = newOrthoSize;

        // 调试信息
        if (showDebugAreas&&Mathf.Abs(newOrthoSize - currentOrthoSize) > 0.001f)
        {
            Debug.Log($"正交缩放: {currentOrthoSize:F2} -> {newOrthoSize:F2}, 目标: {targetOrthoSize:F2}");
        }
    }




    #endregion

    #region API

    private Vector3 lookToTargetPos = new Vector3(0, 0, -999);
    public void LookTo(Vector3 worldPos)
    {
        lookToTargetPos = worldPos;
    }
    private void HandleLookTo()
    {

        // 目标与相机的XY向量（Z清零）
        var cur = CommonTools.Vector3NoZ(realCamera.transform.position);
        var target = CommonTools.Vector3NoZ(lookToTargetPos);
        var dir = target - cur;

        // 用平方模比较更省
        if (dir.sqrMagnitude > 0.05f * 0.05f)
        {
            // 每帧朝目标推进一小步（世界坐标，不会越过目标）
            var step = maxPanSpeed * Time.deltaTime;
            var next = Vector3.MoveTowards(cur, target, step);
            // 只改XY，保留原Z
            realCamera.transform.position = new Vector3(next.x, next.y, realCamera.transform.position.z);
        }
        else
        {
            // 直接吸附到目标XY，避免残余误差抖动
            realCamera.transform.position = new Vector3(target.x, target.y, realCamera.transform.position.z);
            // 清空目标（建议别用魔法数，见下方建议）
            lookToTargetPos = new Vector3(0, 0, -999);
        }
    }




    #endregion


    #region 调试与可视化

    [Header("调试设置")]
    [SerializeField] private bool showDebugAreas = true;
    [SerializeField] private Color debugAreaColor = new Color(1f, 0f, 0f, 0.3f);
    [SerializeField] private Color activeDebugAreaColor = new Color(0f, 1f, 0f, 0.5f);

  

    private void OnGUI()
    {
        if (!showDebugAreas || !InputManager.Instance.IsGamePlayMap()) return;
        DrawDebugEdgeAreas();
        DrawDebugInfo();
    }

    [Button("切换调试显示")]
    private void ToggleDebugDisplay()
    {
        showDebugAreas = !showDebugAreas;
    }

    [Button("测试边缘平移")]
    private void TestEdgePan()
    {
        StartCoroutine(TestEdgePanCoroutine());
    }

    private IEnumerator TestEdgePanCoroutine()
    {
        Debug.Log("开始边缘平移测试...");
        showDebugAreas = true;
        mousePosition = new Vector2(10, 10);
        yield return new WaitForSeconds(2f);
        mousePosition = new Vector2(Screen.width - 10, Screen.height - 10);
        yield return new WaitForSeconds(2f);
        mousePosition = new Vector2(Screen.width / 2, Screen.height / 2);
        Debug.Log("边缘平移测试完成");
    }

    private void DrawDebugEdgeAreas()
    {
        Rect leftArea = new Rect(0, 0, screenEdgeThreshold_Hor, Screen.height);
        Rect rightArea = new Rect(Screen.width - screenEdgeThreshold_Hor, 0, screenEdgeThreshold_Hor, Screen.height);
        Rect bottomArea = new Rect(0, Screen.height - screenEdgeThreshold_Vert, Screen.width, screenEdgeThreshold_Vert);
        Rect topArea = new Rect(0, 0, Screen.width, screenEdgeThreshold_Vert);

        bool leftActive = mousePosition.x < screenEdgeThreshold_Hor;
        bool rightActive = mousePosition.x > Screen.width - screenEdgeThreshold_Hor;
        bool bottomActive = mousePosition.y < screenEdgeThreshold_Vert;
        bool topActive = mousePosition.y > Screen.height - screenEdgeThreshold_Vert;

        DrawRect(leftArea, leftActive);
        DrawRect(rightArea, rightActive);
        DrawRect(bottomArea, bottomActive);
        DrawRect(topArea, topActive);
        DrawMouseIndicator();
    }

    private void DrawRect(Rect rect, bool isActive)
    {
        Color color = isActive ? activeDebugAreaColor : debugAreaColor;
        Texture2D texture = new Texture2D(1, 1);
        texture.SetPixel(0, 0, color);
        texture.Apply();
        GUI.skin.box.normal.background = texture;
        GUI.Box(rect, GUIContent.none);
    }

    private void DrawMouseIndicator()
    {
        float guiMouseY = Screen.height - mousePosition.y;
        float crossSize = 10f;
        GUIStyle style = new GUIStyle(GUI.skin.label)
        {
            normal = { textColor = Color.yellow },
            alignment = TextAnchor.MiddleCenter
        };

        GUI.Label(new Rect(mousePosition.x - crossSize, guiMouseY - 1, crossSize * 2, 2), "", style);
        GUI.Label(new Rect(mousePosition.x - 1, guiMouseY - crossSize, 2, crossSize * 2), "", style);
        GUI.Label(new Rect(mousePosition.x + 15, guiMouseY - 20, 200, 40),
                  $"Mouse: ({mousePosition.x:F0}, {mousePosition.y:F0})", style);
    }

    private void DrawDebugInfo()
    {
        GUIStyle style = new GUIStyle(GUI.skin.label)
        {
            normal = { textColor = Color.white },
            fontSize = 12
        };

        string debugText = $"目标速度: {targetPanVelocity.magnitude:F2}\n" +
                           $"是否平移中: {isPanning}\n" +
                           $"相机位置: {virtualCamera.transform.position}\n" +
                           $"目标OrthoSize: {targetOrthoSize:F2}\n" +  // 更新调试信息
                           $"当前OrthoSize: {(virtualCamera != null ? virtualCamera.m_Lens.OrthographicSize.ToString("F2") : "N/A")}";

        GUI.Label(new Rect(10, 10, 300, 120), debugText, style);
    }



    #endregion


}
