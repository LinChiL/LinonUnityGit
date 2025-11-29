using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 草地排斥体管理器 - 负责管理场景中影响草地变形的排斥体
/// </summary>
public class GrassRepellerManager : MonoBehaviour
{
    [Header("排斥体设置")]
    [Tooltip("手动指定的排斥体变换列表")]
    public List<Transform> repellers = new List<Transform>();

    [Tooltip("排斥体检测半径（未来扩展用）")]
    public float detectionRadius = 5f;

    [Tooltip("排斥体所在层级（未来扩展用）")]
    public LayerMask repellerLayer = -1;

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

    // Shader属性ID
    private static int RepellersProperty = Shader.PropertyToID("_Repellers");
    private static int RepellerCountProperty = Shader.PropertyToID("_RepellerCount");

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
        InitializeBuffer();
        _debugTimer = debugLogInterval;
    }

    void Update()
    {
        if (!Application.isPlaying) return;

        UpdateRepellerData();
        UpdateDebugInfo();
    }

    void OnDestroy()
    {
        ReleaseBuffer();
    }

    void OnDisable()
    {
        // 禁用时清理缓冲区以避免内存泄漏
        if (_repellerBuffer != null)
        {
            _repellerBuffer.Release();
            _repellerBuffer = null;
        }
    }

    /// <summary>
    /// 初始化ComputeBuffer
    /// </summary>
    private void InitializeBuffer()
    {
        // 清理空引用
        CleanNullRepellers();

        int maxRepellers = Mathf.Max(1, repellers.Count);

        // 创建ComputeBuffer，每个排斥体使用Vector4存储（xyz位置 + w半径）
        _repellerBuffer = new ComputeBuffer(maxRepellers, sizeof(float) * 4);
        _repellerPositions = new Vector4[maxRepellers];

        // 设置全局Shader属性
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

        // 收集有效的排斥体位置数据
        for (int i = 0; i < repellers.Count && i < _repellerPositions.Length; i++)
        {
            if (repellers[i] != null && repellers[i].gameObject.activeInHierarchy)
            {
                Vector3 position = repellers[i].position;
                _repellerPositions[validCount] = new Vector4(position.x, position.y, position.z, detectionRadius);
                validCount++;
            }
        }

        ActiveRepellerCount = validCount;

        // 更新ComputeBuffer数据
        if (validCount > 0)
        {
            _repellerBuffer.SetData(_repellerPositions, 0, 0, validCount);
        }

        // 更新Shader中的排斥体数量
        Shader.SetGlobalInt(RepellerCountProperty, validCount);
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
            if (Application.isPlaying)
            {
                Debug.Log($"[GrassRepellerManager] 活跃排斥体: {ActiveRepellerCount}/{repellers.Count}, " +
                         $"Buffer状态: {(_repellerBuffer != null ? "有效" : "无效")}");
            }
            _debugTimer = debugLogInterval;
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
    /// 编辑器可视化
    /// </summary>
    void OnDrawGizmosSelected()
    {
        if (!enableDebug) return;

        // 绘制管理器位置
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, 0.5f);

        // 绘制排斥体连线
        Gizmos.color = Color.yellow;
        foreach (var repeller in repellers)
        {
            if (repeller != null)
            {
                Gizmos.DrawLine(transform.position, repeller.position);

                // 绘制排斥体影响范围
                Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f);
                Gizmos.DrawWireSphere(repeller.position, detectionRadius);
                Gizmos.color = Color.yellow;
            }
        }

        // 显示信息文本
        UnityEditor.Handles.Label(transform.position + Vector3.up,
            $"Grass Repeller Manager\nActive: {ActiveRepellerCount}");
    }
#endif
}