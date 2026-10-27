using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Numu.World
{
    /// <summary>One answered question in an emotion-recognition activity.</summary>
    [Serializable]
    public class NumuAssessmentResult
    {
        public string sessionId;
        public string playerId;
        public string world;
        public string scenarioId;
        public string question;
        public string correctAnswer;
        public string selectedAnswer;
        public bool isCorrect;
        public int attempt;
        public long responseTimeMs;
        public string answeredAtUtc;
        public bool reducedMotion;
    }

    [Serializable]
    class NumuResultLog
    {
        public List<NumuAssessmentResult> results = new List<NumuAssessmentResult>();
    }

    /// <summary>
    /// Persists assessment results on the device (JSON in persistentDataPath) and forwards each one to the
    /// host app through <see cref="NumuBridge"/> so React Native can sync it to the backend.
    /// </summary>
    public static class NumuAssessmentStore
    {
        const string FileName = "numu_assessment_results.json";
        static NumuResultLog _log;

        public static string FilePath => Path.Combine(Application.persistentDataPath, FileName);

        static NumuResultLog Log
        {
            get
            {
                if (_log != null) return _log;
                try
                {
                    _log = File.Exists(FilePath) ? JsonUtility.FromJson<NumuResultLog>(File.ReadAllText(FilePath)) : new NumuResultLog();
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[NUMU] Could not read results, starting a new log: {e.Message}");
                    _log = new NumuResultLog();
                }
                return _log ??= new NumuResultLog();
            }
        }

        public static IReadOnlyList<NumuAssessmentResult> All => Log.results;

        public static void Record(NumuAssessmentResult r)
        {
            var session = NumuSession.Current;
            r.sessionId = session.sessionId;
            r.playerId = session.playerId;
            r.answeredAtUtc = DateTime.UtcNow.ToString("o");
            r.reducedMotion = Numu.NumuSettings.ReducedMotion;
            Log.results.Add(r);
            try
            {
                File.WriteAllText(FilePath, JsonUtility.ToJson(Log, true));
            }
            catch (Exception e)
            {
                Debug.LogError($"[NUMU] Failed to save result: {e.Message}");
            }
            NumuBridge.SendToHost("assessmentResult", JsonUtility.ToJson(r));
        }
    }
}
