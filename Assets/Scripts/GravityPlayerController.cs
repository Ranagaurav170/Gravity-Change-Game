
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CapsuleCollider))]
public class GravityPlayerController : MonoBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 4f;
    public float rotationSpeed = 12f;
    public float jumpForce = 7f;
    public float gravityPower = 25f;

    [Header("Jump Settings")]
    public float jumpBufferTime = 0.15f;
    public float coyoteTime = 0.15f;

    [Header("Camera")]
    public Transform cameraTransform;

    [Header("Ground Detection")]
    public float groundNormalDotLimit = 0.55f;

    [Header("Gravity Hologram")]
    public Transform hologram;
    public float hologramDistance = 2f;
    public KeyCode applyGravityKey = KeyCode.Return;

    [Header("Gravity Landing With Hologram")]
    public bool landAtHologramPosition = true;

    [Tooltip("Extra height added to hologram position. Increase this if player lands too low on the wall.")]
    public float hologramLandingHeightOffset = 2f;

    [Tooltip("Small offset away from selected gravity surface direction.")]
    public float landingBackOffset = 0.2f;

    [Header("Destroy Layer")]
    public string destroyLayerName = "Box";

    [Header("Animation")]
    public Animator animator;

    private Rigidbody rb;
    private CapsuleCollider capsule;

    private Vector3 currentGravityDirection = Vector3.down;
    private Vector3 selectedGravityDirection = Vector3.down;

    private Vector3 groundNormal = Vector3.up;
    private Vector3 moveDirection;
    private Vector3 desiredForwardDirection = Vector3.forward;

    private bool isGrounded;
    private bool hologramActive;

    private float jumpBufferCounter;
    private float coyoteCounter;

    private int destroyLayer;

    public Vector3 CurrentGravityDirection => currentGravityDirection;
    public Vector3 CharacterUp => isGrounded ? groundNormal : -currentGravityDirection;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        capsule = GetComponent<CapsuleCollider>();

        rb.useGravity = false;
        rb.isKinematic = false;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        rb.constraints = RigidbodyConstraints.FreezeRotation;

        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;

        if (hologram != null)
            hologram.gameObject.SetActive(false);

        destroyLayer = LayerMask.NameToLayer(destroyLayerName);

        desiredForwardDirection = Vector3.ProjectOnPlane(transform.forward, -currentGravityDirection);

        if (desiredForwardDirection.sqrMagnitude < 0.01f)
            desiredForwardDirection = Vector3.forward;

        desiredForwardDirection.Normalize();
    }

    private void Update()
    {
        HandleJumpInput();
        HandleHologramInput();
        HandleAnimations();
    }

    private void FixedUpdate()
    {
        UpdateGroundTimers();

        HandleMovement();
        HandleJump();

        ApplyCustomGravity();
        AlignCharacterSmooth();
    }

    private void HandleJumpInput()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            jumpBufferCounter = jumpBufferTime;
        }
        else
        {
            jumpBufferCounter -= Time.deltaTime;
        }
    }

    private void UpdateGroundTimers()
    {
        if (isGrounded)
        {
            coyoteCounter = coyoteTime;
        }
        else
        {
            coyoteCounter -= Time.fixedDeltaTime;
        }
    }

    private void HandleMovement()
    {
        float horizontal = 0f;
        float vertical = 0f;

        if (Input.GetKey(KeyCode.A))
            horizontal = -1f;

        if (Input.GetKey(KeyCode.D))
            horizontal = 1f;

        if (Input.GetKey(KeyCode.W))
            vertical = 1f;

        if (Input.GetKey(KeyCode.S))
            vertical = -1f;

        Vector3 characterUp = CharacterUp;

        Vector3 cameraForward;
        Vector3 cameraRight;

        GetCameraDirections(characterUp, out cameraForward, out cameraRight);

        moveDirection = ((cameraRight * horizontal) + (cameraForward * vertical)).normalized;

        Vector3 gravityVelocity = Vector3.Project(rb.velocity, currentGravityDirection);
        Vector3 moveVelocity = moveDirection * moveSpeed;

        rb.velocity = moveVelocity + gravityVelocity;

        if (moveDirection.sqrMagnitude > 0.01f)
        {
            desiredForwardDirection = Vector3.ProjectOnPlane(moveDirection, characterUp).normalized;
        }
    }

    private void HandleJump()
    {
        if (jumpBufferCounter > 0f && coyoteCounter > 0f)
        {
            Vector3 jumpDirection = -currentGravityDirection;

            Vector3 nonJumpVelocity = Vector3.ProjectOnPlane(rb.velocity, jumpDirection);
            rb.velocity = nonJumpVelocity;

            rb.AddForce(jumpDirection * jumpForce, ForceMode.VelocityChange);

            isGrounded = false;
            jumpBufferCounter = 0f;
            coyoteCounter = 0f;
        }
    }

    private void GetCameraDirections(Vector3 characterUp, out Vector3 cameraForward, out Vector3 cameraRight)
    {
        if (cameraTransform != null)
        {
            cameraForward = Vector3.ProjectOnPlane(cameraTransform.forward, characterUp);

            if (cameraForward.sqrMagnitude < 0.01f)
                cameraForward = Vector3.ProjectOnPlane(transform.forward, characterUp);

            cameraForward.Normalize();

            cameraRight = Vector3.ProjectOnPlane(cameraTransform.right, characterUp);

            if (cameraRight.sqrMagnitude < 0.01f)
                cameraRight = Vector3.Cross(characterUp, cameraForward);

            cameraRight.Normalize();
        }
        else
        {
            cameraForward = Vector3.ProjectOnPlane(Vector3.forward, characterUp);

            if (cameraForward.sqrMagnitude < 0.01f)
                cameraForward = transform.forward;

            cameraForward.Normalize();

            cameraRight = Vector3.ProjectOnPlane(Vector3.right, characterUp);

            if (cameraRight.sqrMagnitude < 0.01f)
                cameraRight = Vector3.Cross(characterUp, cameraForward);

            cameraRight.Normalize();
        }
    }

   private void HandleHologramInput()
{
    // Hide hologram when player movement keys are pressed
    if (Input.GetKeyDown(KeyCode.W) ||
        Input.GetKeyDown(KeyCode.A) ||
        Input.GetKeyDown(KeyCode.S) ||
        Input.GetKeyDown(KeyCode.D))
    {
        HideHologram();
        return;
    }

    bool arrowPressed = false;

    Vector3 characterUp = CharacterUp;

    Vector3 playerForward = Vector3.ProjectOnPlane(transform.forward, characterUp);
    Vector3 playerRight = Vector3.ProjectOnPlane(transform.right, characterUp);

    if (playerForward.sqrMagnitude < 0.01f)
        playerForward = desiredForwardDirection;

    if (playerRight.sqrMagnitude < 0.01f)
        playerRight = Vector3.Cross(characterUp, playerForward);

    playerForward.Normalize();
    playerRight.Normalize();

    if (Input.GetKeyDown(KeyCode.UpArrow))
    {
        selectedGravityDirection = playerForward;
        arrowPressed = true;
    }
    else if (Input.GetKeyDown(KeyCode.DownArrow))
    {
        selectedGravityDirection = -playerForward;
        arrowPressed = true;
    }
    else if (Input.GetKeyDown(KeyCode.LeftArrow))
    {
        selectedGravityDirection = -playerRight;
        arrowPressed = true;
    }
    else if (Input.GetKeyDown(KeyCode.RightArrow))
    {
        selectedGravityDirection = playerRight;
        arrowPressed = true;
    }

    // Show hologram only after arrow key press
    if (arrowPressed)
    {
        hologramActive = true;

        if (hologram != null)
        {
            hologram.gameObject.SetActive(true);
            UpdateHologram();
        }
    }

    // Keep updating hologram only while active
    if (hologramActive)
    {
        UpdateHologram();
    }

    // Apply gravity only if hologram is active
    if (Input.GetKeyDown(applyGravityKey) && hologramActive)
    {
        ApplySelectedGravity();
    }
}
private void HideHologram()
{
    hologramActive = false;

    if (hologram != null)
        hologram.gameObject.SetActive(false);
}

    private void UpdateHologram()
    {
        if (hologram == null)
            return;

        Vector3 previewGravityDirection = selectedGravityDirection.normalized;
        Vector3 previewUp = -previewGravityDirection;

        Vector3 characterUp = CharacterUp;

        // This is the player's head area.
        Vector3 headPosition = transform.position + characterUp * capsule.height;

        // Hologram appears in selected gravity direction,
        // then height is adjusted upward using current character up.
        hologram.position =
            headPosition +
            previewGravityDirection * hologramDistance +
            characterUp * hologramLandingHeightOffset;

        Vector3 hologramForward = Vector3.ProjectOnPlane(characterUp, previewUp);

        if (hologramForward.sqrMagnitude < 0.01f)
            hologramForward = Vector3.ProjectOnPlane(transform.forward, previewUp);

        if (hologramForward.sqrMagnitude < 0.01f)
            hologramForward = Vector3.ProjectOnPlane(desiredForwardDirection, previewUp);

        if (hologramForward.sqrMagnitude < 0.01f)
            hologramForward = Vector3.forward;

        hologram.rotation = Quaternion.LookRotation(hologramForward.normalized, previewUp);
    }

    private void ApplySelectedGravity()
    {
        Vector3 oldUp = transform.up;
        Vector3 oldForward = transform.forward;
        Vector3 oldRight = transform.right;

        Vector3 newGravityDirection = selectedGravityDirection.normalized;
        Vector3 newUp = -newGravityDirection;

        Vector3 preservedForward = Vector3.ProjectOnPlane(oldForward, newUp);

        if (preservedForward.sqrMagnitude < 0.01f)
            preservedForward = Vector3.ProjectOnPlane(oldUp, newUp);

        if (preservedForward.sqrMagnitude < 0.01f)
            preservedForward = Vector3.ProjectOnPlane(oldRight, newUp);

        if (preservedForward.sqrMagnitude < 0.01f)
            preservedForward = Vector3.forward;

        desiredForwardDirection = preservedForward.normalized;

        // Move player exactly to hologram position before applying gravity.
        if (landAtHologramPosition && hologram != null && hologramActive)
        {
            Vector3 landingPosition = hologram.position;

            // Small backward offset against gravity direction,
            // so player does not spawn inside the wall.
            landingPosition -= newGravityDirection * landingBackOffset;

            rb.position = landingPosition;
            transform.position = landingPosition;
        }

        currentGravityDirection = newGravityDirection;

        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        isGrounded = false;
        coyoteCounter = 0f;
        jumpBufferCounter = 0f;

        groundNormal = newUp;

        Quaternion targetRotation = Quaternion.LookRotation(desiredForwardDirection, newUp);

        rb.MoveRotation(targetRotation);
        transform.rotation = targetRotation;

        if (hologram != null)
            hologram.gameObject.SetActive(false);

        hologramActive = false;
    }

    private void ApplyCustomGravity()
    {
        rb.AddForce(currentGravityDirection * gravityPower, ForceMode.Acceleration);
    }

    private void AlignCharacterSmooth()
    {
        Vector3 characterUp = CharacterUp;

        Vector3 forward = Vector3.ProjectOnPlane(desiredForwardDirection, characterUp);

        if (forward.sqrMagnitude < 0.01f)
            forward = Vector3.ProjectOnPlane(transform.forward, characterUp);

        if (forward.sqrMagnitude < 0.01f)
            forward = Vector3.ProjectOnPlane(transform.right, characterUp);

        if (forward.sqrMagnitude < 0.01f)
            return;

        Quaternion targetRotation = Quaternion.LookRotation(forward.normalized, characterUp);

        rb.MoveRotation(
            Quaternion.Slerp(
                rb.rotation,
                targetRotation,
                rotationSpeed * Time.fixedDeltaTime
            )
        );
    }

    private void OnCollisionEnter(Collision collision)
    {
        CheckCollisionGround(collision);
        DestroyBoxObject(collision.gameObject);
    }

    private void OnCollisionStay(Collision collision)
    {
        CheckCollisionGround(collision);
    }

    private void OnCollisionExit(Collision collision)
    {
        isGrounded = false;
    }

    private void OnTriggerEnter(Collider other)
    {
        DestroyBoxObject(other.gameObject);
    }

    private void DestroyBoxObject(GameObject obj)
    {
        if (destroyLayer == -1)
            return;

        if (obj.layer == destroyLayer)
        {
            Destroy(obj);
        }
    }

    private void CheckCollisionGround(Collision collision)
    {
        Vector3 expectedGroundNormal = -currentGravityDirection;

        for (int i = 0; i < collision.contactCount; i++)
        {
            ContactPoint contact = collision.GetContact(i);

            float dot = Vector3.Dot(contact.normal, expectedGroundNormal);

            if (dot >= groundNormalDotLimit)
            {
                isGrounded = true;
                groundNormal = contact.normal;

                float fallSpeed = Vector3.Dot(rb.velocity, currentGravityDirection);

                if (fallSpeed > 0f)
                {
                    Vector3 nonGravityVelocity = Vector3.ProjectOnPlane(rb.velocity, currentGravityDirection);
                    rb.velocity = nonGravityVelocity;
                }

                return;
            }
        }
    }
    

    private void HandleAnimations()
    {
        if (animator == null)
            return;

        float speed = moveDirection.magnitude;

        float fallingSpeed = Vector3.Dot(rb.velocity, currentGravityDirection);

        bool isFalling = !isGrounded && fallingSpeed > 0.3f;

        animator.SetFloat("Speed", speed);
        animator.SetBool("IsGrounded", isGrounded);
        animator.SetBool("IsFalling", isFalling);
    }
}