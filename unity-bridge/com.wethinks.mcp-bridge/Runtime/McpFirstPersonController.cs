using UnityEngine;

namespace WeThinks.Mcp.Runtime
{
    /// <summary>
    /// A self-contained first-person character controller created by the MCP
    /// "first_person_player" generator. Supports walk, mouse look, jump, and a
    /// crawl that shrinks the collider.
    ///
    /// It reads the legacy Input Manager axes (Horizontal / Vertical / Mouse X /
    /// Mouse Y), so the project's Player setting "Active Input Handling" must be
    /// "Input Manager (Old)" or "Both".
    ///
    /// This component is precompiled into the package's runtime assembly so the
    /// generator can attach it in a single call without waiting for a script
    /// recompile.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class McpFirstPersonController : MonoBehaviour
    {
        [Header("Movement")]
        public float walkSpeed = 6f;
        public float crawlSpeed = 2f;
        public float jumpHeight = 1.2f;
        public float gravity = -20f;

        [Header("Look")]
        public float mouseSensitivity = 2f;
        public float maxLookAngle = 85f;

        [Header("Crawl")]
        public float standingHeight = 2f;
        public float crawlingHeight = 1f;

        private CharacterController _controller;
        private Transform _camera;
        private float _verticalVelocity;
        private float _pitch;
        private float _standCameraY;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            Camera cam = GetComponentInChildren<Camera>();
            if (cam != null)
            {
                _camera = cam.transform;
                _standCameraY = _camera.localPosition.y;
            }
        }

        private void OnEnable()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void Update()
        {
            Look();
            Crawl();
            Move();
        }

        private bool IsCrawling =>
            Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.C);

        private void Look()
        {
            if (_camera == null)
            {
                return;
            }

            float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
            float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

            transform.Rotate(Vector3.up, mouseX);
            _pitch = Mathf.Clamp(_pitch - mouseY, -maxLookAngle, maxLookAngle);
            _camera.localEulerAngles = new Vector3(_pitch, 0f, 0f);
        }

        private void Crawl()
        {
            float targetHeight = IsCrawling ? crawlingHeight : standingHeight;
            _controller.height = Mathf.Lerp(_controller.height, targetHeight, Time.deltaTime * 10f);
            _controller.center = new Vector3(0f, _controller.height * 0.5f, 0f);

            if (_camera != null && standingHeight > 0f)
            {
                float targetCamY = _standCameraY * (_controller.height / standingHeight);
                Vector3 cp = _camera.localPosition;
                _camera.localPosition = new Vector3(
                    cp.x,
                    Mathf.Lerp(cp.y, targetCamY, Time.deltaTime * 10f),
                    cp.z);
            }
        }

        private void Move()
        {
            float speed = IsCrawling ? crawlSpeed : walkSpeed;
            float h = Input.GetAxis("Horizontal");
            float v = Input.GetAxis("Vertical");

            Vector3 move = transform.right * h + transform.forward * v;
            if (move.sqrMagnitude > 1f)
            {
                move.Normalize();
            }

            if (_controller.isGrounded)
            {
                _verticalVelocity = -1f;
                if (!IsCrawling && Input.GetKeyDown(KeyCode.Space))
                {
                    _verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
                }
            }
            else
            {
                _verticalVelocity += gravity * Time.deltaTime;
            }

            Vector3 velocity = move * speed + Vector3.up * _verticalVelocity;
            _controller.Move(velocity * Time.deltaTime);
        }
    }
}
