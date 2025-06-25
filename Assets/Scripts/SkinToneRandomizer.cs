using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class SkinToneRandomizer : MonoBehaviour
{
    [SerializeField] List<Sprite> skinTones;
    [SerializeField] private SpriteRenderer spriteRenderer;

    void Awake()
    {
        spriteRenderer.sprite = skinTones[Random.Range(0, skinTones.Count)];
    }


}
