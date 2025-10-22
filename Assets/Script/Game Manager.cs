using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class GameManager : MonoBehaviour
{
    [Header("Gameplay Settings")]
    [Tooltip("Reference to the player controller script that should be enabled/disabled.")]
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

    [Header("UI Settings")]
    [Tooltip("Text component on the UI Canvas that displays the timer.")]
    public Text timerText;

    private void Awake()
    {
        // Validate references
        if(controller == null)
            Debug.LogError("Controller reference is not assigned in GameManager.");
        if(popUp == null)
            Debug.LogError("PopUp reference is not assigned in GameManager.");
        if(timerText == null)
            Debug.LogError("Timer Text reference is not assigned in GameManager.");
        timerText.text = "";
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

        // Show countdown using PopUp script
        bool countdownFinished = false;

        // Adjust PopUp's animation duration to match countdownDuration if needed
        popUp.animationDuration = countdownDuration / 4f; // Ready + 3 + 2 + 1 = 4 sprites total

        popUp.ShowCountdown(() => countdownFinished = true);

        // Wait until countdown finishes
        while(!countdownFinished) yield return null;

        // Optional delay after countdown
        if(postCountdownDelay > 0f)
            yield return new WaitForSeconds(postCountdownDelay);

        // Enable controller and play BGM
        controller.enabled = true;
        if(BGM != null)
            BGM.Play();

        // Start the gameplay timer
        float remainingTime = gameplayDuration;

        while(remainingTime > 0f)
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
    }
}