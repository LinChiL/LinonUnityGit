using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 草地排斥体管理器 - 负责管理场景中影响草地变形的排斥体（带LOD优化）
/// </summary>
public class GrassRepellerManager : MonoBehaviour
{
    [Header("排斥体设置")]
    [Tooltip("手动指定的排斥体变换列表")]
    public List<Transform> repellers = new List<Transform>();

    [Tooltip("额外扩展半径（最终半径 = Collider尺寸 + 此值，无Collider时直接用此值）")]
    public float extraRadius = 1f;

    [Tooltip("排斥体体积乘法偏移量")]
    public float offsetMil = 1f;

    [Tooltip("排斥体所在层级（未来扩展用）")]
    public LayerMask repellerLayer = -1;

    [Header("LOD性能设置（逆向逻辑：只计算相机范围内的草）")]
    [Tooltip("手动指定用于LOD计算的主相机（优先使用，为空则自动查找MainCamera）")]
    public Camera lodCamera; // 新增：可手动指定的相机
    [Tooltip("LOD最大距离：超过此距离的草完全不执行排斥体变形（单位：米）")]
    public float lodMaxDistance = 50f; // 相机为中心的最大计算距离
    [Tooltip("LOD过渡距离：在LOD最大距离前的过渡区间，逐渐降低变形强度（单位：米）")]
    public float lodFadeDistance = 10f;

    [Header("调试设置")]
    [Tooltip("启用调试可视化")]
    public bool enableDebug = true;

    [Tooltip("调试信息输出间隔（秒）")]
    public float debugLogInterval = 2f;

    // ComputeBuffer相关变量
    private ComputeBuffer _repellerBuffer;
    private Vector4[] _repellerPositions;

    // 调试计时器
    private float _debugTimer;

    // Shader属性ID（新增LOD和相机位置相关）
    private static int RepellersProperty = Shader.PropertyToID("_Repellers");
    private static int RepellerCountProperty = Shader.PropertyToID("_RepellerCount");
    private static int LodMaxDistanceProperty = Shader.PropertyToID("_LodMaxDistance");
    private static int LodFadeDistanceProperty = Shader.PropertyToID("_LodFadeDistance");
    private static int CameraPositionProperty = Shader.PropertyToID("_GrassRepellerCameraPos");

    /// <summary>
    /// 当前激活的排斥体数量
    /// </summary>
    public int ActiveRepellerCount { get; private set; }

    /// <summary>
    /// 获取排斥体数据缓冲区
    /// </summary>
    public ComputeBuffer RepellerBuffer => _repellerBuffer;

    void Start()
    {
        // 自动查找相机（如果未手动指定）
        AutoFindCameraIfNotAssigned();

        InitializeBuffer();
        _debugTimer = debugLogInterval;

        // 初始化LOD相关Shader属性
        Shader.SetGlobalFloat(LodMaxDistanceProperty, lodMaxDistance);
        Shader.SetGlobalFloat(LodFadeDistanceProperty, lodFadeDistance);
    }

    void Update()
    {
        if (!Application.isPlaying) return;

        // 实时检查相机是否有效，无效则自动查找（容错处理）
        AutoFindCameraIfNotAssigned();

        UpdateRepellerData();
        UpdateDebugInfo();
        UpdateCameraPositionToShader(); // 实时更新相机位置到Shader（逆向LOD核心）
    }

    void OnDestroy()
    {
        ReleaseBuffer();
    }

    void OnDisable()
    {
        if (_repellerBuffer != null)
        {
            _repellerBuffer.Release();
            _repellerBuffer = null;
        }
    }

    /// <summary>
    /// 如果未手动指定相机，则自动查找MainCamera
    /// </summary>
    private void AutoFindCameraIfNotAssigned()
    {
        if (lodCamera == null)
        {
            lodCamera = Camera.main;
            if (lodCamera != null && enableDebug)
            {
                Debug.Log($"[GrassRepellerManager] 自动查找并使用相机: {lodCamera.name}");
            }
            else if (enableDebug)
            {
                Debug.LogWarning("[GrassRepellerManager] 未找到有效相机！LOD功能将无法正常工作，请手动指定相机。");
            }
        }
    }

    /// <summary>
    /// 初始化ComputeBuffer
    /// </summary>
    private void InitializeBuffer()
    {
        CleanNullRepellers();
        int maxRepellers = Mathf.Max(1, repellers.Count);

        _repellerBuffer = new ComputeBuffer(maxRepellers, sizeof(float) * 4);
        _repellerPositions = new Vector4[maxRepellers];

        Shader.SetGlobalBuffer(RepellersProperty, _repellerBuffer);
        Shader.SetGlobalInt(RepellerCountProperty, 0);
    }

    /// <summary>
    /// 更新排斥体数据
    /// </summary>
    private void UpdateRepellerData()
    {
        if (_repellerBuffer == null) return;

        CleanNullRepellers();
        int validCount = 0;

        for (int i = 0; i < repellers.Count && i < _repellerPositions.Length; i++)
        {
            if (repellers[i] != null && repellers[i].gameObject.activeInHierarchy)
            {
                Vector3 position = repellers[i].position;
                float finalRadius = CalculateRepellerFinalRadius(repellers[i]);

                // 调试模式下才打印，避免性能消耗
                if (enableDebug)
                {
                    Debug.Log($"排斥体 {repellers[i].name} 最终半径: {finalRadius} (Collider尺寸+extraRadius)");
                }

                _repellerPositions[validCount] = new Vector4(position.x, position.y, position.z, finalRadius);
                validCount++;
            }
        }

        ActiveRepellerCount = validCount;

        if (validCount > 0)
        {
            _repellerBuffer.SetData(_repellerPositions, 0, 0, validCount);
        }

        Shader.SetGlobalInt(RepellerCountProperty, validCount);
    }

    /// <summary>
    /// 计算排斥体的最终影响半径
    /// </summary>
    private float CalculateRepellerFinalRadius(Transform repellerTransform)
    {
        Collider collider = repellerTransform.GetComponent<Collider>();
        if (collider == null || !collider.enabled)
        {
            return extraRadius;
        }

        float colliderRadius = 0f;
        switch (collider)
        {
            case SphereCollider sphereCollider:
                colliderRadius = sphereCollider.radius * Mathf.Max(
                    repellerTransform.lossyScale.x,
                    repellerTransform.lossyScale.y,
                    repellerTransform.lossyScale.z
                );
                break;

            case CapsuleCollider capsuleCollider:
                float radius = capsuleCollider.radius * Mathf.Max(
                    repellerTransform.lossyScale.x,
                    repellerTransform.lossyScale.z
                );
                float halfHeight = capsuleCollider.height * 0.5f * repellerTransform.lossyScale.y;
                colliderRadius = Mathf.Max(radius, halfHeight);
                break;

            case BoxCollider boxCollider:
                Vector3 boxExtents = boxCollider.size * 0.5f;
                boxExtents.x *= repellerTransform.lossyScale.x;
                boxExtents.y *= repellerTransform.lossyScale.y;
                boxExtents.z *= repellerTransform.lossyScale.z;
                colliderRadius = Mathf.Sqrt(
                    boxExtents.x * boxExtents.x +
                    boxExtents.y * boxExtents.y +
                    boxExtents.z * boxExtents.z
                );
                break;

            default:
                colliderRadius = 0f;
                break;
        }

        return Mathf.Max(colliderRadius * offsetMil + extraRadius, 0.1f);
    }

    /// <summary>
    /// 清理列表中的空引用
    /// </summary>
    private void CleanNullRepellers()
    {
        for (int i = repellers.Count - 1; i >= 0; i--)
        {
            if (repellers[i] == null)
            {
                repellers.RemoveAt(i);
            }
        }
    }

    /// <summary>
    /// 更新调试信息
    /// </summary>
    private void UpdateDebugInfo()
    {
        if (!enableDebug) return;

        _debugTimer -= Time.deltaTime;
        if (_debugTimer <= 0f)
        {
            string cameraInfo = lodCamera != null ? lodCamera.name : "无有效相机";
            Debug.Log($"[GrassRepellerManager] 活跃排斥体: {ActiveRepellerCount}/{repellers.Count}, " +
                     $"Buffer状态: {(_repellerBuffer != null ? "有效" : "无效")}, " +
                     $"当前相机: {cameraInfo}, " +
                     $"LOD最大距离: {lodMaxDistance}m");
            _debugTimer = debugLogInterval;
        }
    }

    /// <summary>
    /// 实时更新相机位置到Shader（逆向LOD核心：用于计算草叶到相机的距离）
    /// </summary>
    private void UpdateCameraPositionToShader()
    {
        // 优先使用手动指定的相机
        if (lodCamera != null)
        {
            Shader.SetGlobalVector(CameraPositionProperty, lodCamera.transform.position);
        }
        else
        {
            // 容错：如果相机无效，使用管理器位置作为默认（避免Shader报错）
            Shader.SetGlobalVector(CameraPositionProperty, transform.position);
        }
    }

    /// <summary>
    /// 手动添加排斥体
    /// </summary>
    public void AddRepeller(Transform repeller)
    {
        if (repeller != null && !repellers.Contains(repeller))
        {
            repellers.Add(repeller);
        }
    }

    /// <summary>
    /// 手动移除排斥体
    /// </summary>
    public void RemoveRepeller(Transform repeller)
    {
        if (repeller != null)
        {
            repellers.Remove(repeller);
        }
    }

    /// <summary>
    /// 释放ComputeBuffer资源
    /// </summary>
    private void ReleaseBuffer()
    {
        if (_repellerBuffer != null)
        {
            _repellerBuffer.Release();
            _repellerBuffer = null;
        }
    }

#if UNITY_EDITOR
    /// <summary>
    /// 编辑器可视化（添加LOD范围显示）
    /// </summary>
    void OnDrawGizmosSelected()
    {
        if (!enableDebug) return;

        // 绘制管理器位置
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, 0.5f);

        // 绘制LOD范围（使用手动指定的相机位置）
        Vector3 lodCenter = transform.position; // 默认使用管理器位置
        if (lodCamera != null)
        {
            lodCenter = lodCamera.transform.position;
        }
        else
        {
            // 编辑器模式下如果未指定相机，尝试查找MainCamera预览
            Camera mainCam = Camera.main;
            if (mainCam != null)
            {
                lodCenter = mainCam.transform.position;
            }
        }

        if (lodMaxDistance > 0)
        {
            // LOD最大范围（半透明紫色）
            Gizmos.color = new Color(0.8f, 0.2f, 0.8f, 0.1f);
            Gizmos.DrawSphere(lodCenter, lodMaxDistance);
            Gizmos.color = new Color(0.8f, 0.2f, 0.8f, 0.3f);
            Gizmos.DrawWireSphere(lodCenter, lodMaxDistance);

            // LOD过渡范围（半透明粉色）
            if (lodFadeDistance > 0 && lodFadeDistance < lodMaxDistance)
            {
                Gizmos.color = new Color(1f, 0.4f, 0.6f, 0.1f);
                Gizmos.DrawSphere(lodCenter, lodMaxDistance - lodFadeDistance);
                Gizmos.color = new Color(1f, 0.4f, 0.6f, 0.3f);
                Gizmos.DrawWireSphere(lodCenter, lodMaxDistance - lodFadeDistance);
            }
        }

        // 绘制排斥体连线和最终影响范围
        Gizmos.color = Color.yellow;
        foreach (var repeller in repellers)
        {
            if (repeller != null)
            {
                Gizmos.DrawLine(transform.position, repeller.position);
                float finalRadius = CalculateRepellerFinalRadius(repeller);

                // 最终影响范围（半透明橙色）
                Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f);
                Gizmos.DrawWireSphere(repeller.position, finalRadius);
                // Collider原始范围（透明蓝色）
                DrawColliderGizmo(repeller, 0.1f);

                Gizmos.color = Color.yellow;
            }
        }

        // 显示信息文本（包含相机和LOD信息）
        string cameraName = lodCamera != null ? lodCamera.name : "未指定";
        UnityEditor.Handles.Label(transform.position + Vector3.up * 2,
            $"Grass Repeller Manager\n" +
            $"Active: {ActiveRepellerCount}\n" +
            $"Extra Radius: {extraRadius}\n" +
            $"当前相机: {cameraName}\n" +
            $"LOD Max: {lodMaxDistance}m\n" +
            $"LOD Fade: {lodFadeDistance}m");
    }

    /// <summary>
    /// 绘制排斥体的Collider原始范围
    /// </summary>
    private void DrawColliderGizmo(Transform repeller, float alpha)
    {
        Collider collider = repeller.GetComponent<Collider>();
        if (collider == null || !collider.enabled) return;

        Gizmos.color = new Color(0f, 0.5f, 1f, alpha);
        Matrix4x4 originalMatrix = Gizmos.matrix;

        // 应用排斥体的缩放和旋转
        Gizmos.matrix = Matrix4x4.TRS(
            repeller.position,
            repeller.rotation,
            repeller.lossyScale
        );

        switch (collider)
        {
            case SphereCollider sphere:
                Gizmos.DrawWireSphere(Vector3.zero, sphere.radius);
                break;
            case CapsuleCollider capsule:
                // 胶囊体Gizmo简化绘制
                float capsuleHalfHeight = capsule.height * 0.5f - capsule.radius;
                Gizmos.DrawWireSphere(Vector3.up * capsuleHalfHeight, capsule.radius);
                Gizmos.DrawWireSphere(Vector3.down * capsuleHalfHeight, capsule.radius);
                Gizmos.DrawLine(Vector3.up * (capsuleHalfHeight + capsule.radius),
                                Vector3.down * (capsuleHalfHeight + capsule.radius));
                break;
            case BoxCollider box:
                Gizmos.DrawWireCube(Vector3.zero, box.size);
                break;
        }

        Gizmos.matrix = originalMatrix;
    }
#endif
}