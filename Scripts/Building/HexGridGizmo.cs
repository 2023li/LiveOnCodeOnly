using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteInEditMode]
public class HexGridGizmo : MonoBehaviour
{
    [Header("网格设置")]
    public float hexRadius = 1f;          // 六边形半径（中心到顶点）
    public float gridHeight = 0.01f;      // 网格绘制高度（相对物体）
    public int gridSize = 5;              // 网格半径（从中心向外扩展的层数）
    public Color lineColor = Color.cyan;  // 网格线颜色

    [Header("显示选项")]
    public bool showGrid = true;
    public bool showCoordinates = true;
    public bool useCubeCoordinates = true; // 使用立方体坐标系

    [Header("平面选择")]
    public PlaneOrientation planeOrientation = PlaneOrientation.XY;

    public enum PlaneOrientation
    {
        XY, // 2D平面
        XZ  // 3D地面
    }

    private void OnDrawGizmosSelected()
    {
        if (!showGrid || !enabled) return;

        Gizmos.color = lineColor;

        // 绘制六边形网格
        DrawHexGrid();
    }

    private void DrawHexGrid()
    {
        // 使用立方体坐标生成六边形网格
        for (int x = -gridSize; x <= gridSize; x++)
        {
            for (int y = -gridSize; y <= gridSize; y++)
            {
                int z = -x - y; // 立方体坐标约束：x + y + z = 0

                // 只绘制在范围内的六边形
                if (Mathf.Abs(z) <= gridSize)
                {
                    Vector3 center = GetHexWorldPosition(x, y, z);
                    DrawHexagon(center, true); // true表示尖顶六边形

                    // 绘制坐标标签
#if UNITY_EDITOR
                    if (showCoordinates)
                    {
                        Vector3 labelPos = center;
                        if (planeOrientation == PlaneOrientation.XY)
                        {
                            labelPos += Vector3.forward * 0.1f; // 在Z轴方向偏移一点显示标签
                        }
                        else
                        {
                            labelPos += Vector3.up * 0.1f; // 在Y轴方向偏移一点显示标签
                        }

                        string coordText;
                        if (useCubeCoordinates)
                        {
                            coordText = $"({x}, {y}, {z})";
                        }
                        else
                        {
                            // 轴向坐标 (q, r)
                            int q = x;
                            int r = z;
                            coordText = $"({q}, {r})";
                        }

                        Handles.Label(labelPos, coordText);
                    }
#endif
                }
            }
        }

        // 绘制原点标记
        Gizmos.color = Color.yellow;
        Vector3 origin = GetPlanePosition(Vector3.zero);
        Gizmos.DrawWireSphere(origin + GetHeightOffset(), 0.1f);
    }

    private Vector3 GetHexWorldPosition(int x, int y, int z)
    {
        // 尖顶六边形的坐标转换
        // 使用立方体坐标转换到世界坐标
        float q = x; // 立方体坐标的x就是轴向坐标的q
        float r = z; // 立方体坐标的z就是轴向坐标的r

        float worldX, worldY;

        if (planeOrientation == PlaneOrientation.XY)
        {
            // XY平面（2D）
            worldX = hexRadius * (Mathf.Sqrt(3f) * q + Mathf.Sqrt(3f) / 2f * r);
            worldY = hexRadius * (3f / 2f * r);
            return GetPlanePosition(new Vector3(worldX, worldY, 0));
        }
        else
        {
            // XZ平面（3D地面）
            worldX = hexRadius * (Mathf.Sqrt(3f) * q + Mathf.Sqrt(3f) / 2f * r);
            float worldZ = hexRadius * (3f / 2f * r);
            return GetPlanePosition(new Vector3(worldX, 0, worldZ));
        }
    }

    private Vector3 GetPlanePosition(Vector3 position)
    {
        // 应用物体的变换（位置、旋转、缩放）
        Vector3 worldPos = transform.TransformPoint(position);

        // 添加高度偏移
        return worldPos + GetHeightOffset();
    }

    private Vector3 GetHeightOffset()
    {
        if (planeOrientation == PlaneOrientation.XY)
        {
            // XY平面：在Z轴方向添加高度偏移
            return new Vector3(0, 0, gridHeight);
        }
        else
        {
            // XZ平面：在Y轴方向添加高度偏移
            return new Vector3(0, gridHeight, 0);
        }
    }

    private void DrawHexagon(Vector3 center, bool pointyTop = true)
    {
        Vector3[] corners = CalculateHexCorners(pointyTop);

        for (int i = 0; i < 6; i++)
        {
            Vector3 start = center + corners[i];
            Vector3 end = center + corners[(i + 1) % 6];
            Gizmos.DrawLine(start, end);
        }
    }

    private Vector3[] CalculateHexCorners(bool pointyTop = true)
    {
        Vector3[] corners = new Vector3[6];

        for (int i = 0; i < 6; i++)
        {
            float angle;
            if (pointyTop)
            {
                // 尖顶六边形：一个顶点朝上，角度偏移30度
                angle = (60f * i + 30f) * Mathf.Deg2Rad;
            }
            else
            {
                // 平顶六边形
                angle = 60f * i * Mathf.Deg2Rad;
            }

            if (planeOrientation == PlaneOrientation.XY)
            {
                // XY平面：在XY平面上绘制
                corners[i] = new Vector3(
                    hexRadius * Mathf.Cos(angle),
                    hexRadius * Mathf.Sin(angle),
                    0
                );
            }
            else
            {
                // XZ平面：在XZ平面上绘制
                corners[i] = new Vector3(
                    hexRadius * Mathf.Cos(angle),
                    0,
                    hexRadius * Mathf.Sin(angle)
                );
            }
        }

        return corners;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        // 当属性在Inspector中改变时，强制重绘Scene视图
        SceneView.RepaintAll();
    }

    // 添加自定义Inspector界面
    [CustomEditor(typeof(HexGridGizmo))]
    public class HexGridGizmoEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            HexGridGizmo script = (HexGridGizmo)target;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("网格预览", EditorStyles.boldLabel);

            // 显示网格信息
            int totalHexes = CalculateTotalHexes(script.gridSize);
            EditorGUILayout.LabelField($"六边形数量: {totalHexes}");

            // 快速设置按钮
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("小网格 (半径3)"))
            {
                script.gridSize = 3;
                script.hexRadius = 1f;
                SceneView.RepaintAll();
            }
            if (GUILayout.Button("中网格 (半径5)"))
            {
                script.gridSize = 5;
                script.hexRadius = 1f;
                SceneView.RepaintAll();
            }
            if (GUILayout.Button("大网格 (半径10)"))
            {
                script.gridSize = 10;
                script.hexRadius = 0.5f;
                SceneView.RepaintAll();
            }
            EditorGUILayout.EndHorizontal();

            // 平面切换按钮
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("切换到XY平面 (2D)"))
            {
                script.planeOrientation = PlaneOrientation.XY;
                SceneView.RepaintAll();
            }
            if (GUILayout.Button("切换到XZ平面 (3D)"))
            {
                script.planeOrientation = PlaneOrientation.XZ;
                SceneView.RepaintAll();
            }
            EditorGUILayout.EndHorizontal();

            // 坐标系切换
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("立方体坐标"))
            {
                script.useCubeCoordinates = true;
                SceneView.RepaintAll();
            }
            if (GUILayout.Button("轴向坐标"))
            {
                script.useCubeCoordinates = false;
                SceneView.RepaintAll();
            }
            EditorGUILayout.EndHorizontal();
        }

        private int CalculateTotalHexes(int radius)
        {
            // 计算六边形网格中的总六边形数量公式
            return 1 + 3 * radius * (radius + 1);
        }
    }
#endif
}
