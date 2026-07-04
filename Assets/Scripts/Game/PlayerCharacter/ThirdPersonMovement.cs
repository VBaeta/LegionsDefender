using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public class ThirdPersonMovement : MonoBehaviour
{
    [SerializeField] private CharacterController controller;
    [SerializeField] private new Transform camera;
    [SerializeField] private Transform groundCheck;
    [SerializeField] private LayerMask groundMask;
    
    [SerializeField] private float moveSpeed = 6f;
    [SerializeField] private float rotationTime = 0.1f;
    
    [SerializeField] private float jumpHeight = 2.5f;
    private float _rotationSpeed;
    private Vector3 _velocity;
    private float _groundDistance = 0.2f;
    private float _gravity = -20f;
    private bool _isGrounded;
    void Update()
    {
        if (camera == null)
        {
            if (Camera.main != null)
            {
                camera = Camera.main.transform;
            }
            else
            {
                return;
            }
        }

        // Use parent pivot which sits flush on the ground for maximum reliability
        _isGrounded = Physics.CheckSphere(transform.position + new Vector3(0f, 0.15f, 0f), 0.25f, groundMask);
        
        if (_isGrounded && _velocity.y < 0)
        {
            _velocity.y = 0f;
        }
        
        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");
        Vector3 direction = new Vector3(horizontal, 0f, vertical).normalized;
        
        if (direction.magnitude >= 0.1f)
        {
            float targetAngle = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg + camera.eulerAngles.y;
            float smoothAngle = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetAngle, ref _rotationSpeed, rotationTime);
            transform.rotation = Quaternion.Euler(0f, smoothAngle, 0f);
            
            Vector3 moveDirection = Quaternion.Euler(0f, targetAngle, 0f) * Vector3.forward;
            controller.Move(moveDirection.normalized * (moveSpeed * Time.deltaTime));
        }
        
        if (Input.GetButtonDown("Jump") && _isGrounded)
        {
            _velocity.y = Mathf.Sqrt(jumpHeight * -2f * _gravity);
        }
        
        _velocity.y += _gravity * Time.deltaTime;
            
        controller.Move(_velocity * Time.deltaTime);
    }
}
