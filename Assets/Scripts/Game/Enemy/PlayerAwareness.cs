using UnityEngine;

public class PlayerAwareness : MonoBehaviour
{
    public bool AwareOfPlayer {get; private set;} // public getter, private setter
    public Vector2 DirectionToPlayer {get; private set;} 

    [SerializeField] private float _playerAwarenessDistance;

    private Transform _player;



    private void Awake() // on load 
    {
        _player = FindFirstObjectByType<PlayerScript>().transform;
    }

    void Update()
    {
        Vector2 enemyToPlayerVector = _player.position - transform.position;
        DirectionToPlayer = enemyToPlayerVector.normalized;

        AwareOfPlayer = enemyToPlayerVector.magnitude <= _playerAwarenessDistance;
    }


}
