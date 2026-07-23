using UnityEngine;

namespace Game.Systems
{
    public class CursorController : MonoBehaviour
    {
        [SerializeField] private Texture2D cursor;
        [SerializeField] private Vector2 hotspot = Vector2.zero;

        private void Start()
        {
            Cursor.SetCursor(cursor, hotspot, CursorMode.Auto);
        }
    }
}
