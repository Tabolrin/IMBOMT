using UnityEngine;
using UnityEngine.Events;
using System;

public class TattooMachine : MonoBehaviour
{
    [SerializeField] public float offset;
    [SerializeField] public float shakeOffset;

    private int degreeMax = 360;

    public void MoveTattooMachine(Vector2 DrawPos)
    {
        this.gameObject.SetActive(true);

        double degree = UnityEngine.Random.Range(0, degreeMax);
        transform.position = new Vector2((DrawPos.x + offset) + (float)Math.Sin(degree) * shakeOffset,
                                        (DrawPos.y - offset) + (float)Math.Cos(degree) * shakeOffset);
    }

    public void HideTattooMachine()
    {
        this.gameObject.SetActive(false);
    }
}
