using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Client.GameObjects.Network
{
    public class PlayerController : NetworkBehaviour
    {
        public Vector3 inputDirection { get; set; }

        [SerializeField] InputActionReference _moveAction;
        [SerializeField] float _moveSpeed = 3f;
        

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            _moveAction.action.performed += OnMovePerformed;
        }

        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();

            _moveAction.action.performed -= OnMovePerformed;
        }

        void OnMovePerformed(InputAction.CallbackContext context)
        {
            Vector2 value = context.ReadValue<Vector2>();
            inputDirection = new Vector3(value.x, 0f, value.y);
        }

        private void FixedUpdate()
        {
            Move();
        }

        void Move()
        {
            Vector3 velocity = new Vector3(inputDirection.x, 0f, inputDirection.z).normalized * _moveSpeed;
            transform.Translate(velocity * Time.fixedDeltaTime);
        }
    }
}
