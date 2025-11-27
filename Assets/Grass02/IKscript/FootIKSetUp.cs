using RootMotion.FinalIK;
using UnityEngine;

[RequireComponent(typeof(LegIK))] // 强制依赖 LegIK 组件，避免漏挂
public class LegIK_GroundAdapt : MonoBehaviour
{
    private LegIK legIK; // 当前腿的 LegIK 组件引用
    private bool isDebugMode = true; // 是否启用调试日志（无需时可设为 false）

    [Header("核心配置（必设）")]
    public Transform footTarget; // IK目标（拖入之前创建的 ToeTarget_Left/Right 空对象）
    public LayerMask groundLayer; // 地面专属层级（仅勾选 Ground 层）
    public float legLength = 1.0f; // 角色腿长（骨盆到脚趾的距离，用于自动计算射线距离）

    [Header("检测参数（可微调）")]
    public float raycastDistanceMultiplier = 2.0f; // 射线距离 = 腿长 × 倍数（确保覆盖地面）
    public float footOffsetY = 0.05f; // 足部离地面的偏移（避免穿模）
    public float sphereCastRadius = 0.05f; // 球形射线半径（避免穿透薄地面）
    public bool useSphereCast = true; // 是否使用球形射线（比普通射线更稳定）

    [Header("平滑参数")]
    public float smoothSpeed = 15f; // Target 位置平滑过渡速度（15~20 最佳）
    public float rotationSmoothSpeed = 10f; // Target 旋转平滑过渡速度（贴合地面法线）

    // 调试用：存储射线信息，供 OnDrawGizmosSelected 绘制
    private Vector3 debugRayOrigin;
    private Vector3 debugRayDirection;
    private float debugRayDistance;
    private bool debugIsHitGround;

    private void Awake()
    {
        // 自动获取当前对象上的 LegIK 组件（无需手动赋值）
        legIK = GetComponent<LegIK>();

        // 初始化校验（避免关键参数未配置）
        InitialCheck();
    }

    private void LateUpdate()
    {
        // 若关键参数未配置，直接退出（避免报错）
        if (!IsConfigValid())
            return;

        // 1. 实时检测地面（存储射线信息供调试绘制）
        if (DetectGround(out Vector3 groundPoint, out Vector3 groundNormal))
        {
            // 2. 计算 Target 目标位置（XZ跟随角色，Y贴合地面）
            Vector3 targetPos = footTarget.position;
            targetPos.y = groundPoint.y + footOffsetY;

            // 3. 平滑更新 Target 位置
            footTarget.position = Vector3.Lerp(footTarget.position, targetPos, smoothSpeed * Time.deltaTime);

            // 4. 平滑更新 Target 旋转（贴合地面法线，避免脚部悬空）
            Quaternion targetRot = Quaternion.FromToRotation(Vector3.up, groundNormal) * footTarget.rotation;
            footTarget.rotation = Quaternion.Lerp(footTarget.rotation, targetRot, rotationSmoothSpeed * Time.deltaTime);

            // 调试日志：显示检测成功信息
            if (isDebugMode)
                Debug.Log($"[LegIK] 检测成功 | 地面高度：{groundPoint.y:F2} | Target当前Y：{footTarget.position.y:F2}", this);
        }
        else
        {
            // 调试日志：显示未检测到地面（帮助排查问题）
            if (isDebugMode)
                Debug.LogWarning($"[LegIK] 未检测到地面 | Target位置：{footTarget.position} | 射线距离：{GetRaycastDistance()}", this);
        }
    }

    /// <summary>
    /// 地面检测（支持普通射线/球形射线）
    /// </summary>
    /// <param name="groundPoint">地面碰撞点</param>
    /// <param name="groundNormal">地面法线（用于旋转Target）</param>
    /// <returns>是否检测到地面</returns>
    private bool DetectGround(out Vector3 groundPoint, out Vector3 groundNormal)
    {
        groundPoint = Vector3.zero;
        groundNormal = Vector3.up;

        // 射线起点：在 Target 上方 0.5f 处（避免 Target 贴地时漏检）
        debugRayOrigin = footTarget.position + Vector3.up * 0.5f;
        // 射线方向：正下方（重力方向）
        debugRayDirection = Vector3.down;
        // 射线距离：腿长 × 倍数（动态适配不同角色）
        debugRayDistance = GetRaycastDistance();

        if (useSphereCast)
        {
            // 球形射线（更稳定，不易穿透薄地面）
            if (Physics.SphereCast(debugRayOrigin, sphereCastRadius, debugRayDirection, out RaycastHit hit, debugRayDistance, groundLayer))
            {
                groundPoint = hit.point;
                groundNormal = hit.normal;
                debugIsHitGround = true;
                return true;
            }
        }
        else
        {
            // 普通射线（备用）
            if (Physics.Raycast(debugRayOrigin, debugRayDirection, out RaycastHit hit, debugRayDistance, groundLayer))
            {
                groundPoint = hit.point;
                groundNormal = hit.normal;
                debugIsHitGround = true;
                return true;
            }
        }

        // 未命中地面
        debugIsHitGround = false;
        return false;
    }

    /// <summary>
    /// 计算射线距离（腿长 × 倍数）
    /// </summary>
    private float GetRaycastDistance()
    {
        return legLength * raycastDistanceMultiplier;
    }

    /// <summary>
    /// 初始化校验（启动时检查关键参数）
    /// </summary>
    private void InitialCheck()
    {
        if (isDebugMode)
            Debug.Log($"[LegIK] 初始化 | 角色腿长：{legLength} | 射线距离：{GetRaycastDistance()}", this);

        // 检查 LegIK 组件是否存在
        if (legIK == null)
        {
            Debug.LogError("[LegIK] 未找到 LegIK 组件！请确保脚本挂载在 LegIK 所在对象上", this);
            enabled = false; // 禁用脚本，避免报错
        }
    }

    /// <summary>
    /// 配置有效性检查（每帧执行，避免空引用）
    /// </summary>
    private bool IsConfigValid()
    {
        // 检查 Target 是否赋值
        if (footTarget == null)
        {
            Debug.LogError("[LegIK] 错误：footTarget 未赋值！请拖入正确的 Target 空对象", this);
            return false;
        }

        // 检查地面层级是否配置
        if (groundLayer.value == 0)
        {
            Debug.LogError("[LegIK] 错误：groundLayer 未选择！请勾选地面专属层级（如 Ground）", this);
            return false;
        }

        // 检查腿长是否合理（避免射线距离过短）
        if (legLength <= 0.1f)
        {
            Debug.LogWarning("[LegIK] 警告：legLength 过小！请设置正确的角色腿长（如 1.0f）", this);
            return false;
        }

        return true;
    }

    /// <summary>
    /// 在Scene视图中绘制调试射线（选中脚本所在对象时可见）
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        // 若 Target 未赋值，不绘制
        if (footTarget == null)
            return;

        // 设置射线颜色：命中=绿色，未命中=红色
        Gizmos.color = debugIsHitGround ? Color.green : Color.red;

        // 绘制射线起点（球形射线绘制球，普通射线绘制点）
        if (useSphereCast)
        {
            Gizmos.DrawWireSphere(debugRayOrigin, sphereCastRadius);
        }
        else
        {
            Gizmos.DrawSphere(debugRayOrigin, 0.02f); // 绘制射线起点的小球
        }

        // 绘制射线主体
        Gizmos.DrawLine(debugRayOrigin, debugRayOrigin + debugRayDirection * debugRayDistance);

        // 若命中地面，绘制碰撞点和法线
        if (debugIsHitGround)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawSphere(debugRayOrigin + debugRayDirection * debugRayDistance, 0.03f); // 碰撞点
            Gizmos.DrawLine(debugRayOrigin + debugRayDirection * debugRayDistance,
                           debugRayOrigin + debugRayDirection * debugRayDistance + debugRayDirection * 0.2f); // 法线方向
        }
    }
}