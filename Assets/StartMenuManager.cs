using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Controla a tela inicial do jogo: uma única imagem de fundo (com título,
/// raposinha e opções de idade já desenhados) com 3 botões INVISÍVEIS por cima
/// das áreas de idade. Tocar numa idade já define a dificuldade inicial e
/// libera o jogo (o toque para plantar a raposinha em AR).
/// </summary>
public class StartMenuManager : MonoBehaviour
{
    [Header("Referências de lógica")]
    public MathProblemGenerator problemGenerator;
    public ARFoxPlacer foxPlacer;
    public CreatureSpawner creatureSpawner;

    [Header("Painel da tela inicial (a imagem de fundo)")]
    public GameObject startMenuPanel;

    [Header("Borda decorativa (só aparece depois de começar o jogo)")]
    public GameObject gameplayBorder;

    [Header("Botões invisíveis sobre cada faixa etária da imagem")]
    public Button youngAgeButton;   // sobre "6-7 anos" -> dificuldade 1
    public Button middleAgeButton;  // sobre "8-9 anos" -> dificuldade 2
    public Button olderAgeButton;   // sobre "10-12 anos" -> dificuldade 3

    private void Awake()
    {
        if (startMenuPanel != null) startMenuPanel.SetActive(true);
        if (gameplayBorder != null) gameplayBorder.SetActive(false);

        // Trava o toque de plantar a raposinha até o menu terminar
        if (foxPlacer != null)
            foxPlacer.inputEnabled = false;
    }

    private void OnEnable()
    {
        if (youngAgeButton != null)
            youngAgeButton.onClick.AddListener(() => SelectAgeGroup(1));
        if (middleAgeButton != null)
            middleAgeButton.onClick.AddListener(() => SelectAgeGroup(2));
        if (olderAgeButton != null)
            olderAgeButton.onClick.AddListener(() => SelectAgeGroup(3));
    }

    /// <summary>
    /// Chamado ao tocar numa das faixas etárias da imagem. Define a dificuldade
    /// inicial e libera o jogo (o toque para plantar a raposinha em AR).
    /// </summary>
    public void SelectAgeGroup(int startingDifficulty)
    {
        if (problemGenerator != null)
            problemGenerator.currentDifficulty = Mathf.Clamp(startingDifficulty, 1, 5);

        if (startMenuPanel != null) startMenuPanel.SetActive(false);
        if (gameplayBorder != null) gameplayBorder.SetActive(true);

        if (foxPlacer != null)
            foxPlacer.inputEnabled = true;

        if (creatureSpawner != null)
            creatureSpawner.StartSpawning();
    }
}