using UnityEngine;
using System.Collections;
using System.IO;

[System.Serializable]
public struct ValidationResult
{
    public float coverage;           // 0-1, percentage of reference tattoo covered
    public float accuracy;          // 0-1, accuracy of the drawing
    public float outsidePenalty;    // 0-1, penalty for drawing outside reference
    public float finalScore;        // 0-1, final score after penalties
    public int correctPixels;       // Debug: pixels drawn correctly
    public int incorrectPixels;     // Debug: pixels drawn outside reference
    public int totalReferencePixels; // Debug: total pixels in reference
}