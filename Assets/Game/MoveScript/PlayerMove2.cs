using UnityEngine;

public class PlayerControllerRigidbody : MonoBehaviour
{
    [Header("引用组件")]
    public Rigidbody rb;
    public Camera playerCamera;
    public CapsuleCollider capsuleCollider;
    public Animator animator; // 动画组件（需手动赋值或自动获取）
    public Transform groundCheck; // 地面检测空物体（拖入场景中的空物体，放在角色脚底）

    [Header("移动设置")]
    public float walkSpeed = 7.5f;
    public float sprintSpeed = 9f;
    public float rotateSpeed = 180f; // 旋转速度（度/秒）
    public float inputDeadZone = 0.1f;
    public float moveSmoothTime = 0.1f;
    public float rotationSmoothTime = 0.1f; // 新增：旋转平滑时间

    [Header("跳跃设置")]
    public float jumpForce = 6f; // 跳跃力（优化为6f，确保跳跃高度合适）
    public float gravityScale = 3f; // 重力缩放
    public float groundCheckRadius = 0.1f; // 减小检测半径，减少误检测
    public LayerMask groundMask; // 地面图层（需在Inspector中选择地面所在图层）
    public float jumpCooldown = 0.2f; // 跳跃冷却（防止连跳）
    public bool debugJump = true; // 是否开启跳跃调试日志
    public float groundCheckDistance = 0.1f; // 新增：接地检测距离

    [Header("第三人称相机设置")]
    public float cameraDistance = 5f;
    public float cameraHeight = 3f;
    public float cameraAngle = 30f;
    public Vector3 cameraOffset = new Vector3(0, 0, -1);
    public float cameraRotateSpeed = 180f;
    public float cameraSmoothSpeed = 10f;
    public LayerMask obstacleMask;

    // 私有变量
    private Vector3 currentMoveDirection;
    private Vector3 moveVelocity; // 移动平滑用
    private Vector3 rotationVelocity; // 旋转平滑用
    private bool isGrounded;
    private bool wasGrounded = false; // 记录上一帧是否接地
    private Vector3 cameraTargetPosition;
    private float jumpTimer; // 跳跃冷却计时器
    private bool hasJumpedOnce = false; // 记录是否成功跳跃过（避免重复打印）
    private float groundCheckTimer = 0f; // 接地检测定时器
    private const float groundCheckInterval = 0.05f; // 接地检测间隔

    // 动画参数（只保留你之前存在的WalkSpeed）
    private const string WALK_SPEED = "WalkSpeed";

    void Start()
    {
        // 自动获取组件
        if (rb == null)
        {
            rb = GetComponent<Rigidbody>();
            if (debugJump) Debug.Log($"[跳跃调试] 自动获取Rigidbody组件：{rb != null}");
        }
        if (capsuleCollider == null)
        {
            capsuleCollider = GetComponent<CapsuleCollider>();
            if (debugJump) Debug.Log($"[跳跃调试] 自动获取CapsuleCollider组件：{capsuleCollider != null}");
        }
        if (playerCamera == null) playerCamera = Camera.main;
        if (animator == null) animator = GetComponent<Animator>();

        // 检查地面检测空物体是否赋值
        if (groundCheck == null)
        {
            // 自动创建地面检测空物体（如果未手动赋值）
            GameObject groundCheckObj = new GameObject("GroundCheck");
            groundCheckObj.transform.parent = transform;
            // 位置设置在胶囊体底部
            Vector3 colliderBottom = transform.position + capsuleCollider.center
                - new Vector3(0, capsuleCollider.height / 2 - capsuleCollider.radius, 0);
            groundCheckObj.transform.position = colliderBottom;
            groundCheck = groundCheckObj.transform;
            if (debugJump) Debug.Log($"[跳跃调试] 自动创建GroundCheck空物体，位置：{groundCheck.position}");
        }
        else
        {
            if (debugJump) Debug.Log($"[跳跃调试] 手动赋值GroundCheck空物体，位置：{groundCheck.position}");
        }

        // 初始化Rigidbody（关键！确保跳跃和移动正常）
        rb.freezeRotation = true; // 冻结旋转，避免物理倾斜
        rb.useGravity = true;
        rb.drag = 0.5f; // 地面阻力，避免滑行
        rb.angularDrag = 5f;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous; // 避免穿模
        rb.interpolation = RigidbodyInterpolation.Interpolate; // 添加插值，减少抖动
        if (debugJump) Debug.Log($"[跳跃调试] Rigidbody初始化完成，冻结旋转：{rb.freezeRotation}，使用重力：{rb.useGravity}");

        // 相机初始化
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        if (playerCamera.transform.parent == transform)
        {
            playerCamera.transform.parent = null;
            Debug.Log("第三人称相机已解除父子关系");
        }
        UpdateCameraTargetPosition();
        playerCamera.transform.position = cameraTargetPosition;
        SetCameraLookAt();

        // 初始化变量
        currentMoveDirection = Vector3.zero;
        moveVelocity = Vector3.zero;
        rotationVelocity = Vector3.zero;
        jumpTimer = jumpCooldown; // 跳跃冷却初始化
        if (debugJump) Debug.Log($"[跳跃调试] 初始化完成，跳跃冷却：{jumpTimer}/{jumpCooldown}");

        // 检查动画组件
        if (animator == null)
        {
            Debug.LogWarning("警告：未找到Animator组件！动画参数无法传递");
        }
    }

    void Update()
    {
        // 每帧更新：地面检测 → 输入处理 → 跳跃检测 → 相机更新 → 动画参数
        CheckGrounded();
        HandleMovementInput();
        HandleJumpInput();
        UpdateCameraPosition();
        UpdateAnimatorParams();

        // 每帧打印接地状态（方便观察）
        if (debugJump && Time.frameCount % 30 == 0) // 每30帧打印一次，避免日志刷屏
        {
            Debug.Log($"[跳跃调试] 接地状态：{isGrounded} | 跳跃冷却：{jumpTimer:F2}/{jumpCooldown} | Y轴速度：{rb.velocity.y:F2}");
        }
    }

    void FixedUpdate()
    {
        // 物理相关：移动 → 重力（FixedUpdate保证帧率稳定）
        ApplyMovement();
        ApplyGravity();
    }

    #region 核心逻辑（移动、跳跃、重力）
    void HandleMovementInput()
    {
        // 获取输入
        float x = Input.GetAxis("Horizontal");
        float z = Input.GetAxis("Vertical");

        // 应用死区
        x = Mathf.Abs(x) < inputDeadZone ? 0f : x;
        z = Mathf.Abs(z) < inputDeadZone ? 0f : z;

        // 目标移动方向（世界空间，归一化）
        Vector3 targetMoveDirection = new Vector3(x, 0f, z).normalized;

        // 平滑过渡移动方向
        if (targetMoveDirection.magnitude > 0.1f)
        {
            // 使用SmoothDamp进行平滑过渡
            currentMoveDirection = Vector3.SmoothDamp(
                currentMoveDirection,
                targetMoveDirection,
                ref moveVelocity,
                rotationSmoothTime
            );
        }
        else
        {
            // 无输入时衰减移动方向
            currentMoveDirection = Vector3.SmoothDamp(
                currentMoveDirection,
                Vector3.zero,
                ref moveVelocity,
                rotationSmoothTime * 0.5f // 减速更快
            );
        }

        // 角色旋转（基于平滑后的方向进行旋转）
        if (currentMoveDirection.magnitude > 0.1f)
        {
            // 计算目标旋转
            Vector3 targetForward = new Vector3(currentMoveDirection.x, 0, currentMoveDirection.z);
            Quaternion targetRotation = Quaternion.LookRotation(targetForward, Vector3.up);

            // 使用平滑旋转
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRotation,
                rotateSpeed * Time.deltaTime
            );
        }

        // 有移动时更新相机位置
        if (currentMoveDirection.magnitude > 0.1f)
        {
            UpdateCameraTargetPosition();
        }
    }

    void ApplyMovement()
    {
        // 计算当前速度（冲刺逻辑：仅向前且按Shift生效）
        float currentSpeed = walkSpeed;
        bool isSprinting = Input.GetKey(KeyCode.LeftShift) && currentMoveDirection.z > 0.5f;
        if (isSprinting) currentSpeed = sprintSpeed;

        // 空中移动速度衰减（更自然）
        if (!isGrounded) currentSpeed *= 0.8f;

        // 计算目标速度
        Vector3 targetVelocity = currentMoveDirection * currentSpeed;

        // 应用速度（只改XZ轴，Y轴由重力/跳跃控制）
        rb.velocity = new Vector3(
            targetVelocity.x,
            rb.velocity.y,
            targetVelocity.z
        );
    }

    void HandleJumpInput()
    {
        // 跳跃冷却计时
        if (jumpTimer < jumpCooldown)
        {
            jumpTimer += Time.deltaTime;
            return;
        }

        // 检测跳跃输入（按下空格键）
        if (Input.GetButtonDown("Jump"))
        {
            if (debugJump) Debug.Log($"[跳跃调试] 检测到跳跃输入！接地状态：{isGrounded} | 冷却是否完成：{jumpTimer >= jumpCooldown}");

            // 跳跃条件：接地 + 按下跳跃键（使用地面检测空物体的结果）
            if (isGrounded)
            {
                // 直接设置Y轴速度（跳跃更稳定，避免叠加力的问题）
                rb.velocity = new Vector3(rb.velocity.x, jumpForce, rb.velocity.z);
                jumpTimer = 0f; // 重置冷却
                if (debugJump && !hasJumpedOnce)
                {
                    Debug.Log($"[跳跃调试] 跳跃成功！跳跃力：{jumpForce} | 跳跃后Y轴速度：{rb.velocity.y}");
                    hasJumpedOnce = true; // 只打印一次成功日志
                }
            }
            else
            {
                if (debugJump) Debug.LogWarning($"[跳跃调试] 跳跃失败：未接地！当前Y轴速度：{rb.velocity.y}");
            }
        }
    }

    void ApplyGravity()
    {
        // 接地时Y轴速度重置（避免接地后仍有下落速度）
        if (isGrounded && rb.velocity.y < 0)
        {
            // 不再直接重置Y轴速度，而是使用射线检测确认接地
            if (wasGrounded) // 只有在上一帧也接地时才重置
            {
                rb.velocity = new Vector3(rb.velocity.x, -0.5f, rb.velocity.z); // 轻微吸附地面
            }
            if (debugJump && Time.frameCount % 60 == 0)
            {
                Debug.Log($"[跳跃调试] 接地时重置Y轴速度：{rb.velocity.y}");
            }
            return;
        }

        // 空中应用重力（Acceleration模式：不受质量影响）
        Vector3 gravityForce = Vector3.down * gravityScale;
        rb.AddForce(gravityForce, ForceMode.Acceleration);

        // 打印空中重力应用信息（每60帧一次）
        if (!isGrounded && debugJump && Time.frameCount % 60 == 0)
        {
            Debug.Log($"[跳跃调试] 空中应用重力：{gravityForce.y} | 当前Y轴速度：{rb.velocity.y}");
        }
    }

    void CheckGrounded()
    {
        // 减少检测频率，避免频繁的物理检测造成抖动
        groundCheckTimer += Time.deltaTime;
        if (groundCheckTimer < groundCheckInterval) return;

        groundCheckTimer = 0f;

        // 使用射线检测而不是球形检测，更稳定
        bool newGrounded = Physics.Raycast(
            groundCheck.position,
            Vector3.down,
            groundCheckDistance,
            groundMask,
            QueryTriggerInteraction.Ignore
        );

        // 检查是否刚接触地面（跳跃后落地）
        if (newGrounded && !wasGrounded)
        {
            // 落地时的处理
            if (rb.velocity.y < -2f) // 如果下落速度过大，稍微调整
            {
                rb.velocity = new Vector3(rb.velocity.x, -2f, rb.velocity.z);
            }
        }

        // 接地状态变化时打印日志
        if (newGrounded != isGrounded && debugJump)
        {
            Debug.Log($"[跳跃调试] 接地状态变化：{isGrounded} → {newGrounded} | 检测位置：{groundCheck.position} | 检测距离：{groundCheckDistance}");

            // 打印检测到的地面信息
            if (newGrounded)
            {
                RaycastHit hit;
                if (Physics.Raycast(groundCheck.position, Vector3.down, out hit, groundCheckDistance, groundMask))
                {
                    Debug.Log($"[跳跃调试] 检测到的地面物体：{hit.collider.gameObject.name} | 距离：{hit.distance}");
                }
            }
        }

        wasGrounded = isGrounded;
        isGrounded = newGrounded;
    }
    #endregion

    #region 相机逻辑（保留原逻辑）
    void UpdateCameraTargetPosition()
    {
        Vector3 characterCenter = transform.position + capsuleCollider.center;
        float horizontalDistance = cameraDistance * Mathf.Cos(cameraAngle * Mathf.Deg2Rad);
        float verticalOffset = cameraHeight;

        Vector3 fixedOffset = new Vector3(
            cameraOffset.x * horizontalDistance,
            verticalOffset,
            cameraOffset.z * horizontalDistance
        );

        cameraTargetPosition = characterCenter + fixedOffset;
        CheckCameraObstacles(characterCenter);
    }

    void UpdateCameraPosition()
    {
        // 相机位置平滑移动
        playerCamera.transform.position = Vector3.Lerp(
            playerCamera.transform.position,
            cameraTargetPosition,
            cameraSmoothSpeed * Time.deltaTime
        );
        SetCameraLookAt();
    }

    void SetCameraLookAt()
    {
        Vector3 lookTarget = transform.position + capsuleCollider.center;
        Quaternion targetRotation = Quaternion.LookRotation(lookTarget - playerCamera.transform.position);

        // 锁定X轴旋转（固定俯视角）
        Vector3 euler = targetRotation.eulerAngles;
        euler.x = cameraAngle;
        targetRotation = Quaternion.Euler(euler);

        // 相机平滑旋转
        float maxCameraRotationStep = cameraRotateSpeed * Time.deltaTime;
        playerCamera.transform.rotation = Quaternion.RotateTowards(
            playerCamera.transform.rotation,
            targetRotation,
            maxCameraRotationStep
        );
    }

    void CheckCameraObstacles(Vector3 characterCenter)
    {
        if (Physics.Linecast(characterCenter, cameraTargetPosition, out RaycastHit hit, obstacleMask))
        {
            cameraTargetPosition = hit.point + hit.normal * 0.1f;
        }
    }
    #endregion

    #region 动画参数传递（仅保留你之前不报错的逻辑）
    void UpdateAnimatorParams()
    {
        if (animator == null) return;

        // 只保留移动速度参数（你之前不报错，说明这个参数存在）
        float moveMagnitude = currentMoveDirection.magnitude;
        animator.SetFloat(WALK_SPEED, moveMagnitude);
    }
    #endregion

    #region Gizmos调试（添加地面检测球形可视化）
    void OnDrawGizmosSelected()
    {
        if (capsuleCollider == null) return;

        // 绘制胶囊体碰撞体
        Gizmos.color = Color.blue;
        Gizmos.DrawWireCube(
            transform.position + capsuleCollider.center,
            new Vector3(capsuleCollider.radius * 2, capsuleCollider.height, capsuleCollider.radius * 2)
        );

        // 绘制地面检测射线（绿色=接地，红色=空中）
        if (groundCheck != null)
        {
            Gizmos.color = isGrounded ? Color.green : Color.red;
            Gizmos.DrawLine(groundCheck.position, groundCheck.position + Vector3.down * groundCheckDistance);

            // 绘制检测范围的中心点（黄色）
            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(groundCheck.position, 0.05f);
        }

        // 绘制移动方向
        Gizmos.color = Color.green;
        Gizmos.DrawRay(transform.position, currentMoveDirection * 2f);

        // 绘制相机视线
        if (playerCamera != null)
        {
            Gizmos.color = Color.yellow;
            Vector3 lookTarget = transform.position + capsuleCollider.center;
            Gizmos.DrawLine(playerCamera.transform.position, lookTarget);
        }
    }
    #endregion
}



