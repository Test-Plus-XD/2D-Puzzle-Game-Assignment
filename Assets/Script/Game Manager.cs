using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    [Header("Gameplay Settings")]
    [Tooltip("Reference to the player controller script that should be enabled or disabled.")]
    public Controller controller;
    [Tooltip("Duration of the main gameplay timer in seconds.")]
    public float gameplayDuration = 60f;
    [Tooltip("Number of decimal places to display on the gameplay timer.")]
    [Range(0, 5)]
    public int timerDecimals = 3;
    [Header("Countdown Settings")]
    [Tooltip("Reference to the PopUp script that handles the pre-game countdown.")]
    public PopUp popUp;
    [Tooltip("Duration of the pre-game countdown in seconds (default is 4).")]
    public float countdownDuration = 4f;
    [Tooltip("Optional delay after countdown before gameplay starts.")]
    public float postCountdownDelay = 0f;
    [Header("Audio Settings")]
    [Tooltip("Background music to play once gameplay starts.")]
    public AudioSource BGM;
    [Tooltip("Sound played when time is extended.")]
    public AudioClip timeExtensionSound;
    [Tooltip("Volume for time extension sound.")]
    public float timeExtensionVolume = 0.8f;
    [Header("UI Settings")]
    [Tooltip("Text component on the UI Canvas that displays the timer.")]
    public Text timerText;
    [Tooltip("Panel or GameObject to show at game start (optional).")]
    public GameObject startPanel;
    [Tooltip("Panel or GameObject to show during gameplay (optional).")]
    public GameObject gameplayPanel;
    [Tooltip("Panel or GameObject to show when time is up.")]
    public GameObject timesUpPanel;
    [Tooltip("Text component showing final score on times up panel.")]
    public Text finalScoreText;
    [Tooltip("Button to restart game.")]
    public Button restartButton;
    private float remainingTime;
    private bool gameActive = false;
    private AudioSource audioSource;

    private void Awake()
    {
        // Validate references
        if (controller == null)
            Debug.LogError("Controller reference is not assigned in GameManager.");
        if (popUp == null)
            Debug.LogError("PopUp reference is not assigned in GameManager.");
        if (timerText == null)
            Debug.LogError("Timer Text reference is not assigned in GameManager.");
        // Setup audio source for time extension sound
        audioSource = gameObject.GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        // Initialise UI panels
        if (startPanel != null) startPanel.SetActive(true);
        if (gameplayPanel != null) gameplayPanel.SetActive(false);
        if (timesUpPanel != null) timesUpPanel.SetActive(false);
        timerText.text = "";
        // Setup restart button
        if (restartButton != null)
        {
            restartButton.onClick.AddListener(RestartGame);
        }
    }

    private void Start()
    {
        // Begin the game sequence coroutine
        StartCoroutine(GameSequence());
    }

    private IEnumerator GameSequence()
    {
        // Disable controller at the start
        controller.enabled = false;
        // Hide start panel after a moment
        if (startPanel != null)
        {
            yield return new WaitForSeconds(0.5f);
            startPanel.SetActive(false);
        }
        // Show countdown using PopUp script
        bool countdownFinished = false;
        // Adjust PopUp's animation duration to match countdownDuration if needed
        popUp.animationDuration = countdownDuration / 4f;
        popUp.ShowCountdown(() => countdownFinished = true);
        // Wait until countdown finishes
        while (!countdownFinished) yield return null;
        // Optional delay after countdown
        if (postCountdownDelay > 0f)
            yield return new WaitForSeconds(postCountdownDelay);
        // Show gameplay panel
        if (gameplayPanel != null) gameplayPanel.SetActive(true);
        // Enable controller and play BGM
        controller.enabled = true;
        if (BGM != null)
            BGM.Play();
        // Initialise score manager
        if (ScoreManager.Instance != null)
        {
            ScoreManager.Instance.ResetScore();
        }
        // Start the gameplay timer
        remainingTime = gameplayDuration;
        gameActive = true;
        while (remainingTime > 0f && gameActive)
        {
            // Update timer text with specified decimals
            timerText.text = remainingTime.ToString("F" + timerDecimals);
            // Smoothly change colour from green to red as time decreases
            timerText.color = Color.Lerp(Color.red, Color.green, remainingTime / gameplayDuration);
            remainingTime -= Time.deltaTime;
            yield return null;
        }
        // Ensure timer shows 0 at the end
        timerText.text = 0f.ToString("F" + timerDecimals);
        timerText.color = Color.red;
        // Disable controller after gameplay ends
        controller.enabled = false;
        gameActive = false;
        // Fade out BGM
        if (BGM != null)
        {
            StartCoroutine(FadeOutBGM(1f));
        }
        // Hide gameplay panel
        if (gameplayPanel != null) gameplayPanel.SetActive(false);
        // Show times up panel with final score
        yield return new WaitForSeconds(0.5f);
        ShowTimesUpPanel();
    }

    /// Extend gameplay time (called by Logic when combo threshold reached)
    public void ExtendTime(float seconds)
    {
        if (!gameActive) return;
        remainingTime += seconds;
        // Clamp to maximum to prevent excessive extensions
        remainingTime = Mathf.Min(remainingTime, gameplayDuration * 1.5f);
        // Play time extension sound
        if (timeExtensionSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(timeExtensionSound, timeExtensionVolume);
        }
        // Show visual feedback (flash timer text)
        StartCoroutine(FlashTimerCoroutine());
    }

    // Flash timer text to indicate time extension
    private IEnumerator FlashTimerCoroutine()
    {
        Color originalColour = timerText.color;
        for (int i = 0; i < 3; i++)
        {
            timerText.color = Color.cyan;
            yield return new WaitForSeconds(0.1f);
            timerText.color = originalColour;
            yield return new WaitForSeconds(0.1f);
        }
    }

    // Show times up panel with final score and restart option
    private void ShowTimesUpPanel()
    {
        if (timesUpPanel != null)
        {
            timesUpPanel.SetActive(true);
            // Animate panel entrance
            StartCoroutine(AnimateTimesUpPanelCoroutine());
        }
        // Display final score
        if (finalScoreText != null && ScoreManager.Instance != null)
        {
            int finalScore = ScoreManager.Instance.GetCurrentScore();
            finalScoreText.text = "FINAL SCORE: " + finalScore.ToString();
        }
    }

    // Animate times up panel entrance
    private IEnumerator AnimateTimesUpPanelCoroutine()
    {
        if (timesUpPanel == null) yield break;
        // Start scaled down
        timesUpPanel.transform.localScale = Vector3.zero;
        float duration = 0.5f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            // Bounce in effect
            float scale = Mathf.Sin(t * Mathf.PI * 0.5f);
            timesUpPanel.transform.localScale = Vector3.one * scale;
            yield return null;
        }
        timesUpPanel.transform.localScale = Vector3.one;
    }

    // Restart game by reloading scene
    public void RestartGame()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    // Fade out background music smoothly
    private IEnumerator FadeOutBGM(float duration)
    {
        if (BGM == null) yield break;
        float startVolume = BGM.volume;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            BGM.volume = Mathf.Lerp(startVolume, 0f, elapsed / duration);
            yield return null;
        }
        BGM.Stop();
        BGM.volume = startVolume;
    }
}