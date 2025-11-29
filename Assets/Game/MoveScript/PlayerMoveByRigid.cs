using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MoveByRigidbody : MonoBehaviour
{
    [Header("移动设置")]
    [SerializeField] float walkSpeed = 7.5f;
    [SerializeField] float jumpForce = 8f;
    [SerializeField] float rotateSpeed = 180f;
    [SerializeField] float moveSmoothTime = 0.1f;
    [SerializeField] float rotationSmoothTime = 0.1f;
    [SerializeField] float gravityScale = 9.81f;

    [Header("地面检测")]
    [SerializeField] float groundCheckDistance = 0.1f;
    [SerializeField] LayerMask groundLayer = -1;
    [SerializeField] bool debugGrounding = false;

    [Header("相机设置")]
    [SerializeField] Camera playerCamera;
    [SerializeField] float cameraDistance = 5f;
    [SerializeField] float cameraHeight = 3f;
    [SerializeField] float cameraAngle = 30f;
    [SerializeField] Vector3 cameraOffset = new Vector3(0, 0, -1);
    [SerializeField] float cameraSmoothSpeed = 10f;

    [Header("组件引用")]
    [SerializeField] new Rigidbody rigidbody;
    [SerializeField] CapsuleCollider capsuleCollider;
    [SerializeField] Animator animator;

    private bool isGrounded = false;
    private bool wasGrounded = false;
    private Vector3 currentMoveDirection;
    private Vector3 moveVelocity;
    private Vector3 rotationVelocity;
    private Transform groundCheckTransform;
    private Vector3 cameraTargetPosition;
    private float horizontalInput;
    private float verticalInput;

    void Start()
    {
        // 自动获取组件
        if (rigidbody == null)
        {
            rigidbody = GetComponent<Rigidbody>();
            if (rigidbody == null)
            {
                Debug.LogError("Rigidbody组件未找到！");
                return;
            }
        }

        if (capsuleCollider == null)
        {
            capsuleCollider = GetComponent<CapsuleCollider>();
            if (capsuleCollider == null)
            {
                Debug.LogWarning("CapsuleCollider组件未找到，将使用默认值");
            }
        }

        if (playerCamera == null)
        {
            playerCamera = Camera.main;
        }

        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }

        // 初始化Rigidbody属性
        rigidbody.freezeRotation = true;
        rigidbody.drag = 1f; // 地面阻力
        rigidbody.angularDrag = 5f;
        rigidbody.collisionDetectionMode = CollisionDetectionMode.Continuous;

        // 创建地面检测点
        GameObject groundCheckObj = new GameObject("GroundCheck");
        groundCheckObj.transform.SetParent(transform);
        groundCheckObj.transform.localPosition = Vector3.zero;
        groundCheckTransform = groundCheckObj.transform;

        // 初始化变量
        currentMoveDirection = Vector3.zero;
        moveVelocity = Vector3.zero;
        rotationVelocity = Vector3.zero;

        // 初始化相机
        UpdateCameraTargetPosition();
        if (playerCamera != null)
        {
            playerCamera.transform.position = cameraTargetPosition;
        }
    }

    void Update()
    {
        // 获取输入
        horizontalInput = Input.GetAxis("Horizontal");
        verticalInput = Input.GetAxis("Vertical");

        // 检测是否接地
        CheckGrounded();

        // 处理输入和移动
        HandleMovementInput();

        // 跳跃检测（仅在接地时允许跳跃）
        if (Input.GetKeyDown(KeyCode.Space) && isGrounded)
        {
            Vector3 newVelocity = rigidbody.velocity;
            newVelocity.y = jumpForce;
            rigidbody.velocity = newVelocity;

            if (debugGrounding)
                Debug.Log("跳跃！当前速度: " + rigidbody.velocity);
        }

        // 更新相机
        UpdateCameraPosition();

        // 更新动画参数
        UpdateAnimatorParams();

        // 调试信息
        if (debugGrounding && Time.frameCount % 60 == 0) // 每秒打印一次
        {
            Debug.Log($"接地状态: {isGrounded}, 速度: {rigidbody.velocity}, 移动方向: {currentMoveDirection}");
        }
    }

    private void FixedUpdate()
    {
        // 更新地面检测位置（在FixedUpdate中确保与物理更新同步）
        UpdateGroundCheckPosition();

        // 处理移动
        ApplyMovement();

        // 应用重力
        ApplyGravity();
    }

    private void HandleMovementInput()
    {
        // 获取输入（去除死区处理）
        Vector3 inputDirection = new Vector3(horizontalInput, 0, verticalInput);

        // 标准化输入方向（直接标准化，无死区判断）
        if (inputDirection.magnitude > 1) inputDirection.Normalize();

        // 将输入方向转换为世界空间（相对于相机方向）
        Vector3 forward = playerCamera.transform.forward;
        Vector3 right = playerCamera.transform.right;

        // 忽略Y轴，只考虑水平方向
        forward.y = 0;
        right.y = 0;
        forward.Normalize();
        right.Normalize();

        // 计算世界空间中的移动方向
        Vector3 worldDirection = (forward * inputDirection.z + right * inputDirection.x).normalized;

        // 平滑过渡移动方向
        if (worldDirection.magnitude > 0.1f)
        {
            currentMoveDirection = Vector3.SmoothDamp(
                currentMoveDirection,
                worldDirection,
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
                rotationSmoothTime * 0.5f
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
    }

    private void ApplyMovement()
    {
        // 固定使用步行速度（去除冲刺机制）
        float currentSpeed = walkSpeed;

        // 空中移动速度衰减（保留原逻辑）
        if (!isGrounded) currentSpeed *= 0.8f;

        // 计算目标速度
        Vector3 targetVelocity = currentMoveDirection * currentSpeed;

        // 应用速度（只改XZ轴，Y轴由重力/跳跃控制）
        rigidbody.velocity = new Vector3(
            targetVelocity.x,
            rigidbody.velocity.y,
            targetVelocity.z
        );
    }

    private void ApplyGravity()
    {
        // 去除吸附地面逻辑，直接应用重力（单一加速度）
        Vector3 gravityForce = Vector3.down * gravityScale;
        rigidbody.AddForce(gravityForce, ForceMode.Acceleration);
    }

    private void CheckGrounded()
    {
        // 仅使用射线检测判断是否接地（去除球形检测备选）
        RaycastHit hit;
        Vector3 groundCheckPos = groundCheckTransform.position;

        // 向下射线检测
        isGrounded = Physics.Raycast(groundCheckPos, Vector3.down, out hit, groundCheckDistance, groundLayer);

        wasGrounded = isGrounded;
    }

    private void UpdateGroundCheckPosition()
    {
        // 更新地面检测点位置到角色底部
        if (groundCheckTransform != null)
        {
            if (capsuleCollider != null)
            {
                // 计算胶囊体底部位置
                Vector3 bottomPosition = transform.position;
                bottomPosition.y -= capsuleCollider.height / 2 - capsuleCollider.radius;
                groundCheckTransform.position = bottomPosition;
            }
            else
            {
                // 如果没有胶囊体碰撞器，使用transform位置
                groundCheckTransform.position = transform.position;
            }
        }
    }

    #region 相机逻辑
    void UpdateCameraTargetPosition()
    {
        Vector3 characterCenter = transform.position;
        if (capsuleCollider != null)
        {
            characterCenter += capsuleCollider.center;
        }

        float horizontalDistance = cameraDistance * Mathf.Cos(cameraAngle * Mathf.Deg2Rad);
        float verticalOffset = cameraHeight;

        Vector3 fixedOffset = new Vector3(
            cameraOffset.x * horizontalDistance,
            verticalOffset,
            cameraOffset.z * horizontalDistance
        );

        cameraTargetPosition = characterCenter + fixedOffset;
    }

    void UpdateCameraPosition()
    {
        // 相机位置平滑移动
        if (playerCamera != null)
        {
            UpdateCameraTargetPosition();
            playerCamera.transform.position = Vector3.Lerp(
                playerCamera.transform.position,
                cameraTargetPosition,
                cameraSmoothSpeed * Time.deltaTime
            );

            // 设置相机看向角色
            Vector3 lookTarget = transform.position;
            if (capsuleCollider != null)
            {
                lookTarget += capsuleCollider.center;
            }
            playerCamera.transform.LookAt(lookTarget);
        }
    }
    #endregion

    #region 动画参数
    void UpdateAnimatorParams()
    {
        if (animator == null) return;

        // 更新移动速度参数
        float moveMagnitude = currentMoveDirection.magnitude;
        animator.SetFloat("WalkSpeed", moveMagnitude);

        // 更新接地状态参数（如果动画控制器中有这个参数）
        animator.SetBool("IsGrounded", isGrounded);

        // 移除冲刺状态参数（因冲刺机制已删除）
    }
    #endregion

    private void OnDrawGizmosSelected()
    {
        if (groundCheckTransform != null)
        {
            // 绘制地面检测射线
            Gizmos.color = isGrounded ? Color.green : Color.red;
            Gizmos.DrawLine(groundCheckTransform.position,
                           groundCheckTransform.position + Vector3.down * groundCheckDistance);
        }

        // 绘制移动方向
        Gizmos.color = Color.blue;
        Gizmos.DrawRay(transform.position, currentMoveDirection * 2f);

        // 绘制速度向量
        if (rigidbody != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawRay(transform.position, rigidbody.velocity * 0.1f);
        }
    }
}