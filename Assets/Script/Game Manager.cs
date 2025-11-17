using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameManager : MonoBehaviour
{
    [Header("Gameplay Settings")]
    [Tooltip("Reference to the player controller script that should be enabled or disabled.")]
    public Controller controller;
    [Tooltip("Reference to the topping spawner script that should be enabled or disabled.")]
    public Spawner spawner;
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
    [Tooltip("Background music for start and times up screens.")]
    public AudioSource menuBGM;
    [Tooltip("Background music to play during gameplay.")]
    public AudioSource gameplayBGM;
    [Tooltip("Fade duration for menu BGM transitions.")]
    public float menuBGMFadeDuration = 1f;
    [Tooltip("Sound played when time is extended.")]
    public AudioClip timeExtensionSound;
    [Tooltip("Volume for time extension sound.")]
    public float timeExtensionVolume = 0.8f;
    [Header("Background Settings")]
    [Tooltip("UI Image that will have its colour synced to the remaining time.")]
    public SpriteRenderer backgroundImage;
    [Tooltip("Colour when time is full (255,255,255).")]
    public Color32 backgroundColourFull = new Color32(255, 255, 255, 255);
    [Tooltip("Colour when time is empty (155,155,155).")]
    public Color32 backgroundColourEmpty = new Color32(155, 155, 155, 255);
    [Header("UI Settings")]
    [Tooltip("Panel or GameObject to show at game start.")]
    public GameObject startPanel;
    [Tooltip("Button on start panel to begin game.")]
    public Button startButton;
    [Tooltip("Panel or GameObject to show during gameplay.")]
    public GameObject gameplayPanel;
    [Tooltip("Text component on the UI Canvas that displays the timer.")]
    public Text timerText;
    [Tooltip("Panel or GameObject to show when time is up.")]
    public GameObject timesUpPanel;
    [Tooltip("Text component showing final score on times up panel.")]
    public Text finalScoreText;
    [Tooltip("Button to restart game.")]
    public Button restartButton;
    private float remainingTime;
    private bool gameActive = false;
    private AudioSource audioSource;
    private float BGMOriginalVolume;

    private void Awake()
    {
        // Validate references
        if(controller == null) Debug.LogError("Controller reference is not assigned in GameManager.");
        if(spawner == null) Debug.LogError("Spawner reference is not assigned in GameManager.");
        if(popUp == null) Debug.LogError("PopUp reference is not assigned in GameManager.");
        if(timerText == null) Debug.LogError("Timer Text reference is not assigned in GameManager.");
        // Setup audio source for time extension sound
        audioSource = gameObject.GetComponent<AudioSource>();
        if(audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        BGMOriginalVolume = gameplayBGM.volume;   // Stores the Inspector value
        // Initialise UI panels
        if(startPanel != null) startPanel.SetActive(true);
        if(gameplayPanel != null) gameplayPanel.SetActive(false);
        if(timesUpPanel != null) timesUpPanel.SetActive(false);
        timerText.text = "";
        // Setup button listeners
        if(startButton != null) startButton.onClick.AddListener(OnStartButtonClicked);
        if(restartButton != null) restartButton.onClick.AddListener(RestartGame);
        // Start menu BGM with fade in
        if(menuBGM != null)
        {
            menuBGM.volume = 0f;
            menuBGM.loop = true;
            menuBGM.Play();
            StartCoroutine(FadeInAudioSource(menuBGM, menuBGMFadeDuration, BGMOriginalVolume));
        }
    }

    private void Start()
    {
        // Disable controller at start
        if(controller != null)
        {
            controller.enabled = false;
            controller.gameObject.SetActive(false);
        }
    }

    private void LateUpdate()
    {
        // Prevent divide-by-zero and early errors.
        if(gameplayDuration <= 0f) return;
        // Compute normalised percentage of remaining time in [0,1].
        float percentage = Mathf.Clamp01(remainingTime / gameplayDuration);
        Color32 newColour = Color32.Lerp(backgroundColourEmpty, backgroundColourFull, percentage);
        // Apply to background image if assigned.
        if(backgroundImage != null) backgroundImage.color = newColour;
    }

    // Called when player clicks start button
    private void OnStartButtonClicked()
    {
        // Disable start button to prevent multiple clicks
        if(startButton != null) startButton.interactable = false;
        // Begin the game sequence
        StartCoroutine(GameSequence());
    }

    private IEnumerator GameSequence()
    {
        spawner.EnableGameplay();
        // Enable controller for initial setup
        controller.enabled = true;
        controller.gameObject.SetActive(true);
        // Fade out menu BGM
        if(menuBGM != null) yield return StartCoroutine(FadeOutAudioSource(menuBGM, menuBGMFadeDuration));
        // Disable controller during countdown
        controller.enabled = false;
        // Hide start panel
        if(startPanel != null) startPanel.SetActive(false);
        // Show countdown using PopUp script
        bool countdownFinished = false;
        popUp.animationDuration = countdownDuration / 4f;
        // Start countdown popup (Ready / 3 / 2 / 1)
        popUp.ShowCountdown(() => countdownFinished = true);
        // Start gameplay BGM immediately when popup appears
        if(gameplayBGM != null)
        {
            gameplayBGM.loop = true;
            gameplayBGM.Play();
        }
        // Wait until countdown finishes
        while(!countdownFinished) yield return null;
        // Delay after countdown
        if(postCountdownDelay > 0f) yield return new WaitForSeconds(postCountdownDelay);
        // Show gameplay panel
        if(gameplayPanel != null) gameplayPanel.SetActive(true);
        // Enable controller
        controller.enabled = true;
        // Initialise score manager
        if(ScoreManager.Instance != null) ScoreManager.Instance.ResetScore();
        // Start the gameplay timer
        remainingTime = gameplayDuration;
        gameActive = true;
        while(remainingTime > 0f && gameActive)
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
        // Stop gameplay BGM immediately (no fade for immediate stop feel)
        if(gameplayBGM != null) gameplayBGM.Stop();
        // Hide gameplay panel
        if(gameplayPanel != null) gameplayPanel.SetActive(false);
        // Start menu BGM with fade in
        if(menuBGM != null)
        {
            menuBGM.volume = 0f;
            menuBGM.Play();
            StartCoroutine(FadeInAudioSource(menuBGM, menuBGMFadeDuration, BGMOriginalVolume));
        }
        // Show 'Time's Up' popup first, then display the times up panel when the popup finishes.
        if(popUp != null)
        {
            popUp.ShowTimesUp(() =>
            {
                // Callback invoked after the popup animation completes.
                ShowTimesUpPanel();
            });
        } else
        {
            // Fallback behaviour if popUp is not assigned
            yield return new WaitForSeconds(0.5f);
            ShowTimesUpPanel();
        }
    }

    /// Extend gameplay time (called by Logic when combo threshold reached)
    public void ExtendTime(float seconds)
    {
        if(!gameActive) return;
        remainingTime += seconds;
        // Clamp to maximum to prevent excessive extensions
        remainingTime = Mathf.Min(remainingTime, gameplayDuration * 1.5f);
        // Play time extension sound
        if(timeExtensionSound != null && audioSource != null)
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
        for(int i = 0; i < 3; i++)
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
        // Disable controller
        if(controller != null)
        {
            controller.enabled = false;
            controller.gameObject.SetActive(false);
        }
        if(timesUpPanel != null)
        {
            timesUpPanel.SetActive(true);
            // Animate panel entrance
            StartCoroutine(AnimateTimesUpPanelCoroutine());
        }
        // Display final score
        if(finalScoreText != null && ScoreManager.Instance != null)
        {
            int finalScore = ScoreManager.Instance.GetCurrentScore();
            finalScoreText.text = "FINAL SCORE: " + finalScore.ToString();
        }
    }

    // Animate times up panel entrance
    private IEnumerator AnimateTimesUpPanelCoroutine()
    {
        if(timesUpPanel == null) yield break;
        // Start scaled down
        timesUpPanel.transform.localScale = Vector3.zero;
        float duration = 0.5f;
        float elapsed = 0f;
        while(elapsed < duration)
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

    // Fade in audio source from zero to target volume
    private IEnumerator FadeInAudioSource(AudioSource source, float duration, float targetVolume)
    {
        if(source == null) yield break;
        float elapsed = 0f;
        while(elapsed < duration)
        {
            elapsed += Time.deltaTime;
            source.volume = Mathf.Lerp(0f, targetVolume, elapsed / duration);
            yield return null;
        }
        source.volume = targetVolume;
    }

    // Fade out audio source from current volume to zero
    private IEnumerator FadeOutAudioSource(AudioSource source, float duration)
    {
        if(source == null) yield break;
        float startVolume = source.volume;
        float elapsed = 0f;
        while(elapsed < duration)
        {
            elapsed += Time.deltaTime;
            source.volume = Mathf.Lerp(startVolume, 0f, elapsed / duration);
            yield return null;
        }
        source.Stop();
        source.volume = startVolume;
    }
}