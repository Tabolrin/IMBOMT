using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "SpriteContainer", menuName = "ScriptableObjects/SpriteContainer", order = 1)]
public class SpriteContainer : ScriptableObject
{
    [SerializeField] private List<Sprite> sprites;

    public Sprite GetRandomSprite() { return sprites[Random.Range(0, sprites.Count)]; }
    
    public Sprite GetSprite(int index)
    {
        if (index < 0 || index >= sprites.Count)
        {
            Debug.LogError("Index out of bounds for pattern collection.");
            return null;
        }
        
        return sprites[index];
    }
}
