using UnityEngine;
using TMPro;

public class LevelTimerManager : MonoBehaviour
{
    [Header("Timer Settings")]
    public float totalTime = 120f;

    [Header("Player")]
    public Transform player;

    [Header("Environment Reference")]
    public GameObject environmentObject;

    [Tooltip("Extra safe area outside the environment before game over happens.")]
    public float fallExtraDistance = 5f;

    [Header("UI")]
    public TMP_Text timerText;
    public GameObject gameOverScreen;
    public GameObject levelCompletedScreen;

    [Header("Box Layer")]
    public string boxLayerName = "Box";

    private float currentTime;
    private bool levelEnded = false;
    private int boxLayer;

    private Bounds environmentBounds;
    private bool hasEnvironmentBounds = false;

    private void Start()
    {
        currentTime = totalTime;
        boxLayer = LayerMask.NameToLayer(boxLayerName);

        CalculateEnvironmentBounds();

        if (gameOverScreen != null)
            gameOverScreen.SetActive(false);

        if (levelCompletedScreen != null)
            levelCompletedScreen.SetActive(false);

        Time.timeScale = 1f;
        UpdateTimerUI();
    }

    private void Update()
    {
        if (levelEnded) return;

        UpdateTimer();
        CheckPlayerOutsideEnvironment();
        CheckAllBoxesDestroyed();
    }

    private void CalculateEnvironmentBounds()
    {
        if (environmentObject == null)
        {
            Debug.LogWarning("Environment Object is not assigned.");
            hasEnvironmentBounds = false;
            return;
        }

        Renderer[] renderers = environmentObject.GetComponentsInChildren<Renderer>();

        if (renderers.Length == 0)
        {
            Debug.LogWarning("Environment Object has no Renderer. Cannot calculate environment bounds.");
            hasEnvironmentBounds = false;
            return;
        }

        environmentBounds = renderers[0].bounds;

        for (int i = 1; i < renderers.Length; i++)
        {
            environmentBounds.Encapsulate(renderers[i].bounds);
        }

        environmentBounds.Expand(fallExtraDistance * 2f);

        hasEnvironmentBounds = true;
    }

    private void CheckPlayerOutsideEnvironment()
    {
        if (player == null) return;
        if (!hasEnvironmentBounds) return;

        if (!environmentBounds.Contains(player.position))
        {
            GameOver();
        }
    }

    private void UpdateTimer()
    {
        currentTime -= Time.deltaTime;

        if (currentTime <= 0f)
        {
            currentTime = 0f;
            UpdateTimerUI();
            GameOver();
            return;
        }

        UpdateTimerUI();
    }

    private void UpdateTimerUI()
    {
        if (timerText == null) return;

        int minutes = Mathf.FloorToInt(currentTime / 60f);
        int seconds = Mathf.FloorToInt(currentTime % 60f);

        timerText.text = minutes.ToString("00") + ":" + seconds.ToString("00");
    }

    private void CheckAllBoxesDestroyed()
    {
        if (boxLayer == -1)
        {
            Debug.LogWarning("Box layer does not exist. Please create a layer named: " + boxLayerName);
            return;
        }

        GameObject[] allObjects = FindObjectsOfType<GameObject>();

        foreach (GameObject obj in allObjects)
        {
            if (obj.layer == boxLayer)
            {
                return;
            }
        }

        LevelCompleted();
    }

    private void GameOver()
    {
        if (levelEnded) return;

        levelEnded = true;

        if (gameOverScreen != null)
            gameOverScreen.SetActive(true);

        if (levelCompletedScreen != null)
            levelCompletedScreen.SetActive(false);

        Time.timeScale = 0f;
    }

    private void LevelCompleted()
    {
        if (levelEnded) return;

        levelEnded = true;

        if (levelCompletedScreen != null)
            levelCompletedScreen.SetActive(true);

        if (gameOverScreen != null)
            gameOverScreen.SetActive(false);

        Time.timeScale = 0f;
    }

    private void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying) return;
        if (!hasEnvironmentBounds) return;

        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(environmentBounds.center, environmentBounds.size);
    }
}