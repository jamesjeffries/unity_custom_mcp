using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace WeThinks.Mcp.Runtime
{
    /// <summary>
    /// A self-contained first-person character controller created by the MCP
    /// "first_person_player" generator. Supports walk, mouse look, jump, and a
    /// crawl that shrinks the collider.
    ///
    /// It defaults to Unity's Input System package (the new input handler),
    /// reading the keyboard and mouse devices directly so no Input Action asset
    /// is required. When a project is configured for the legacy Input Manager
    /// only, it transparently falls back to the old <c>Input</c> API.
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
        public float mouseSensitivity = 0.1f;
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

        // --- Input abstraction (new Input System by default, legacy fallback) ---

        private Vector2 ReadMoveAxis()
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard kb = Keyboard.current;
            if (kb == null)
            {
                return Vector2.zero;
            }

            float x = (kb.dKey.isPressed ? 1f : 0f) - (kb.aKey.isPressed ? 1f : 0f);
            float y = (kb.wKey.isPressed ? 1f : 0f) - (kb.sKey.isPressed ? 1f : 0f);
            return new Vector2(x, y);
#else
            return new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
#endif
        }

        private Vector2 ReadLookDelta()
        {
#if ENABLE_INPUT_SYSTEM
            Mouse mouse = Mouse.current;
            return mouse != null ? mouse.delta.ReadValue() * mouseSensitivity : Vector2.zero;
#else
            return new Vector2(
                Input.GetAxis("Mouse X"),
                Input.GetAxis("Mouse Y")) * (mouseSensitivity * 20f);
#endif
        }

        private bool ReadJumpPressed()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.Space);
#endif
        }

        private bool ReadCrawlHeld()
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard kb = Keyboard.current;
            return kb != null && (kb.leftCtrlKey.isPressed || kb.cKey.isPressed);
#else
            return Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.C);
#endif
        }

        // --- Behaviour ---

        private bool IsCrawling => ReadCrawlHeld();

        private void Look()
        {
            if (_camera == null)
            {
                return;
            }

            Vector2 look = ReadLookDelta();
            transform.Rotate(Vector3.up, look.x);
            _pitch = Mathf.Clamp(_pitch - look.y, -maxLookAngle, maxLookAngle);
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
            Vector2 axis = ReadMoveAxis();

            Vector3 move = transform.right * axis.x + transform.forward * axis.y;
            if (move.sqrMagnitude > 1f)
            {
                move.Normalize();
            }

            if (_controller.isGrounded)
            {
                _verticalVelocity = -1f;
                if (!IsCrawling && ReadJumpPressed())
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
