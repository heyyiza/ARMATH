using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Gera desafios de matemática básica (soma, subtração, multiplicação, divisão,
/// comparação de quantidades) com dificuldade adaptativa e "roupagem" de cenário
/// (rua, cafeteria, cozinha) -- sem nenhuma referência a dinheiro, preço ou troco.
/// </summary>
public class MathProblemGenerator : MonoBehaviour
{
    public enum OperationType { Addition, Subtraction, Multiplication, Division, Comparison }
    public enum Scenario { Street, Cafeteria, Kitchen }

    [Serializable]
    public class MathProblem
    {
        public string scenarioText;   // frase contextualizada, ex: "A raposinha viu 4 gatos e depois mais 3..."
        public string questionText;   // pergunta final, ex: "Quantos gatos ela viu ao todo?"
        public int operandA;
        public int operandB;
        public OperationType operation;
        public int correctAnswer;
        public List<int> answerOptions; // 4 opções (1 certa + 3 erradas), só para perguntas numéricas

        public string GetFullPrompt()
        {
            return $"{scenarioText} {questionText}";
        }
    }

    [Header("Dificuldade (1 = mais fácil, 5 = mais difícil)")]
    [Range(1, 5)] public int currentDifficulty = 1;

    [Header("Controle de adaptação")]
    [Tooltip("Quantos acertos seguidos para subir de dificuldade")]
    public int correctStreakToLevelUp = 3;
    [Tooltip("Quantos erros seguidos para descer de dificuldade")]
    public int wrongStreakToLevelDown = 2;

    private int _correctStreak = 0;
    private int _wrongStreak = 0;

    private System.Random _rng = new System.Random();

    // ---- Faixas numéricas por dificuldade ----
    // Nível 1-2: números pequenos, só soma/subtração
    // Nível 3: entra multiplicação e comparação
    // Nível 4-5: entra divisão exata e números maiores
    private (int min, int max) GetNumberRange()
    {
        switch (currentDifficulty)
        {
            case 1: return (1, 5);
            case 2: return (1, 10);
            case 3: return (2, 12);
            case 4: return (2, 15);
            default: return (3, 20);
        }
    }

    private List<OperationType> GetAvailableOperations()
    {
        var ops = new List<OperationType> { OperationType.Addition, OperationType.Subtraction };
        if (currentDifficulty >= 2) ops.Add(OperationType.Comparison);
        if (currentDifficulty >= 3) ops.Add(OperationType.Multiplication);
        if (currentDifficulty >= 4) ops.Add(OperationType.Division);
        return ops;
    }

    /// <summary>
    /// Gera um novo problema de matemática, escolhendo aleatoriamente um cenário
    /// (rua, cafeteria ou cozinha) e uma operação disponível para a dificuldade atual.
    /// </summary>
    public MathProblem GenerateProblem()
    {
        var ops = GetAvailableOperations();
        var operation = ops[_rng.Next(ops.Count)];
        var scenario = (Scenario)_rng.Next(Enum.GetValues(typeof(Scenario)).Length);
        var (min, max) = GetNumberRange();

        int a = _rng.Next(min, max + 1);
        int b = _rng.Next(min, max + 1);

        var problem = new MathProblem { operation = operation };

        switch (operation)
        {
            case OperationType.Addition:
                problem.operandA = a;
                problem.operandB = b;
                problem.correctAnswer = a + b;
                BuildAdditionText(problem, scenario);
                break;

            case OperationType.Subtraction:
                // garante que a >= b para não dar resultado negativo
                if (b > a) { int tmp = a; a = b; b = tmp; }
                problem.operandA = a;
                problem.operandB = b;
                problem.correctAnswer = a - b;
                BuildSubtractionText(problem, scenario);
                break;

            case OperationType.Multiplication:
                // números menores para multiplicação não ficar assustadora
                a = _rng.Next(min, Mathf.Max(min + 1, max / 2) + 1);
                b = _rng.Next(2, 6);
                problem.operandA = a;
                problem.operandB = b;
                problem.correctAnswer = a * b;
                BuildMultiplicationText(problem, scenario);
                break;

            case OperationType.Division:
                // gera divisão exata: escolhe o resultado e o divisor primeiro
                int divisor = _rng.Next(2, 6);
                int quotient = _rng.Next(min, max / divisor + 2);
                int dividend = divisor * Mathf.Max(1, quotient);
                problem.operandA = dividend;
                problem.operandB = divisor;
                problem.correctAnswer = Mathf.Max(1, quotient);
                BuildDivisionText(problem, scenario);
                break;

            case OperationType.Comparison:
                problem.operandA = a;
                problem.operandB = b;
                problem.correctAnswer = a > b ? 1 : (a < b ? -1 : 0); // 1 = a maior, -1 = b maior, 0 = igual
                BuildComparisonText(problem, scenario);
                break;
        }

        if (operation != OperationType.Comparison)
            problem.answerOptions = GenerateAnswerOptions(problem.correctAnswer);

        return problem;
    }

    /// <summary>
    /// Gera 4 opções de resposta embaralhadas: a correta + 3 erradas, mas plausíveis
    /// (próximas da resposta certa, nunca negativas, nunca repetidas).
    /// </summary>
    private List<int> GenerateAnswerOptions(int correctAnswer)
    {
        var options = new HashSet<int> { correctAnswer };
        int attempts = 0;

        while (options.Count < 4 && attempts < 50)
        {
            attempts++;
            int offset = _rng.Next(-5, 6); // erro plausível: até 5 pra mais ou pra menos
            if (offset == 0) continue;

            int wrongAnswer = correctAnswer + offset;
            if (wrongAnswer < 0) continue;

            options.Add(wrongAnswer);
        }

        // Caso raro (não conseguiu gerar 4 distintas): completa com vizinhos simples
        int filler = correctAnswer + 1;
        while (options.Count < 4)
        {
            if (filler >= 0) options.Add(filler);
            filler++;
        }

        var shuffled = new List<int>(options);
        for (int i = shuffled.Count - 1; i > 0; i--)
        {
            int j = _rng.Next(i + 1);
            (shuffled[i], shuffled[j]) = (shuffled[j], shuffled[i]);
        }

        return shuffled;
    }

    /// <summary>
    /// Chame este método depois que o jogador responder, para o sistema
    /// ajustar a dificuldade automaticamente (sobe após acertos seguidos,
    /// desce após erros seguidos).
    /// </summary>
    public void RegisterAnswer(bool wasCorrect)
    {
        if (wasCorrect)
        {
            _correctStreak++;
            _wrongStreak = 0;
            if (_correctStreak >= correctStreakToLevelUp && currentDifficulty < 5)
            {
                currentDifficulty++;
                _correctStreak = 0;
            }
        }
        else
        {
            _wrongStreak++;
            _correctStreak = 0;
            if (_wrongStreak >= wrongStreakToLevelDown && currentDifficulty > 1)
            {
                currentDifficulty--;
                _wrongStreak = 0;
            }
        }
    }

    /// <summary>
    /// Gera uma explicação passo a passo de como chegar na resposta certa,
    /// para mostrar à criança quando ela errar (feedback corretivo).
    /// </summary>
    public string GetStepByStepExplanation(MathProblem p)
    {
        switch (p.operation)
        {
            case OperationType.Addition:
                return $"Somar é juntar as quantidades:\n{p.operandA} + {p.operandB} = {p.correctAnswer}";

            case OperationType.Subtraction:
                return $"Subtrair é tirar uma quantidade:\nComeçamos com {p.operandA}, tiramos {p.operandB} e sobram {p.correctAnswer}.\n{p.operandA} - {p.operandB} = {p.correctAnswer}";

            case OperationType.Multiplication:
            {
                // Mostra a multiplicação como uma soma repetida: 4 x 3 = 4 + 4 + 4
                string repeated = p.operandA.ToString();
                for (int i = 1; i < p.operandB; i++)
                    repeated += " + " + p.operandA;

                return $"Multiplicar é somar várias vezes o mesmo número:\n{repeated} = {p.correctAnswer}\nOu seja, {p.operandA} x {p.operandB} = {p.correctAnswer}";
            }

            case OperationType.Division:
                return $"Dividir é repartir em partes iguais:\n{p.operandA} dividido em {p.operandB} grupos iguais dá {p.correctAnswer} em cada grupo.\n{p.operandA} ÷ {p.operandB} = {p.correctAnswer}";

            case OperationType.Comparison:
                if (p.correctAnswer == 0)
                    return $"{p.operandA} e {p.operandB} são iguais, então é um empate!";

                string ladoMaior = p.correctAnswer == 1 ? "primeira" : "segunda";
                int maior = Mathf.Max(p.operandA, p.operandB);
                int menor = Mathf.Min(p.operandA, p.operandB);
                return $"Comparar é ver qual número é maior:\n{maior} é maior que {menor}, então a {ladoMaior} tem mais.";

            default:
                return "";
        }
    }

    // ---------- Geração de texto por cenário (sem dinheiro/preço em nenhum caso) ----------

    private void BuildAdditionText(MathProblem p, Scenario scenario)
    {
        switch (scenario)
        {
            case Scenario.Street:
                p.scenarioText = $"Na rua, a raposinha contou {p.operandA} carros vermelhos e depois mais {p.operandB} carros azuis passando.";
                p.questionText = "Quantos carros ela viu ao todo?";
                break;
            case Scenario.Cafeteria:
                p.scenarioText = $"Na cafeteria, tinham {p.operandA} bolinhos numa bandeja e trouxeram mais {p.operandB} bolinhos.";
                p.questionText = "Quantos bolinhos tem agora ao todo?";
                break;
            default: // Kitchen
                p.scenarioText = $"Na cozinha, a receita já tinha {p.operandA} ovos na tigela e a raposinha colocou mais {p.operandB}.";
                p.questionText = "Quantos ovos tem na tigela agora?";
                break;
        }
    }

    private void BuildSubtractionText(MathProblem p, Scenario scenario)
    {
        switch (scenario)
        {
            case Scenario.Street:
                p.scenarioText = $"Havia {p.operandA} passarinhos em cima do fio e {p.operandB} deles voaram embora.";
                p.questionText = "Quantos passarinhos ficaram?";
                break;
            case Scenario.Cafeteria:
                p.scenarioText = $"A vitrine tinha {p.operandA} doces e {p.operandB} foram servidos para os clientes.";
                p.questionText = "Quantos doces sobraram na vitrine?";
                break;
            default:
                p.scenarioText = $"A receita pedia {p.operandA} xícaras de farinha, e a raposinha já usou {p.operandB}.";
                p.questionText = "Quantas xícaras ainda faltam usar?";
                break;
        }
    }

    private void BuildMultiplicationText(MathProblem p, Scenario scenario)
    {
        switch (scenario)
        {
            case Scenario.Street:
                p.scenarioText = $"Cada casinha da rua tem {p.operandA} janelas, e são {p.operandB} casinhas na fileira.";
                p.questionText = "Quantas janelas existem ao todo na fileira?";
                break;
            case Scenario.Cafeteria:
                p.scenarioText = $"Cada amigo da raposinha quer {p.operandA} biscoitos, e são {p.operandB} amigos na mesa.";
                p.questionText = "Quantos biscoitos precisam ser separados ao todo?";
                break;
            default:
                p.scenarioText = $"A receita usa {p.operandA} xícaras de farinha para cada bolo, e a raposinha vai fazer {p.operandB} bolos.";
                p.questionText = "Quantas xícaras de farinha serão usadas ao todo?";
                break;
        }
    }

    private void BuildDivisionText(MathProblem p, Scenario scenario)
    {
        switch (scenario)
        {
            case Scenario.Street:
                p.scenarioText = $"Existem {p.operandA} flores no jardim, distribuídas igualmente em {p.operandB} canteiros.";
                p.questionText = "Quantas flores há em cada canteiro?";
                break;
            case Scenario.Cafeteria:
                p.scenarioText = $"A raposinha precisa separar {p.operandA} biscoitos igualmente entre {p.operandB} amigos.";
                p.questionText = "Quantos biscoitos cada amigo vai receber?";
                break;
            default:
                p.scenarioText = $"A receita rende {p.operandA} pedaços de bolo, que serão divididos igualmente em {p.operandB} bandejas.";
                p.questionText = "Quantos pedaços vão em cada bandeja?";
                break;
        }
    }

    private void BuildComparisonText(MathProblem p, Scenario scenario)
    {
        switch (scenario)
        {
            case Scenario.Street:
                p.scenarioText = $"Numa rua tem {p.operandA} árvores, e em outra rua tem {p.operandB} árvores.";
                p.questionText = "Qual rua tem mais árvores? (responda: primeira, segunda ou empate)";
                break;
            case Scenario.Cafeteria:
                p.scenarioText = $"Numa bandeja tem {p.operandA} bolinhos, e em outra bandeja tem {p.operandB} bolinhos.";
                p.questionText = "Qual bandeja tem mais bolinhos? (responda: primeira, segunda ou empate)";
                break;
            default:
                p.scenarioText = $"Numa tigela tem {p.operandA} morangos, e em outra tigela tem {p.operandB} morangos.";
                p.questionText = "Qual tigela tem mais morangos? (responda: primeira, segunda ou empate)";
                break;
        }
    }
}