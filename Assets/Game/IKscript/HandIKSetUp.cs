using RootMotion.FinalIK;
using UnityEngine;

public class HandIKController : MonoBehaviour
{
    [Header("Final IK References")]
    public FullBodyBipedIK ik;

    [Header("Hand Targets (手部支撑参考点)")]
    public Transform leftHandTarget;  // 左手支撑目标
    public Transform rightHandTarget; // 右手支撑目标
    public LayerMask wallLayer;       // 墙面/支撑物层
    public float handReachDistance = 0.8f; // 手部射线检测距离
    public float handOffset = 0.02f;   // 手部偏移量

    [Header("Hand IK Settings")]
    public float handPositionWeight = 1f;
    public float handRotationWeight = 0.5f;
    public float handWeightThreshold = 0.3f; // 触发手部IK的阈值
    public float handPositionSmoothTime = 0.1f;
    public float handRotationSmoothTime = 0.1f;

    [Header("Smoothing Settings")]
    public float weightSmoothTime = 0.2f; // 权重平滑过渡时间

    [Header("Debug")]
    public bool showDebugRays = true;
    public bool showTargetGizmos = true;

    // 平滑过渡用变量
    private Vector3 leftHandVelocity;
    private Vector3 rightHandVelocity;
    private Quaternion leftHandRotVelocity;
    private Quaternion rightHandRotVelocity;
    private float leftHandPosWeightVelocity;
    private float leftHandRotWeightVelocity;
    private float rightHandPosWeightVelocity;
    private float rightHandRotWeightVelocity;

    // 目标位置和旋转缓存
    private Vector3 leftHandTargetPos;
    private Vector3 rightHandTargetPos;
    private Quaternion leftHandTargetRot;
    private Quaternion rightHandTargetRot;

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

        // 初始化目标位置和旋转（如果提供了手部目标）
        if (leftHandTarget != null)
        {
            leftHandTargetPos = leftHandTarget.position;
            leftHandTargetRot = leftHandTarget.rotation;
        }
        else
        {
            // 如果没有手部目标，使用角色的初始手部位置
            leftHandTargetPos = ik.solver.leftHandEffector.bone.position;
            leftHandTargetRot = ik.solver.leftHandEffector.bone.rotation;
        }

        if (rightHandTarget != null)
        {
            rightHandTargetPos = rightHandTarget.position;
            rightHandTargetRot = rightHandTarget.rotation;
        }
        else
        {
            // 如果没有手部目标，使用角色的初始手部位置
            rightHandTargetPos = ik.solver.rightHandEffector.bone.position;
            rightHandTargetRot = ik.solver.rightHandEffector.bone.rotation;
        }

        // 初始化IK权重
        ik.solver.leftHandEffector.positionWeight = 0f;
        ik.solver.leftHandEffector.rotationWeight = 0f;
        ik.solver.rightHandEffector.positionWeight = 0f;
        ik.solver.rightHandEffector.rotationWeight = 0f;
    }

    private void LateUpdate()
    {
        if (ik == null || !ik.enabled) return;

        ApplyHandIK();
    }

    private void ApplyHandIK()
    {
        // 处理左右手IK（用于扶墙）
        ProcessHandIK(
            ik.solver.leftHandEffector,
            leftHandTarget,
            ref leftHandTargetPos,
            ref leftHandTargetRot,
            ref leftHandVelocity,
            ref leftHandRotVelocity,
            ref leftHandPosWeightVelocity,
            ref leftHandRotWeightVelocity
        );

        ProcessHandIK(
            ik.solver.rightHandEffector,
            rightHandTarget,
            ref rightHandTargetPos,
            ref rightHandTargetRot,
            ref rightHandVelocity,
            ref rightHandRotVelocity,
            ref rightHandPosWeightVelocity,
            ref rightHandRotWeightVelocity
        );
    }

    /// <summary>
    /// 处理单只手的IK计算（用于扶墙）
    /// </summary>
    private void ProcessHandIK(
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
        // 计算手部到稳定目标的距离
        Vector3 handPosition = effector.bone.position;
        Vector3 toTarget = stableTarget.position - handPosition;

        // 从手部位置向稳定目标方向发射射线
        Vector3 rayStart = handPosition;
        Vector3 rayDirection = toTarget.normalized;
        float rayDistance = Mathf.Min(toTarget.magnitude, handReachDistance);

        Ray ray = new Ray(rayStart, rayDirection);

        if (Physics.Raycast(ray, out RaycastHit hit, rayDistance, wallLayer, QueryTriggerInteraction.Ignore))
        {
            // 检测到墙面/支撑物
            Vector3 newTargetPos = hit.point + hit.normal * handOffset;

            // 计算目标旋转（使手掌朝向墙面法线）
            Quaternion normalRot = Quaternion.FromToRotation(Vector3.forward, -hit.normal);
            Quaternion newTargetRot = normalRot * stableTarget.rotation;

            // 平滑过渡目标位置和旋转
            targetPos = Vector3.SmoothDamp(targetPos, newTargetPos, ref posVelocity, handPositionSmoothTime);
            targetRot = Quaternion.Slerp(targetRot, newTargetRot, handRotationSmoothTime * Time.deltaTime * 10f);

            // 根据距离调整权重（距离越近，权重越高）
            float distance = Vector3.Distance(handPosition, hit.point);
            float activationWeight = Mathf.InverseLerp(handReachDistance, handWeightThreshold, distance);

            // 平滑激活IK权重
            float targetPosWeight = handPositionWeight * activationWeight;
            float targetRotWeight = handRotationWeight * activationWeight;

            effector.positionWeight = Mathf.SmoothDamp(effector.positionWeight, targetPosWeight, ref posWeightVelocity, weightSmoothTime);
            effector.rotationWeight = Mathf.SmoothDamp(effector.rotationWeight, targetRotWeight, ref rotWeightVelocity, weightSmoothTime);

            // 应用IK目标
            effector.position = targetPos;
            effector.rotation = targetRot;

            // 调试绘制
            if (showDebugRays)
            {
                Debug.DrawRay(rayStart, rayDirection * hit.distance, Color.cyan); // 命中墙面（青色）
                Debug.DrawLine(stableTarget.position, targetPos, Color.green); // 稳定目标到IK目标（绿色）
            }
        }
        else
        {
            // 未检测到支撑物：逐渐降低权重
            effector.positionWeight = Mathf.SmoothDamp(effector.positionWeight, 0f, ref posWeightVelocity, weightSmoothTime * 2f);
            effector.rotationWeight = Mathf.SmoothDamp(effector.rotationWeight, 0f, ref rotWeightVelocity, weightSmoothTime * 2f);

            // 调试绘制
            if (showDebugRays)
            {
                Debug.DrawRay(rayStart, rayDirection * rayDistance, Color.red); // 未命中（红色）
            }
        }
    }

    // Gizmos绘制稳定目标位置（场景视图可视化）
    private void OnDrawGizmosSelected()
    {
        if (!showTargetGizmos) return;

        // 绘制手部目标
        if (leftHandTarget != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(leftHandTarget.position, Vector3.one * 0.08f);
            Gizmos.DrawRay(leftHandTarget.position, leftHandTarget.forward * 0.1f);
        }

        if (rightHandTarget != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireCube(rightHandTarget.position, Vector3.one * 0.08f);
            Gizmos.DrawRay(rightHandTarget.position, rightHandTarget.forward * 0.1f);
        }
    }
}