using RootMotion.FinalIK;
using UnityEngine;

public class SmoothFootIKSetup : MonoBehaviour
{
    [Header("Final IK References")]
    public FullBodyBipedIK ik;

    [Header("Foot Targets (不随动画移动的参考点)")]
    public Transform leftFootTarget;  // 左脚稳定参考目标
    public Transform rightFootTarget; // 右脚稳定参考目标

    [Header("Foot IK Settings")]
    public float footPositionWeight = 1f;
    public float footRotationWeight = 1f;
    public LayerMask groundLayer;      // 手动指定地面层（避免默认层错误）
    public float footOffset = 0.05f;   // 脚离地面的偏移量
    public float raycastDistance = 1f; // 射线检测距离

    [Header("Smoothing Settings")]
    public float positionSmoothTime = 0.1f;
    public float rotationSmoothTime = 0.1f;
    public float weightSmoothTime = 0.2f; // 权重平滑过渡时间

    [Header("Debug")]
    public bool showDebugRays = true;
    public bool showTargetGizmos = true;

    // 平滑过渡用变量
    private Vector3 leftFootVelocity;
    private Vector3 rightFootVelocity;
    private Quaternion leftFootRotVelocity; // 改用Quaternion.Slerp平滑旋转（更自然）
    private Quaternion rightFootRotVelocity;
    private float leftFootPosWeightVelocity;
    private float leftFootRotWeightVelocity;
    private float rightFootPosWeightVelocity;
    private float rightFootRotWeightVelocity;

    // 目标位置和旋转缓存
    private Vector3 leftFootTargetPos;
    private Vector3 rightFootTargetPos;
    private Quaternion leftFootTargetRot;
    private Quaternion rightFootTargetRot;

    private void Start()
    {
        // 初始化IK组件
        if (ik == null)
            ik = GetComponent<FullBodyBipedIK>();

        if (ik == null)
        {
            Debug.LogError("[" + name + "] 未找到 FullBodyBipedIK 组件！", this);
            enabled = false;
            return;
        }

        // 检查目标是否赋值
        if (leftFootTarget == null || rightFootTarget == null)
        {
            Debug.LogError("[" + name + "] 请为左右脚分配 FootTarget！", this);
            enabled = false;
            return;
        }

        // 初始化目标位置和旋转（基于FootTarget的初始状态）
        leftFootTargetPos = leftFootTarget.position;
        leftFootTargetRot = leftFootTarget.rotation; // 修复原代码的赋值错误
        rightFootTargetPos = rightFootTarget.position;
        rightFootTargetRot = rightFootTarget.rotation;

        // 初始化IK权重
        ik.solver.leftFootEffector.positionWeight = 0f;
        ik.solver.leftFootEffector.rotationWeight = 0f;
        ik.solver.rightFootEffector.positionWeight = 0f;
        ik.solver.rightFootEffector.rotationWeight = 0f;
    }

    private void LateUpdate()
    {
        if (ik == null || !ik.enabled) return;

        ApplyFootIK();
    }

    private void ApplyFootIK()
    {
        // 处理左右脚IK（传入对应的稳定目标）
        ProcessFootIK(
            ik.solver.leftFootEffector,
            leftFootTarget,
            ref leftFootTargetPos,
            ref leftFootTargetRot,
            ref leftFootVelocity,
            ref leftFootRotVelocity,
            ref leftFootPosWeightVelocity,
            ref leftFootRotWeightVelocity
        );

        ProcessFootIK(
            ik.solver.rightFootEffector,
            rightFootTarget,
            ref rightFootTargetPos,
            ref rightFootTargetRot,
            ref rightFootVelocity,
            ref rightFootRotVelocity,
            ref rightFootPosWeightVelocity,
            ref rightFootRotWeightVelocity
        );
    }

    /// <summary>
    /// 处理单只脚的IK计算（核心逻辑）
    /// </summary>
    /// <param name="effector">IK effector</param>
    /// <param name="stableTarget">不随动画移动的稳定参考目标</param>
    /// <param name="targetPos">目标位置缓存</param>
    /// <param name="targetRot">目标旋转缓存</param>
    /// <param name="posVelocity">位置平滑速度</param>
    /// <param name="rotVelocity">旋转平滑速度</param>
    /// <param name="posWeightVelocity">位置权重平滑速度</param>
    /// <param name="rotWeightVelocity">旋转权重平滑速度</param>
    private void ProcessFootIK(
        IKEffector effector,
        Transform stableTarget,
        ref Vector3 targetPos,
        ref Quaternion targetRot,
        ref Vector3 posVelocity,
        ref Quaternion rotVelocity,
        ref float posWeightVelocity,
        ref float rotWeightVelocity
    )
    {
        // 1. 基于稳定目标发射射线（核心：射线起点不随动画移动）
        Vector3 rayStart = stableTarget.position + Vector3.up * 0.3f; // 射线起点在目标上方（避免穿模）
        Ray ray = new Ray(rayStart, Vector3.down);

        if (Physics.Raycast(ray, out RaycastHit hit, raycastDistance, groundLayer, QueryTriggerInteraction.Ignore))
        {
            // 2. 计算目标位置（地面点 + 偏移量，XZ轴锁定到稳定目标位置）
            Vector3 newTargetPos = hit.point;
            newTargetPos.y += footOffset;
            newTargetPos.x = stableTarget.position.x; // X轴跟随稳定目标（不随动画偏移）
            newTargetPos.z = stableTarget.position.z; // Z轴跟随稳定目标（不随动画偏移）

            // 3. 计算目标旋转（地面法线 + 稳定目标的Y轴旋转，避免脚部扭曲）
            Quaternion normalRot = Quaternion.FromToRotation(Vector3.up, hit.normal); // 贴合地面法线
            Quaternion yawRot = Quaternion.Euler(0f, stableTarget.rotation.eulerAngles.y, 0f); // 保持目标的Y轴朝向
            Quaternion newTargetRot = yawRot * normalRot; // 组合旋转（先贴合地面，再保持朝向）

            // 4. 平滑过渡（位置用SmoothDamp，旋转用Slerp更自然）
            targetPos = Vector3.SmoothDamp(targetPos, newTargetPos, ref posVelocity, positionSmoothTime);
            targetRot = Quaternion.Slerp(targetRot, newTargetRot, rotationSmoothTime * Time.deltaTime * 10f);
            // 或用带速度的平滑旋转（适合快速移动）：
            // targetRot = Quaternion.SmoothDamp(targetRot, newTargetRot, ref rotVelocity, rotationSmoothTime);

            // 5. 平滑激活IK权重
            float targetPosWeight = footPositionWeight;
            float targetRotWeight = footRotationWeight;
            effector.positionWeight = Mathf.SmoothDamp(effector.positionWeight, targetPosWeight, ref posWeightVelocity, weightSmoothTime);
            effector.rotationWeight = Mathf.SmoothDamp(effector.rotationWeight, targetRotWeight, ref rotWeightVelocity, weightSmoothTime);

            // 6. 应用IK目标
            effector.position = targetPos;
            effector.rotation = targetRot;

            // 调试绘制
            if (showDebugRays)
            {
                Debug.DrawRay(rayStart, Vector3.down * hit.distance, Color.green); // 命中地面（绿色）
                Debug.DrawLine(stableTarget.position, targetPos, Color.blue); // 稳定目标到IK目标（蓝色）
                Debug.DrawRay(targetPos, targetRot * Vector3.forward * 0.1f, Color.cyan); // 脚部朝向（青色）
            }
        }
        else
        {
            // 未检测到地面：继续更新目标位置（跟随稳定目标），保持IK权重
            Vector3 newTargetPos = stableTarget.position;
            newTargetPos.y += footOffset; // 保持偏移量，避免贴地

            // 旋转保持稳定目标的Y轴朝向
            Quaternion yawRot = Quaternion.Euler(0f, stableTarget.rotation.eulerAngles.y, 0f);
            Quaternion newTargetRot = yawRot * Quaternion.identity; // 保持默认向上的旋转

            // 平滑过渡目标位置和旋转
            targetPos = Vector3.SmoothDamp(targetPos, newTargetPos, ref posVelocity, positionSmoothTime);
            targetRot = Quaternion.Slerp(targetRot, newTargetRot, rotationSmoothTime * Time.deltaTime * 10f);

            // 保持IK权重（不禁用）
            effector.positionWeight = Mathf.SmoothDamp(effector.positionWeight, footPositionWeight, ref posWeightVelocity, weightSmoothTime);
            effector.rotationWeight = Mathf.SmoothDamp(effector.rotationWeight, footRotationWeight, ref rotWeightVelocity, weightSmoothTime);

            // 应用IK目标
            effector.position = targetPos;
            effector.rotation = targetRot;

            // 调试绘制
            if (showDebugRays)
            {
                Debug.DrawRay(rayStart, Vector3.down * raycastDistance, Color.red); // 未命中地面（红色）
                Debug.DrawLine(stableTarget.position, targetPos, Color.yellow); // 稳定目标到空中IK目标（橙色）
            }
        }
    }

    // Gizmos绘制稳定目标位置（场景视图可视化）
    private void OnDrawGizmosSelected()
    {
        if (!showTargetGizmos) return;

        if (leftFootTarget != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(leftFootTarget.position, 0.05f);
            Gizmos.DrawRay(leftFootTarget.position, leftFootTarget.forward * 0.15f);
        }

        if (rightFootTarget != null)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(rightFootTarget.position, 0.05f);
            Gizmos.DrawRay(rightFootTarget.position, rightFootTarget.forward * 0.15f);
        }
    }
}