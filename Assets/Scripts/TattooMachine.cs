using UnityEngine;
using UnityEngine.Events;

public class TattooMachine : MonoBehaviour
{

    public void MoveTattooMachine(Vector2 DrawPos)
    {
        transform.position = new Vector2(DrawPos.x - 100, DrawPos.y - 100);
    }
}
