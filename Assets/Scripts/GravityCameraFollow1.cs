
using UnityEngine;

public class GravityCameraFollow : MonoBehaviour
{
    [Header("Target")]
    public Transform player;

    [Header("Camera Offset")]
    public float distance = 6f;
    public float height = 2.5f;

    [Header("Follow")]
    public float followSpeed = 12f;
    public float rotationSpeed = 10f;

    [Header("Gravity Rotation Smoothness")]
    public float gravityAlignSpeed = 6f;
    public float gravityChangeAngleThreshold = 8f;

    [Header("Mouse Look")]
    public bool useMouseLook = true;
    public float mouseSensitivity = 3f;
    public float minPitch = -25f;
    public float maxPitch = 60f;

    [Header("Camera Collision")]
    public LayerMask collisionLayers = ~0;
    public float collisionRadius = 0.3f;
    public float collisionOffset = 0.25f;
    public float minCameraDistance = 1.2f;
    public bool ignorePlayerLayer = true;

    private Vector3 currentUp;
    private Vector3 lastUp;
    private Vector3 cameraPlanarForward;

    private float yaw;
    private float pitch = 15f;

    private void Start()
    {
        if (player == null)
        {
            Debug.LogError("Player not assigned in GravityCameraFollow.");
            enabled = false;
            return;
        }

        currentUp = player.up;
        lastUp = player.up;

        cameraPlanarForward = Vector3.ProjectOnPlane(transform.forward, currentUp).normalized;

        if (cameraPlanarForward.sqrMagnitude < 0.01f)
            cameraPlanarForward = Vector3.ProjectOnPlane(player.forward, currentUp).normalized;

        if (cameraPlanarForward.sqrMagnitude < 0.01f)
            cameraPlanarForward = Vector3.forward;

        if (ignorePlayerLayer)
        {
            collisionLayers &= ~(1 << player.gameObject.layer);
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void LateUpdate()
    {
        if (player == null) return;

        HandleMouseLook();
        HandleCameraFollow();
    }

    private void HandleMouseLook()
    {
        if (!useMouseLook) return;

        yaw += Input.GetAxis("Mouse X") * mouseSensitivity;
        pitch -= Input.GetAxis("Mouse Y") * mouseSensitivity;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
    }

    private void HandleCameraFollow()
    {
        Vector3 targetUp = player.up;

        bool gravityChanged = Vector3.Angle(lastUp, targetUp) > gravityChangeAngleThreshold;

        if (gravityChanged)
        {
            Quaternion gravityRotation = Quaternion.FromToRotation(lastUp, targetUp);

            cameraPlanarForward = gravityRotation * cameraPlanarForward;
            cameraPlanarForward = Vector3.ProjectOnPlane(cameraPlanarForward, targetUp).normalized;

            if (cameraPlanarForward.sqrMagnitude < 0.01f)
                cameraPlanarForward = Vector3.ProjectOnPlane(player.forward, targetUp).normalized;

            lastUp = targetUp;
        }

        currentUp = Vector3.Slerp(
            currentUp,
            targetUp,
            gravityAlignSpeed * Time.deltaTime
        );

        Quaternion yawRotation = Quaternion.AngleAxis(yaw, currentUp);

        Vector3 forward = yawRotation * cameraPlanarForward;
        forward = Vector3.ProjectOnPlane(forward, currentUp).normalized;

        if (forward.sqrMagnitude < 0.01f)
            forward = Vector3.ProjectOnPlane(transform.forward, currentUp).normalized;

        Vector3 right = Vector3.Cross(currentUp, forward).normalized;

        Quaternion pitchRotation = Quaternion.AngleAxis(pitch, right);

        Vector3 backDirection = pitchRotation * -forward;

        Vector3 lookTarget =
            player.position +
            currentUp * 1.2f;

        Vector3 desiredPosition =
            player.position +
            backDirection.normalized * distance +
            currentUp * height;

        desiredPosition = ResolveCameraCollision(lookTarget, desiredPosition);

        transform.position = Vector3.Lerp(
            transform.position,
            desiredPosition,
            followSpeed * Time.deltaTime
        );

        Quaternion desiredRotation = Quaternion.LookRotation(
            lookTarget - transform.position,
            currentUp
        );

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            desiredRotation,
            rotationSpeed * Time.deltaTime
        );
    }

    private Vector3 ResolveCameraCollision(Vector3 lookTarget, Vector3 desiredPosition)
    {
        Vector3 direction = desiredPosition - lookTarget;
        float desiredDistance = direction.magnitude;

        if (desiredDistance <= 0.01f)
            return desiredPosition;

        direction.Normalize();

        RaycastHit hit;

        if (Physics.SphereCast(
            lookTarget,
            collisionRadius,
            direction,
            out hit,
            desiredDistance,
            collisionLayers,
            QueryTriggerInteraction.Ignore
        ))
        {
            float safeDistance = hit.distance - collisionOffset;
            safeDistance = Mathf.Clamp(safeDistance, minCameraDistance, desiredDistance);

            return lookTarget + direction * safeDistance;
        }

        return desiredPosition;
    }
}