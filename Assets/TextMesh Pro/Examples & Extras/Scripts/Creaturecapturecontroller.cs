using System.Collections;
using UnityEngine;

/// <summary>
/// Controla a sequência de captura de UM bichinho por vez: a criança precisa
/// acertar 3 perguntas tendo 2 vidas no total durante toda a tentativa de captura.
/// Ao errar as 2 vidas, a captura é cancelada e o bichinho entra em "recarga" por um tempo.
/// </summary>
public class CreatureCaptureController : MonoBehaviour
{
    [Header("Referências")]
    public GameUIManager gameUIManager;
    public MathProblemGenerator problemGenerator;

    [Header("Regras de captura")]
    public int correctAnswersNeeded = 3;
    public int maxMistakesAllowed = 2; // 2 vidas: o 2º erro cancela a captura
    public float cooldownSecondsOnFail = 30f * 60f; // 30 minutos

    private bool _encounterInProgress = false;

    /// <summary>Chamado quando a criança toca num bichinho disponível no mundo AR.</summary>
    public void StartEncounter(CreatureInstance creature)
    {
        if (_encounterInProgress || creature == null || !creature.IsInteractable)
            return;

        StartCoroutine(RunEncounter(creature));
    }

    private IEnumerator RunEncounter(CreatureInstance creature)
    {
        _encounterInProgress = true;
        Animator creatureAnimator = creature.GetComponent<Animator>();

        int correctCount = 0;
        int mistakeCount = 0;

        while (correctCount < correctAnswersNeeded && mistakeCount < maxMistakesAllowed)
        {
            var problem = problemGenerator.GenerateProblem();

            bool waitingForAnswer = true;
            bool wasCorrect = false;

            gameUIManager.AskQuestion(problem, creatureAnimator, (isCorrect) =>
            {
                wasCorrect = isCorrect;
                waitingForAnswer = false;
            });

            yield return new WaitUntil(() => !waitingForAnswer);

            if (wasCorrect)
                correctCount++;
            else
                mistakeCount++;

            if (mistakeCount >= maxMistakesAllowed)
            {
                // perdeu todas as vidas -> falha a captura inteira
                creature.FailCaptureAndCooldown(cooldownSecondsOnFail);
                _encounterInProgress = false;
                yield break;
            }
        }

        // chegou a 3 acertos antes de perder as vidas -> captura confirmada
        creature.Capture();
        _encounterInProgress = false;
    }
}