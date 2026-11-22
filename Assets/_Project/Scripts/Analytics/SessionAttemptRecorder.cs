using System;
using System.Collections.Generic;
using MathHorde.Core.Problems;
using MathHorde.Core.Progress;

namespace MathHorde.Analytics
{
    /// <summary>F3 session evidence. Tick copies immutable references and structs;
    /// the external frame host materializes parent-facing attempts after Tick.</summary>
    public sealed class SessionAttemptRecorder
    {
        private readonly MathAnswerSnapshot[] _answers;
        private readonly MathProblem[] _problems;
        private readonly List<QuestionAttempt> _attempts;
        private readonly IReadOnlyList<QuestionAttempt> _view;
        private int _count, _drained;
        private GameSessionId _sessionId;
        private DateTimeOffset _startedAt;
        public IReadOnlyList<QuestionAttempt> Attempts => _view;
        public SessionAttemptRecorder(int capacity)
        {
            if (capacity < 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            _answers = new MathAnswerSnapshot[capacity]; _problems = new MathProblem[capacity];
            _attempts = new List<QuestionAttempt>(capacity); _view = _attempts.AsReadOnly();
        }
        public void Begin(GameSessionId sessionId, DateTimeOffset startedAt)
        { _sessionId = sessionId; _startedAt = startedAt; _count = _drained = 0; _attempts.Clear(); }
        public void Record(MathAnswerSnapshot answer, MathProblem problem)
        {
            if (problem == null || !answer.ProblemId.Equals(problem.Id)) throw new ArgumentException("Answer metadata mismatch.");
            if (_count == _answers.Length) throw new InvalidOperationException("Reserve the full session schedule before Tick.");
            _answers[_count] = answer; _problems[_count++] = problem;
        }
        public void MaterializePending()
        {
            while (_drained < _count)
            {
                var answer = _answers[_drained]; var problem = _problems[_drained++];
                string selected = null;
                if (answer.SelectedOptionIndex.HasValue)
                    for (int i = 0; i < problem.Options.Count; i++)
                        if (problem.Options[i].OptionIndex == answer.SelectedOptionIndex.Value)
                            selected = problem.Options[i].Value.ToDisplayString();
                _attempts.Add(new QuestionAttempt(problem.Id, _sessionId, problem.SkillTags, problem.DifficultyLevel,
                    problem.CorrectAnswer.ToDisplayString(), selected, answer.WasCorrect, answer.ResponseTimeSeconds,
                    answer.TimedOut, answer.ErrorCategory, _startedAt.AddSeconds(answer.AnsweredAtGameTime)));
            }
        }
    }
}
