using RootMotion.FinalIK;
using UnityEngine;

public class FullBodyIKWithWallSupport : MonoBehaviour
{
    [Header("Final IK References")]
    public FullBodyBipedIK ik;

    [Header("Foot Targets (不随动画移动的参考点)")]
    public Transform leftFootTarget;
    public Transform rightFootTarget;

    [Header("Hand Targets (手部支撑参考点)")]
    public Transform leftHandTarget;
    public Transform rightHandTarget;
    public LayerMask wallLayer;
    public float handReachDistance = 0.8f;
    public float handOffset = 0.02f;

    [Header("Foot IK Settings")]
    public float footPositionWeight = 1f;
    public float footRotationWeight = 1f;
    public LayerMask groundLayer;
    public float footOffset = 0.05f;
    public float raycastDistance = 1f;

    [Header("Hand IK Settings")]
    public float handPositionWeight = 1f;
    public float handRotationWeight = 0.5f;
    public float handWeightThreshold = 0.3f;
    public float handPositionSmoothTime = 0.1f;
    public float handRotationSmoothTime = 0.1f;

    [Header("Hand Weight Transition Settings")]
    public float handEnterSmoothTime = 0.1f; // 进入时的平滑时间
    public float handExitSmoothTime = 0.05f;  // 退出时的平滑时间（更快）

    [Header("Smoothing Settings")]
    public float positionSmoothTime = 0.1f;
    public float rotationSmoothTime = 0.1f;
    public float weightSmoothTime = 0.2f;

    [Header("移动检测设置")]
    public float movementThreshold = 0.01f; // 移动阈值
    public float idleDetectionDelay = 1.0f; // 停止移动后启用IK的延迟时间
    public Transform characterRoot; // 角色根节点，用于检测移动

    [Header("Debug")]
    public bool showDebugRays = true;
    public bool showTargetGizmos = true;

    // 移动检测相关变量
    private Vector3 previousPosition;
    private float idleTimer = 0f;
    private bool isIdle = false;

    // 手部IK状态跟踪相关变量
    private CharacterStateManager charactorState;

    // 平滑过渡用变量 - Effector 权重
    private Vector3 leftFootVelocity;
    private Vector3 rightFootVelocity;
    private Vector3 leftHandVelocity;
    private Vector3 rightHandVelocity;
    private Quaternion leftFootRotVelocity;
    private Quaternion rightFootRotVelocity;
    private Quaternion leftHandRotVelocity;
    private Quaternion rightHandRotVelocity;
    private float leftFootPosWeightVelocity;
    private float leftFootRotWeightVelocity;
    private float rightFootPosWeightVelocity;
    private float rightFootRotWeightVelocity;
    private float leftHandPosWeightVelocity;
    private float leftHandRotWeightVelocity;
    private float rightHandPosWeightVelocity;
    private float rightHandRotWeightVelocity;

    // 平滑过渡用变量 - Mapping Maintain 权重
    private float leftHandMaintainPosVel;
    private float leftHandMaintainRotVel;
    private float rightHandMaintainPosVel;
    private float rightHandMaintainRotVel;

    // 目标位置和旋转缓存
    private Vector3 leftFootTargetPos;
    private Vector3 rightFootTargetPos;
    private Vector3 leftHandTargetPos;
    private Vector3 rightHandTargetPos;
    private Quaternion leftFootTargetRot;
    private Quaternion rightFootTargetRot;
    private Quaternion leftHandTargetRot;
    private Quaternion rightHandTargetRot;

    // 手部IK状态跟踪
    private bool leftHandInContact = false;
    private bool rightHandInContact = false;

    // 权重延迟相关变量
    private bool leftHandIKEnabled = false;
    private bool rightHandIKEnabled = false;
    private float leftHandIKEnableTimer = 0f;
    private float rightHandIKEnableTimer = 0f;
    private bool leftHandIKEnablePending = false;
    private bool rightHandIKEnablePending = false;

    private void Start()
    {
        if (ik == null)
            ik = GetComponent<FullBodyBipedIK>();

        if (ik == null)
        {
            Debug.LogError("[" + name + "] 未找到 FullBodyBipedIK 组件！", this);
            enabled = false;
            return;
        }

        if (leftFootTarget == null || rightFootTarget == null)
        {
            Debug.LogError("[" + name + "] 请为左右脚分配 FootTarget！", this);
            enabled = false;
            return;
        }

        if (characterRoot == null)
        {
            characterRoot = transform; // 如果没有指定根节点，则使用当前对象
        }

        previousPosition = characterRoot.position;

        leftFootTargetPos = leftFootTarget.position;
        leftFootTargetRot = leftFootTarget.rotation;
        rightFootTargetPos = rightFootTarget.position;
        rightFootTargetRot = rightFootTarget.rotation;

        if (leftHandTarget != null)
        {
            leftHandTargetPos = leftHandTarget.position;
            leftHandTargetRot = leftHandTarget.rotation;
        }
        else
        {
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
            rightHandTargetPos = ik.solver.rightHandEffector.bone.position;
            rightHandTargetRot = ik.solver.rightHandEffector.bone.rotation;
        }

        // 初始化 Effector 权重
        var leftFoot = ik.solver.leftFootEffector;
        var rightFoot = ik.solver.rightFootEffector;
        var leftHand = ik.solver.leftHandEffector;
        var rightHand = ik.solver.rightHandEffector;

        leftFoot.positionWeight = 0f; leftFoot.rotationWeight = 0f;
        rightFoot.positionWeight = 0f; rightFoot.rotationWeight = 0f;
        leftHand.positionWeight = 0f; leftHand.rotationWeight = 0f;
        rightHand.positionWeight = 0f; rightHand.rotationWeight = 0f;

        // 初始化 Mapping Maintain 权重（完全跟随动画）
        if (ik.solver.leftArmMapping != null)
        {
            ik.solver.leftArmMapping.weight = 1f;
            ik.solver.leftArmMapping.maintainRotationWeight = 1f;
        }
        if (ik.solver.rightArmMapping != null)
        {
            ik.solver.rightArmMapping.weight = 1f;
            ik.solver.rightArmMapping.maintainRotationWeight = 1f;
        }

        charactorState = GetComponent<CharacterStateManager>();
    }

    private void LateUpdate()
    {
        if (ik == null || !ik.enabled) return;

        UpdateMovementDetection();

        ApplyFootIK();
        ApplyHandIK();
    }

    private void UpdateMovementDetection()
    {
        // 检测角色是否移动
        Vector3 currentPosition = characterRoot.position;
        float distance = Vector3.Distance(previousPosition, currentPosition);

        if (distance > movementThreshold)
        {
            // 角色正在移动
            isIdle = false;
            idleTimer = 0f;

            // 重置手部IK启用状态
            if (leftHandIKEnabled)
            {
                leftHandIKEnabled = false;
                leftHandIKEnableTimer = 0f;
                leftHandIKEnablePending = false;
            }
            if (rightHandIKEnabled)
            {
                rightHandIKEnabled = false;
                rightHandIKEnableTimer = 0f;
                rightHandIKEnablePending = false;
            }
        }
        else
        {
            // 角色静止，开始计时
            if (!isIdle)
            {
                idleTimer += Time.deltaTime;
                if (idleTimer >= idleDetectionDelay)
                {
                    isIdle = true;
                }
            }
        }

        previousPosition = currentPosition;
    }

    private void ApplyFootIK()
    {
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

    private void ApplyHandIK()
    {
        ProcessHandIK(
            ik.solver.leftHandEffector,
            leftHandTarget,
            ref leftHandTargetPos,
            ref leftHandTargetRot,
            ref leftHandVelocity,
            ref leftHandRotVelocity,
            ref leftHandPosWeightVelocity,
            ref leftHandRotWeightVelocity,
            ref leftHandInContact,
            isLeft: true
        );

        ProcessHandIK(
            ik.solver.rightHandEffector,
            rightHandTarget,
            ref rightHandTargetPos,
            ref rightHandTargetRot,
            ref rightHandVelocity,
            ref rightHandRotVelocity,
            ref rightHandPosWeightVelocity,
            ref rightHandRotWeightVelocity,
            ref rightHandInContact,
            isLeft: false
        );
    }

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
        Vector3 rayStart = stableTarget.position + Vector3.up * 0.3f;
        Ray ray = new Ray(rayStart, Vector3.down);

        if (Physics.Raycast(ray, out RaycastHit hit, raycastDistance, groundLayer, QueryTriggerInteraction.Ignore))
        {
            Vector3 newTargetPos = hit.point;
            newTargetPos.y += footOffset;
            newTargetPos.x = stableTarget.position.x;
            newTargetPos.z = stableTarget.position.z;

            Quaternion normalRot = Quaternion.FromToRotation(Vector3.up, hit.normal);
            Quaternion yawRot = Quaternion.Euler(0f, stableTarget.rotation.eulerAngles.y, 0f);
            Quaternion newTargetRot = yawRot * normalRot;

            targetPos = Vector3.SmoothDamp(targetPos, newTargetPos, ref posVelocity, positionSmoothTime);
            targetRot = Quaternion.Slerp(targetRot, newTargetRot, rotationSmoothTime * Time.deltaTime * 10f);

            effector.positionWeight = Mathf.SmoothDamp(effector.positionWeight, footPositionWeight, ref posWeightVelocity, weightSmoothTime);
            effector.rotationWeight = Mathf.SmoothDamp(effector.rotationWeight, footRotationWeight, ref rotWeightVelocity, weightSmoothTime);

            effector.position = targetPos;
            effector.rotation = targetRot;

            if (showDebugRays)
            {
                Debug.DrawRay(rayStart, Vector3.down * hit.distance, Color.green);
                Debug.DrawLine(stableTarget.position, targetPos, Color.blue);
                Debug.DrawRay(targetPos, targetRot * Vector3.forward * 0.1f, Color.cyan);
            }
        }
        else
        {
            Vector3 newTargetPos = stableTarget.position;
            newTargetPos.y += footOffset;

            Quaternion yawRot = Quaternion.Euler(0f, stableTarget.rotation.eulerAngles.y, 0f);
            Quaternion newTargetRot = yawRot;

            targetPos = Vector3.SmoothDamp(targetPos, newTargetPos, ref posVelocity, positionSmoothTime);
            targetRot = Quaternion.Slerp(targetRot, newTargetRot, rotationSmoothTime * Time.deltaTime * 10f);

            effector.positionWeight = Mathf.SmoothDamp(effector.positionWeight, footPositionWeight, ref posWeightVelocity, weightSmoothTime);
            effector.rotationWeight = Mathf.SmoothDamp(effector.rotationWeight, footRotationWeight, ref rotWeightVelocity, weightSmoothTime);

            effector.position = targetPos;
            effector.rotation = targetRot;

            if (showDebugRays)
            {
                Debug.DrawRay(rayStart, Vector3.down * raycastDistance, Color.red);
                Debug.DrawLine(stableTarget.position, targetPos, Color.yellow);
            }
        }
    }

    private void ProcessHandIK(
        IKEffector effector,
        Transform stableTarget,
        ref Vector3 targetPos,
        ref Quaternion targetRot,
        ref Vector3 posVelocity,
        ref Quaternion rotVelocity,
        ref float posWeightVelocity,
        ref float rotWeightVelocity,
        ref bool inContact,
        bool isLeft
    )
    {
        Vector3 handPosition = effector.bone.position;
        Vector3 toTarget = stableTarget.position - handPosition;

        Vector3 rayStart = handPosition;
        Vector3 rayDirection = toTarget.normalized;
        float rayDistance = Mathf.Min(toTarget.magnitude, handReachDistance);

        Ray ray = new Ray(rayStart, rayDirection);

        IKMappingLimb limbMapping = isLeft ? ik.solver.leftArmMapping : ik.solver.rightArmMapping;
        if (limbMapping == null) return;

        ref float maintainPosVel = ref (isLeft ? ref leftHandMaintainPosVel : ref rightHandMaintainPosVel);
        ref float maintainRotVel = ref (isLeft ? ref leftHandMaintainRotVel : ref rightHandMaintainRotVel);

        // 检查角色是否静止且达到延迟时间
        //新增检测搬运
        bool canUseHandIK = isIdle && charactorState.currentState == CharacterStateManager.CharacterState.Normal;

        // 更新目标位置（即使IK未启用也更新位置，避免下次切换时的跳跃）
        if (Physics.Raycast(ray, out RaycastHit hit, rayDistance, wallLayer, QueryTriggerInteraction.Ignore))
        {
            Vector3 newTargetPos = hit.point + hit.normal * handOffset;
            Quaternion normalRot = Quaternion.FromToRotation(Vector3.forward, -hit.normal);
            Quaternion newTargetRot = normalRot * stableTarget.rotation;

            // 无论是否启用IK，都平滑更新目标位置和旋转
            targetPos = Vector3.SmoothDamp(targetPos, newTargetPos, ref posVelocity, handPositionSmoothTime);
            targetRot = Quaternion.Slerp(targetRot, newTargetRot, handRotationSmoothTime * Time.deltaTime * 10f);

            float distance = Vector3.Distance(handPosition, hit.point);
            float activationWeight = Mathf.InverseLerp(handReachDistance, handWeightThreshold, distance);

            // 更新接触状态
            if (!inContact && activationWeight > 0.01f) // 刚开始接触
            {
                inContact = true;
            }

            if (showDebugRays)
            {
                Debug.DrawRay(rayStart, rayDirection * hit.distance, Color.cyan);
                Debug.DrawLine(stableTarget.position, targetPos, Color.green);
            }

            // 检查是否可以启用IK（角色静止且满足延迟时间）
            if (canUseHandIK)
            {
                if (isLeft)
                {
                    if (!leftHandIKEnabled)
                    {
                        if (!leftHandIKEnablePending)
                        {
                            leftHandIKEnablePending = true;
                            leftHandIKEnableTimer = 0f;
                        }

                        leftHandIKEnableTimer += Time.deltaTime;
                        if (leftHandIKEnableTimer >= idleDetectionDelay)
                        {
                            leftHandIKEnabled = true;
                            leftHandIKEnablePending = false;
                        }
                    }
                }
                else
                {
                    if (!rightHandIKEnabled)
                    {
                        if (!rightHandIKEnablePending)
                        {
                            rightHandIKEnablePending = true;
                            rightHandIKEnableTimer = 0f;
                        }

                        rightHandIKEnableTimer += Time.deltaTime;
                        if (rightHandIKEnableTimer >= idleDetectionDelay)
                        {
                            rightHandIKEnabled = true;
                            rightHandIKEnablePending = false;
                        }
                    }
                }

                // 应用权重
                bool ikEnabled = isLeft ? leftHandIKEnabled : rightHandIKEnabled;

                if (ikEnabled)
                {
                    // Effector 权重：靠近墙时高（正常）
                    float targetPosWeight = handPositionWeight * activationWeight;
                    float targetRotWeight = handRotationWeight * activationWeight;

                    // 使用不同的平滑时间：进入时使用handEnterSmoothTime，退出时使用handExitSmoothTime
                    float currentSmoothTime = inContact ? handEnterSmoothTime : handExitSmoothTime;

                    effector.positionWeight = Mathf.SmoothDamp(effector.positionWeight, targetPosWeight, ref posWeightVelocity, currentSmoothTime);
                    effector.rotationWeight = Mathf.SmoothDamp(effector.rotationWeight, targetRotWeight, ref rotWeightVelocity, currentSmoothTime);

                    // ✅ 按你的要求：mapping.weight 靠近墙 = 1，远离 = 0
                    float mappingTargetWeight = activationWeight; // 注意：这会削弱 IK 效果！
                    limbMapping.weight = Mathf.SmoothDamp(limbMapping.weight, mappingTargetWeight, ref maintainPosVel, currentSmoothTime);
                    limbMapping.maintainRotationWeight = Mathf.SmoothDamp(limbMapping.maintainRotationWeight, mappingTargetWeight, ref maintainRotVel, currentSmoothTime);

                    effector.position = targetPos;
                    effector.rotation = targetRot;
                }
                else
                {
                    // IK未启用，权重为0
                    effector.positionWeight = Mathf.SmoothDamp(effector.positionWeight, 0f, ref posWeightVelocity, handExitSmoothTime);
                    effector.rotationWeight = Mathf.SmoothDamp(effector.rotationWeight, 0f, ref rotWeightVelocity, handExitSmoothTime);

                    // ✅ mapping.weight = 0 当远离墙面
                    limbMapping.weight = Mathf.SmoothDamp(limbMapping.weight, 0f, ref maintainPosVel, handExitSmoothTime);
                    limbMapping.maintainRotationWeight = Mathf.SmoothDamp(limbMapping.maintainRotationWeight, 0f, ref maintainRotVel, handExitSmoothTime);
                }
            }
            else
            {
                // 角色不在静止状态，重置启用状态
                if (isLeft)
                {
                    leftHandIKEnabled = false;
                    leftHandIKEnableTimer = 0f;
                    leftHandIKEnablePending = false;
                }
                else
                {
                    rightHandIKEnabled = false;
                    rightHandIKEnableTimer = 0f;
                    rightHandIKEnablePending = false;
                }

                // 权重为0
                effector.positionWeight = Mathf.SmoothDamp(effector.positionWeight, 0f, ref posWeightVelocity, handExitSmoothTime);
                effector.rotationWeight = Mathf.SmoothDamp(effector.rotationWeight, 0f, ref rotWeightVelocity, handExitSmoothTime);

                // ✅ mapping.weight = 0 当远离墙面
                limbMapping.weight = Mathf.SmoothDamp(limbMapping.weight, 0f, ref maintainPosVel, handExitSmoothTime);
                limbMapping.maintainRotationWeight = Mathf.SmoothDamp(limbMapping.maintainRotationWeight, 0f, ref maintainRotVel, handExitSmoothTime);
            }
        }
        else
        {
            // 未命中墙面：重置接触状态
            inContact = false;

            // 更新目标位置为手部当前位置，避免跳跃
            targetPos = Vector3.SmoothDamp(targetPos, handPosition, ref posVelocity, handPositionSmoothTime);
            targetRot = Quaternion.Slerp(targetRot, effector.bone.rotation, handRotationSmoothTime * Time.deltaTime * 10f);

            // 更新权重
            if (canUseHandIK)
            {
                if (isLeft)
                {
                    if (!leftHandIKEnabled)
                    {
                        if (!leftHandIKEnablePending)
                        {
                            leftHandIKEnablePending = true;
                            leftHandIKEnableTimer = 0f;
                        }

                        leftHandIKEnableTimer += Time.deltaTime;
                        if (leftHandIKEnableTimer >= idleDetectionDelay)
                        {
                            leftHandIKEnabled = true;
                            leftHandIKEnablePending = false;
                        }
                    }
                }
                else
                {
                    if (!rightHandIKEnabled)
                    {
                        if (!rightHandIKEnablePending)
                        {
                            rightHandIKEnablePending = true;
                            rightHandIKEnableTimer = 0f;
                        }

                        rightHandIKEnableTimer += Time.deltaTime;
                        if (rightHandIKEnableTimer >= idleDetectionDelay)
                        {
                            rightHandIKEnabled = true;
                            rightHandIKEnablePending = false;
                        }
                    }
                }

                // 如果IK已启用，则应用0权重
                bool ikEnabled = isLeft ? leftHandIKEnabled : rightHandIKEnabled;

                if (ikEnabled)
                {
                    effector.positionWeight = Mathf.SmoothDamp(effector.positionWeight, 0f, ref posWeightVelocity, handExitSmoothTime);
                    effector.rotationWeight = Mathf.SmoothDamp(effector.rotationWeight, 0f, ref rotWeightVelocity, handExitSmoothTime);

                    // ✅ mapping.weight = 0 当远离墙面
                    limbMapping.weight = Mathf.SmoothDamp(limbMapping.weight, 0f, ref maintainPosVel, handExitSmoothTime);
                    limbMapping.maintainRotationWeight = Mathf.SmoothDamp(limbMapping.maintainRotationWeight, 0f, ref maintainRotVel, handExitSmoothTime);
                }
            }
            else
            {
                // 角色不在静止状态，重置启用状态
                if (isLeft)
                {
                    leftHandIKEnabled = false;
                    leftHandIKEnableTimer = 0f;
                    leftHandIKEnablePending = false;
                }
                else
                {
                    rightHandIKEnabled = false;
                    rightHandIKEnableTimer = 0f;
                    rightHandIKEnablePending = false;
                }

                // 权重为0
                effector.positionWeight = Mathf.SmoothDamp(effector.positionWeight, 0f, ref posWeightVelocity, handExitSmoothTime);
                effector.rotationWeight = Mathf.SmoothDamp(effector.rotationWeight, 0f, ref rotWeightVelocity, handExitSmoothTime);

                // ✅ mapping.weight = 0 当远离墙面
                limbMapping.weight = Mathf.SmoothDamp(limbMapping.weight, 0f, ref maintainPosVel, handExitSmoothTime);
                limbMapping.maintainRotationWeight = Mathf.SmoothDamp(limbMapping.maintainRotationWeight, 0f, ref maintainRotVel, handExitSmoothTime);
            }

            if (showDebugRays)
            {
                Debug.DrawRay(rayStart, rayDirection * rayDistance, Color.red);
            }
        }
    }

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

        // 绘制移动检测相关的Gizmos
        if (characterRoot != null)
        {
            Gizmos.color = isIdle ? Color.green : Color.red;
            Gizmos.DrawWireSphere(characterRoot.position, 0.1f);

            // 显示静止计时器
            if (!isIdle)
            {
                Gizmos.color = Color.yellow;
                float remainingTime = Mathf.Max(0, idleDetectionDelay - idleTimer);
                Gizmos.DrawWireSphere(characterRoot.position + Vector3.up * 0.3f, remainingTime / idleDetectionDelay * 0.1f);
            }

            // 显示IK启用状态
            if (leftHandIKEnabled)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(ik.solver.leftHandEffector.bone.position + Vector3.up * 0.2f, 0.05f);
            }
            else if (leftHandIKEnablePending)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(ik.solver.leftHandEffector.bone.position + Vector3.up * 0.2f, 0.05f);
            }

            if (rightHandIKEnabled)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(ik.solver.rightHandEffector.bone.position + Vector3.up * 0.2f, 0.05f);
            }
            else if (rightHandIKEnablePending)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(ik.solver.rightHandEffector.bone.position + Vector3.up * 0.2f, 0.05f);
            }
        }
    }
}


