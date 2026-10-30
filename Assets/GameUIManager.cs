using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Componente de UI "burro": mostra uma pergunta, recebe a resposta, mostra feedback
/// (com passo a passo se errar) e avisa quem pediu a pergunta (via callback) se acertou
/// ou errou. Não decide mais sozinho o que fazer depois — isso agora é responsabilidade
/// de quem chama AskQuestion (o CreatureCaptureController), para dar suporte à lógica
/// de "3 perguntas, 2 chances cada" da captura de bichinhos.
/// </summary>
public class GameUIManager : MonoBehaviour
{
    [Header("Referência de lógica")]
    public MathProblemGenerator problemGenerator;

    [Header("UI - Pergunta")]
    public GameObject questionPanel;
    public TMP_Text scenarioText;
    public TMP_Text questionText;

    [Header("UI - Resposta em alternativas")]
    public GameObject multipleChoiceGroup;
    public Button[] answerOptionButtons;

    [Header("UI - Resposta de comparação (maior/menor)")]
    public GameObject comparisonAnswerGroup;
    public Button firstOptionButton;
    public Button secondOptionButton;
    public Button tieButton;

    [Header("UI - Feedback")]
    public GameObject feedbackPanel;
    public TMP_Text feedbackVerdictText;
    public TMP_Text feedbackExplanationText;

    [Tooltip("Opcional: imagem 'Muito bem!' (o script liga só quando a criança acerta)")]
    public GameObject correctFeedbackImage;

    [Tooltip("Opcional: imagem 'Quase lá!' (o script liga só quando a criança erra)")]
    public GameObject wrongFeedbackImage;

    public float feedbackDisplaySeconds = 2f;

    [Tooltip("Tempo extra na tela quando a criança erra, para dar tempo de ler a explicação")]
    public float explanationExtraSeconds = 3f;

    [Header("UI - Pontuação")]
    public TMP_Text scoreText;
    public int pointsPerCorrectAnswer = 10;
    public int pointsLostPerWrongAnswer = 3;

    private int _score = 0;
    private MathProblemGenerator.MathProblem _currentProblem;
    private Animator _currentCreatureAnimator;
    private Action<bool> _onAnswered;
    private bool _waitingForAnswer = false;

    private void Awake()
    {
        if (feedbackPanel != null) feedbackPanel.SetActive(false);
        if (questionPanel != null) questionPanel.SetActive(false);
        UpdateScoreUI();
    }

    private void OnEnable()
    {
        if (firstOptionButton != null)
            firstOptionButton.onClick.AddListener(() => SubmitAnswer(1 == _currentProblem.correctAnswer));
        if (secondOptionButton != null)
            secondOptionButton.onClick.AddListener(() => SubmitAnswer(-1 == _currentProblem.correctAnswer));
        if (tieButton != null)
            tieButton.onClick.AddListener(() => SubmitAnswer(0 == _currentProblem.correctAnswer));
    }

    /// <summary>
    /// Pede pra UI mostrar uma pergunta. Quando a criança responder, "onAnswered" é
    /// chamado com true (acertou) ou false (errou). creatureAnimator é opcional, só
    /// pra disparar a animação de reação do bichinho certo (cada bichinho tem o seu).
    /// </summary>
    public void AskQuestion(MathProblemGenerator.MathProblem problem, Animator creatureAnimator, Action<bool> onAnswered)
    {
        _currentProblem = problem;
        _currentCreatureAnimator = creatureAnimator;
        _onAnswered = onAnswered;
        _waitingForAnswer = true;

        if (questionPanel != null) questionPanel.SetActive(true);
        if (feedbackPanel != null) feedbackPanel.SetActive(false);

        scenarioText.text = problem.scenarioText;
        questionText.text = problem.questionText;

        bool isComparison = problem.operation == MathProblemGenerator.OperationType.Comparison;
        if (multipleChoiceGroup != null) multipleChoiceGroup.SetActive(!isComparison);
        if (comparisonAnswerGroup != null) comparisonAnswerGroup.SetActive(isComparison);

        if (!isComparison)
            SetupMultipleChoiceButtons(problem.answerOptions);
    }

    private void SetupMultipleChoiceButtons(System.Collections.Generic.List<int> options)
    {
        if (answerOptionButtons == null) return;

        for (int i = 0; i < answerOptionButtons.Length; i++)
        {
            if (answerOptionButtons[i] == null) continue;

            int optionValue = (options != null && i < options.Count) ? options[i] : 0;

            TMP_Text label = answerOptionButtons[i].GetComponentInChildren<TMP_Text>();
            if (label != null) label.text = optionValue.ToString();

            answerOptionButtons[i].onClick.RemoveAllListeners();
            answerOptionButtons[i].onClick.AddListener(() => SubmitAnswer(optionValue == _currentProblem.correctAnswer));
        }
    }

    private void SubmitAnswer(bool isCorrect)
    {
        if (!_waitingForAnswer) return;
        _waitingForAnswer = false;

        problemGenerator.RegisterAnswer(isCorrect); // mantém a dificuldade adaptativa global

        if (isCorrect) _score += pointsPerCorrectAnswer;
        else _score = Mathf.Max(0, _score - pointsLostPerWrongAnswer);
        UpdateScoreUI();

        TriggerIfExists(_currentCreatureAnimator, isCorrect ? "Correct" : "Wrong");

        StartCoroutine(ShowFeedbackThenNotify(isCorrect));
    }

    private IEnumerator ShowFeedbackThenNotify(bool isCorrect)
    {
        if (feedbackPanel != null)
        {
            if (questionPanel != null) questionPanel.SetActive(false);
            feedbackPanel.SetActive(true);

            // Cada imagem só aparece no caso certo (antes as duas apareciam juntas)
            if (correctFeedbackImage != null) correctFeedbackImage.SetActive(isCorrect);
            if (wrongFeedbackImage != null) wrongFeedbackImage.SetActive(!isCorrect);

            // Se já existe uma imagem para o veredito, o texto antigo fica vazio
            bool hasVerdictImage = correctFeedbackImage != null || wrongFeedbackImage != null;

            if (feedbackVerdictText != null)
            {
                if (hasVerdictImage) feedbackVerdictText.text = "";
                else feedbackVerdictText.text = isCorrect ? "Certo! Muito bem!" : "Quase lá! Olha como resolve:";
            }

            if (feedbackExplanationText != null)
            {
                feedbackExplanationText.text = isCorrect
                    ? ""
                    : problemGenerator.GetStepByStepExplanation(_currentProblem);
            }
        }

        // Quando erra, fica mais tempo na tela para a criança conseguir ler a explicação
        float waitSeconds = isCorrect ? feedbackDisplaySeconds : feedbackDisplaySeconds + explanationExtraSeconds;
        yield return new WaitForSeconds(waitSeconds);

        if (feedbackPanel != null) feedbackPanel.SetActive(false);
        if (questionPanel != null) questionPanel.SetActive(false);

        _onAnswered?.Invoke(isCorrect);
    }

    /// <summary>
    /// Dispara um trigger do Animator só se ele existir naquele bichinho. Assim cada
    /// bichinho pode ter só as reações que tiver (ex: o coelho só tem "Correct"),
    /// sem gerar avisos de parâmetro inexistente no Console.
    /// </summary>
    private void TriggerIfExists(Animator animator, string triggerName)
    {
        if (animator == null || animator.runtimeAnimatorController == null)
            return;

        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            if (parameter.name == triggerName && parameter.type == AnimatorControllerParameterType.Trigger)
            {
                animator.SetTrigger(triggerName);
                return;
            }
        }
    }

    private void UpdateScoreUI()
    {
        if (scoreText != null)
            scoreText.text = $"Pontos: {_score}";
    }
}