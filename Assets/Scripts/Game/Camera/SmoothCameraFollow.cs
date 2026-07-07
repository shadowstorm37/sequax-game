using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class SmoothCameraFollow : MonoBehaviour
{
    [SerializeField] private Vector3 offset; // offset , default 0
    [SerializeField] private float damping; // how fast camera catches to player

    public Transform target; // target is player (can change later)
    private Vector3 vel = Vector3.zero;

    private void FixedUpdate()
    {
        Vector3 targetPosition = target.position + offset; 
        targetPosition.z = transform.position.z; 
        transform.position = Vector3.SmoothDamp(transform.position, targetPosition, ref vel, damping);
    }
}
