/*using UnityEngine;

namespace TattooSystem
{
    [System.Serializable]
    public struct ValidationResult
    {
        [Header("Validation Metrics")]
        [Range(0f, 1f)]
        public float coverage;           // 0-1, percentage of reference tattoo covered
        
        [Range(0f, 1f)]
        public float accuracy;          // 0-1, accuracy of the drawing
        
        [Range(0f, 1f)]
        public float outsidePenalty;    // 0-1, penalty for drawing outside reference
        
        [Range(0f, 1f)]
        public float finalScore;        // 0-1, final score after penalties
        
        [Header("Debug Information")]
        public int correctPixels;       // Debug: pixels drawn correctly
        public int incorrectPixels;     // Debug: pixels drawn outside reference
        public int totalReferencePixels; // Debug: total pixels in reference

        public float GetCoveragePercentage() => coverage * 100f;
        public float GetAccuracyPercentage() => accuracy * 100f;
        public float GetPenaltyPercentage() => outsidePenalty * 100f;
        public float GetFinalScorePercentage() => finalScore * 100f;

        public string GetFormattedResults()
        {
            return $"Coverage: {GetCoveragePercentage():F1}% | " +
                   $"Accuracy: {GetAccuracyPercentage():F1}% | " +
                   $"Penalty: {GetPenaltyPercentage():F1}% | " +
                   $"Score: {GetFinalScorePercentage():F1}%";
        }

        public string GetDetailedDebugInfo()
        {
            return $"Correct Pixels: {correctPixels} | " +
                   $"Incorrect Pixels: {incorrectPixels} | " +
                   $"Total Reference Pixels: {totalReferencePixels}";
        }

        public ValidationQuality GetQualityLevel()
        {
            if (finalScore >= 0.9f) return ValidationQuality.Excellent;
            if (finalScore >= 0.8f) return ValidationQuality.Good;
            if (finalScore >= 0.6f) return ValidationQuality.Average;
            if (finalScore >= 0.4f) return ValidationQuality.Poor;
            return ValidationQuality.Failed;
        }

        public bool IsPassingScore(float passingThreshold = 0.6f)
        {
            return finalScore >= passingThreshold;
        }
    }

    public enum ValidationQuality
    {
        Failed,
        Poor,
        Average,
        Good,
        Excellent
    }
}*/