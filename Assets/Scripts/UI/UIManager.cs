using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    [Header("HUD")]
    [SerializeField] private TextMeshProUGUI keysText;
    [SerializeField] private Slider staminaSlider;
    [SerializeField] private PlayerController player;

    [Header("Pantallas finales")]
    [SerializeField] private GameObject winPanel;
    [SerializeField] private GameObject losePanel;

    private GameManager gameManager;

    private void Start()
    {
        gameManager = GameManager.Instance;

        if (winPanel != null)
        {
            winPanel.SetActive(false);
        }

        if (losePanel != null)
        {
            losePanel.SetActive(false);
        }

        if (gameManager == null)
        {
            Debug.LogWarning("UIManager: no hay GameManager en la escena.");
            return;
        }

        gameManager.OnKeysChanged += UpdateKeys;
        gameManager.OnStateChanged += HandleStateChanged;
        UpdateKeys(gameManager.KeysCollected, gameManager.TotalKeys);
    }

    private void OnDestroy()
    {
        if (gameManager == null)
        {
            return;
        }

        gameManager.OnKeysChanged -= UpdateKeys;
        gameManager.OnStateChanged -= HandleStateChanged;
    }

    private void Update()
    {
        if (staminaSlider != null && player != null)
        {
            staminaSlider.value = player.Stamina01;
        }
    }

    private void UpdateKeys(int collected, int total)
    {
        if (keysText != null)
        {
            keysText.text = "Llaves: " + collected + "/" + total;
        }
    }

    private void HandleStateChanged(GameManager.GameState state)
    {
        if (state == GameManager.GameState.Won && winPanel != null)
        {
            winPanel.SetActive(true);
        }
        else if (state == GameManager.GameState.Lost && losePanel != null)
        {
            losePanel.SetActive(true);
        }
    }

    public void RestartGame()
    {
        if (gameManager != null)
        {
            gameManager.Restart();
        }
    }
}