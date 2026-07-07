using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using UnityEngine.InputSystem;

public class PlayerScript : MonoBehaviour
{
    [SerializeField] private float _speed; // allows variable to be set within unity itself 
    [SerializeField] private float _rotationSpeed; // how fast the character rotates (ccould vary by movement type , maybej)
    private Rigidbody2D _rigidbody; // variables
    private Vector2 _movementInput; // the movement input
    private Vector2 _smoothedMovementInput;
    private Vector2 _movementInputSmoothVelocity; 

    private void Awake(){ // entry point , called on object instantiating (when gameplay begins)
        _rigidbody = GetComponent<Rigidbody2D>();
    }

    private void FixedUpdate() // runs every 0.02 seconds , loop 
    // Update method triggers every rendered frame, this ones more stable (so i heard)
    {
        SetPlayerVelocity();
        RotateInDirectionOfInput();
    }

    private void SetPlayerVelocity() // sets dynamic movement
    {
        _smoothedMovementInput = Vector2.SmoothDamp( // adds smoother movement 
            _smoothedMovementInput,
            _movementInput,
            ref _movementInputSmoothVelocity,
            0.1f // changes every 0.1 seconds
        );
        _rigidbody.linearVelocity = _smoothedMovementInput * _speed; // speed scales the vector 
    }

    private void RotateInDirectionOfInput() // rotates direction to input
    {
        if(_movementInput != Vector2.zero) // if char moves 
        {
            // odd stuff
            Quaternion targetRotation = Quaternion.LookRotation(transform.forward,_smoothedMovementInput);
            Quaternion rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, _rotationSpeed * Time.deltaTime);  

            _rigidbody.MoveRotation(rotation);
        }
    }

    private void OnMove(InputValue inputValue)
    {
        _movementInput =  inputValue.Get<Vector2>(); // gets the vector of the input (x y pair) 

    }
}
