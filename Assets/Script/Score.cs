using UnityEngine;
using UnityEngine.UI;

/// Manages the scoring system for the bubble tea puzzle game.
/// Score calculation: (Base points per topping * chain count) + (physical length bonus) * combo multiplier
/// Combo multipliers: 3-5 chain = 1x, 6-8 chain = 2x, 9+ chain = 3x
public class ScoreManager : MonoBehaviour
{
    public static ScoreManager Instance { get; private set; }
    [Header("Score Settings")]
    [Tooltip("Base points awarded per topping removed.")]
    public int basePointsPerTopping = 10;
    [Tooltip("Points awarded per world unit of physical chain length.")]
    public int pointsPerUnitLength = 5;
    [Tooltip("Multiplier applied for chains of 6-8 toppings.")]
    public float comboMultiplier = 2f;
    [Tooltip("Multiplier applied for chains of 9+ toppings.")]
    public float superComboMultiplier = 3f;
    [Tooltip("Bonus points awarded when time extension occurs.")]
    public int timeExtensionBonus = 50;
    [Header("UI References")]
    [Tooltip("Text component displaying current score.")]
    public Text scoreText;
    [Tooltip("Text component showing points gained from last chain (optional).")]
    public Text pointsGainedText;
    [Tooltip("Duration to display points gained popup.")]
    public float pointsGainedDisplayDuration = 1.5f;
    [Header("Audio")]
    [Tooltip("Sound played when points are gained.")]
    public AudioClip scoreSound;
    [Tooltip("Volume for score sound.")]
    public float scoreSoundVolume = 0.5f;
    private int currentScore = 0;
    private AudioSource audioSource;

    private void Awake()
    {
        // Singleton pattern
        if(Instance != null && Instance != this)
        {
            Destroy(this.gameObject);
            return;
        }
        Instance = this;
        // Setup audio source
        audioSource = gameObject.GetComponent<AudioSource>();
        if(audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        // Initialise UI
        if(scoreText != null) scoreText.text = "0";
        if(pointsGainedText != null) pointsGainedText.gameObject.SetActive(false);
    }

    /// Calculate and add score based on chain count and physical length.
    /// New formula: (base * count) + (length * lengthBonus) then apply multiplier
    /// This ensures base score is substantial even for short distances
    public void AddScoreForChain(int chainCount, float physicalLength)
    {
        if(chainCount < 3) return;
        // Determine multiplier based on chain count
        float multiplier = 1f;
        if(chainCount >= 9)
        {
            multiplier = superComboMultiplier;
        } else if(chainCount >= 6)
        {
            multiplier = comboMultiplier;
        }
        // Calculate base score from topping count
        int baseScore = basePointsPerTopping * chainCount;
        // Calculate bonus from physical length
        int lengthBonus = Mathf.RoundToInt(physicalLength * pointsPerUnitLength);
        // Combine and apply multiplier
        int pointsGained = Mathf.RoundToInt((baseScore + lengthBonus) * multiplier);
        currentScore += pointsGained;
        // Update UI
        UpdateScoreDisplay();
        // Show points gained popup
        if(pointsGainedText != null)
        {
            StartCoroutine(ShowPointsGainedCoroutine(pointsGained));
        }
        // Play score sound
        if(scoreSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(scoreSound, scoreSoundVolume);
        }
    }

    /// Add bonus points for time extension
    public void AddTimeExtensionBonus()
    {
        currentScore += timeExtensionBonus;
        UpdateScoreDisplay();
        if(pointsGainedText != null)
        {
            StartCoroutine(ShowPointsGainedCoroutine(timeExtensionBonus, "+TIME BONUS! "));
        }
    }

    /// Add custom bonus points (for special features)
    public void AddBonusPoints(int points)
    {
        currentScore += points;
        UpdateScoreDisplay();
    }

    /// Get current score value
    public int GetCurrentScore()
    {
        return currentScore;
    }

    /// Reset score to zero (for new game)
    public void ResetScore()
    {
        currentScore = 0;
        UpdateScoreDisplay();
    }

    // Update the score text display
    private void UpdateScoreDisplay()
    {
        if(scoreText != null)
        {
            scoreText.text = currentScore.ToString();
        }
    }

    // Coroutine to show temporary points gained popup
    private System.Collections.IEnumerator ShowPointsGainedCoroutine(int points, string prefix = "+")
    {
        if(pointsGainedText == null) yield break;
        // Set text and show
        pointsGainedText.text = prefix + points.ToString();
        pointsGainedText.gameObject.SetActive(true);
        // Animate scale and fade
        float elapsed = 0f;
        Vector3 initialScale = pointsGainedText.transform.localScale;
        Color initialColour = pointsGainedText.color;
        while(elapsed < pointsGainedDisplayDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / pointsGainedDisplayDuration;
            // Scale up then down
            float scaleFactor = Mathf.Sin(t * Mathf.PI) * 0.3f + 1f;
            pointsGainedText.transform.localScale = initialScale * scaleFactor;
            // Fade out towards end
            Color colour = initialColour;
            colour.a = Mathf.Lerp(1f, 0f, Mathf.Clamp01((t - 0.5f) * 2f));
            pointsGainedText.color = colour;
            yield return null;
        }
        // Reset and hide
        pointsGainedText.transform.localScale = initialScale;
        pointsGainedText.color = initialColour;
        pointsGainedText.gameObject.SetActive(false);
    }
}