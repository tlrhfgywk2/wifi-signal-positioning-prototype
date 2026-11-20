using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using WifiPositioningPrototype;

namespace WifiPositioningPrototypeEditor
{
    public static class WifiPrototypeSceneGenerator
    {
        private const string ScenePath = "Assets/Scenes/WifiSignalPositioningPrototype.unity";
        private static bool previousEnterPlayModeEnabled;
        private static EnterPlayModeOptions previousEnterPlayModeOptions;
        private static int smokeStage;
        private static PlayerMovement smokePlayer;

        [MenuItem("Tools/Wi-Fi Prototype/Generate Playable Scene")]
        public static void GenerateScene()
        {
            Directory.CreateDirectory("Assets/Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameObject root = new("Wi-Fi Positioning Prototype");
            PrototypeBootstrap bootstrap = root.AddComponent<PrototypeBootstrap>();
            GameObject creatureObject = new("Radio Interference Creature");
            creatureObject.transform.SetParent(root.transform);
            creatureObject.transform.position = new Vector3(-1.2f, 1.1f, 0f);
            RadioInterferenceCreature creature = creatureObject.AddComponent<RadioInterferenceCreature>();
            bootstrap.ConfigureCreature(creature);
            EditorSceneManager.SaveScene(scene, ScenePath);

            List<EditorBuildSettingsScene> scenes = new() { new EditorBuildSettingsScene(ScenePath, true) };
            foreach (EditorBuildSettingsScene existing in EditorBuildSettings.scenes)
                if (!string.Equals(existing.path, ScenePath, StringComparison.OrdinalIgnoreCase)) scenes.Add(existing);
            EditorBuildSettings.scenes = scenes.ToArray();
            AssetDatabase.SaveAssets();
            Debug.Log($"Generated playable prototype scene: {ScenePath}");
        }

        public static void ValidateCoreModel()
        {
            const float reference = -20f;
            const float loss = 5f;
            float signal = LinearSignalModel.Calculate(reference, loss, 3f);
            if (Mathf.Abs(signal - (-35f)) > 0.0001f) throw new Exception("Signal forward model validation failed.");
            if (!LinearSignalModel.TryRecoverDistance(reference, signal, loss, out float recovered)
                || Mathf.Abs(recovered - 3f) > 0.0001f) throw new Exception("Signal inverse model validation failed.");
            if (LinearSignalModel.TryRecoverDistance(reference, signal, 0f, out _))
                throw new Exception("Invalid loss defensive validation failed.");

            ValidateInterferenceGeometry();

            Vector2 target = new(1.25f, -2.1f);
            Vector2[] positions = { new(-4.5f, 4.5f), new(4.5f, 4.5f), new(-4.5f, -4.5f), new(4.5f, -4.5f) };
            string[] ids = { "NW", "NE", "SW", "SE" };
            List<AccessPointSignal> points = new();
            try
            {
                for (int i = 0; i < positions.Length; i++)
                {
                    var point = new GameObject(ids[i]).AddComponent<AccessPointSignal>();
                    point.Configure(ids[i], positions[i], reference, loss);
                    point.Refresh(target);
                    points.Add(point);
                }
                if (!SignalPositionEstimator.TryEstimate(points, out Vector2 estimate)
                    || Vector2.Distance(target, estimate) > 0.0001f)
                    throw new Exception($"Multilateration validation failed. Estimate: {estimate}");
            }
            finally
            {
                foreach (AccessPointSignal point in points) UnityEngine.Object.DestroyImmediate(point.gameObject);
            }

            Debug.Log("Wi-Fi prototype core validation passed: signal model, clamped segment geometry, interference bands/AP contamination, bounded history, and multilateration.");
        }

        private static void ValidateInterferenceGeometry()
        {
            float distance = RadioInterferenceMath.PointToSegmentDistance(new Vector2(0f, 0f),
                new Vector2(-2f, 0f), new Vector2(2f, 0f), out Vector2 closest);
            Require(Mathf.Abs(distance) < 0.0001f && Vector2.Distance(closest, Vector2.zero) < 0.0001f,
                "Creature on segment midpoint validation failed.");

            InterferenceGeometry medium = RadioInterferenceMath.Evaluate(new Vector2(0f, 0.4f),
                new Vector2(-2f, 0f), new Vector2(2f, 0f), 0.25f, 0.65f, 1.4f, 12f, 6f, 3f, 0.5f);
            Require(medium.Level == InterferenceLevel.Medium && medium.Amplitude >= 6f,
                "Medium interference band validation failed.");

            distance = RadioInterferenceMath.PointToSegmentDistance(new Vector2(3f, 1f),
                Vector2.zero, new Vector2(2f, 0f), out closest);
            Require(Vector2.Distance(closest, new Vector2(2f, 0f)) < 0.0001f
                && Mathf.Abs(distance - Mathf.Sqrt(2f)) < 0.0001f, "Beyond-player clamp validation failed.");
            RadioInterferenceMath.PointToSegmentDistance(new Vector2(-1f, 1f),
                Vector2.zero, new Vector2(2f, 0f), out closest);
            Require(Vector2.Distance(closest, Vector2.zero) < 0.0001f, "Behind-AP clamp validation failed.");
            RadioInterferenceMath.PointToSegmentDistance(new Vector2(2f, 0f),
                Vector2.one, Vector2.one, out closest);
            Require(closest == Vector2.one, "Degenerate AP/player segment validation failed.");

            InterferenceGeometry contaminated = RadioInterferenceMath.Evaluate(new Vector2(0f, 0.85f),
                Vector2.zero, new Vector2(5f, 0f), 0.25f, 0.65f, 1.4f, 12f, 6f, 3f, 0.9f);
            Require(contaminated.ApContaminated && contaminated.Level == InterferenceLevel.Medium
                && contaminated.Amplitude >= 6f,
                "AP proximity contamination validation failed.");

            SignalMeasurementHistory history = new(4);
            for (int i = 0; i < 12; i++) history.Add(i);
            Require(history.Count == 4 && history.Capacity == 4 && Mathf.Abs(history.Range - 3f) < 0.0001f,
                "Bounded signal history validation failed.");

            Require(SignalStatusClassifier.Classify(false, InterferenceLevel.None, 20f, false, 5f, 9f) == "STABLE",
                "Static loss was incorrectly classified as creature interference.");
            Require(SignalStatusClassifier.Classify(false, InterferenceLevel.Weak, 1f, true, 5f, 9f) == "WEAK INTERFERENCE",
                "Weak status classification failed.");
            Require(SignalStatusClassifier.Classify(false, InterferenceLevel.Medium, 4f, true, 5f, 9f) == "UNSTABLE",
                "Medium status classification failed.");
            Require(SignalStatusClassifier.Classify(false, InterferenceLevel.Strong, 4f, true, 5f, 9f) == "SEVERE INTERFERENCE",
                "Strong status classification failed.");
            Require(SignalStatusClassifier.Classify(true, InterferenceLevel.Strong, 12f, true, 5f, 9f) == "AP CONTAMINATED",
                "AP contamination status classification failed.");
        }

        public static void RunPlayModeSmokeTest()
        {
            previousEnterPlayModeEnabled = EditorSettings.enterPlayModeOptionsEnabled;
            previousEnterPlayModeOptions = EditorSettings.enterPlayModeOptions;
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            smokeStage = 0;
            EditorApplication.update += SmokeUpdate;
            EditorApplication.isPlaying = true;
        }

        private static void SmokeUpdate()
        {
            if (!EditorApplication.isPlaying || Time.frameCount < 5) return;
            try
            {
                switch (smokeStage)
                {
                    case 0:
                        ValidateRuntimeHierarchy();
                        ValidateInputsAndBounds();
                        CompleteSmokeTest();
                        break;
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                RestorePlayModeSettings();
                EditorApplication.Exit(1);
            }
        }

        private static void ValidateRuntimeHierarchy()
        {
            smokePlayer = UnityEngine.Object.FindFirstObjectByType<PlayerMovement>();
            RoomBounds bounds = UnityEngine.Object.FindFirstObjectByType<RoomBounds>();
            SignalPositionEstimator estimator = UnityEngine.Object.FindFirstObjectByType<SignalPositionEstimator>();
            AccessPointSignal[] points = UnityEngine.Object.FindObjectsByType<AccessPointSignal>(FindObjectsSortMode.None);
            DirectionalInputButton[] buttons = UnityEngine.Object.FindObjectsByType<DirectionalInputButton>(FindObjectsSortMode.None);
            SignalRowView[] rows = UnityEngine.Object.FindObjectsByType<SignalRowView>(FindObjectsSortMode.None);
            RadioInterferenceCreature creature = UnityEngine.Object.FindFirstObjectByType<RadioInterferenceCreature>();
            GameObject inertialMarker = GameObject.Find("Inertial Position (Solid White Dot)");
            GameObject signalMarker = GameObject.Find("Signal Estimated Position (Red Ring)");

            Require(smokePlayer != null && bounds != null, "Player or room was not generated.");
            Require(inertialMarker != null && inertialMarker.GetComponent<MeshRenderer>() != null,
                "The solid-white inertial position marker was not generated.");
            LineRenderer signalRing = signalMarker != null ? signalMarker.GetComponent<LineRenderer>() : null;
            Require(signalRing != null && signalRing.startColor == Color.red && signalRing.endColor == Color.red,
                "The hollow-red signal estimate marker was not generated.");
            Require(points.Length == 4, $"Expected 4 access points, found {points.Length}.");
            Require(creature != null, "The radio interference creature was not generated.");
            Require(buttons.Length == 4, $"Expected 4 held-input buttons, found {buttons.Length}.");
            Require(rows.Length == 4, $"Expected 4 signal rows, found {rows.Length}.");
            Require(estimator != null && estimator.HasValidEstimate, "Position estimator has no valid result.");
            Require(Vector2.Distance(smokePlayer.transform.position, estimator.EstimatedPosition) < 1.5f,
                "The noisy signal estimate exceeded the expected validation tolerance.");

            foreach (AccessPointSignal point in points)
            {
                Require(point.Identifier is "NW" or "NE" or "SW" or "SE", "An AP has an invalid stable identifier.");
                Require(point.HistoryCount <= point.HistoryCapacity, $"AP {point.Identifier} history exceeded its capacity.");
                Vector2 offset = point.Position - (Vector2)bounds.transform.position;
                Require(Mathf.Abs(offset.x) > bounds.Width * 0.35f && Mathf.Abs(offset.y) > bounds.Height * 0.35f,
                    $"AP {point.Identifier} is not placed at a room corner.");
            }

            AccessPointSignal northwest = Array.Find(points, point => point.Identifier == "NW");
            Vector2 original = smokePlayer.transform.position;
            Vector2 towardNorthwest = Vector2.MoveTowards(original, northwest.Position, 2f);
            Vector2 awayFromNorthwest = original + (original - northwest.Position).normalized * 2f;
            northwest.Refresh(original);
            float baseline = northwest.CurrentSignalStrength;
            northwest.Refresh(towardNorthwest);
            Require(northwest.CurrentSignalStrength > baseline, "Moving toward NW did not strengthen its signal.");
            northwest.Refresh(awayFromNorthwest);
            Require(northwest.CurrentSignalStrength < baseline, "Moving away from NW did not weaken its signal.");
            foreach (AccessPointSignal point in points) point.Refresh(original);
        }

        private static void ValidateInputsAndBounds()
        {
            if (Keyboard.current == null) InputSystem.AddDevice<Keyboard>();
            Require(Keyboard.current != null, "Could not create an Input System keyboard for headless validation.");
            Vector2 start = smokePlayer.transform.position;
            InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(Key.W));
            InputSystem.Update();
            Require(Keyboard.current.wKey.isPressed, "The virtual W key state was not applied.");
            smokePlayer.ApplyMovement(smokePlayer.ReadCurrentInput(), 0.25f);
            Require(smokePlayer.transform.position.y > start.y + 0.01f, "W keyboard movement failed.");
            InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState());
            InputSystem.Update();

            string[] names = { "A / LEFT", "S / DOWN", "D / RIGHT", "W / UP" };
            Vector2[] directions = { Vector2.left, Vector2.down, Vector2.right, Vector2.up };
            for (int i = 0; i < names.Length; i++)
            {
                DirectionalInputButton button = GameObject.Find(names[i])?.GetComponent<DirectionalInputButton>();
                Require(button != null, $"Directional button {names[i]} was not found.");
                start = smokePlayer.transform.position;
                button.OnPointerDown(null);
                smokePlayer.ApplyMovement(smokePlayer.ReadCurrentInput(), 0.25f);
                smokePlayer.ApplyMovement(smokePlayer.ReadCurrentInput(), 0.25f);
                button.OnPointerUp(null);
                Vector2 delta = (Vector2)smokePlayer.transform.position - start;
                Require(Vector2.Dot(delta, directions[i]) > 0.01f, $"Held button {names[i]} movement failed.");
            }

            RoomBounds bounds = UnityEngine.Object.FindFirstObjectByType<RoomBounds>();
            smokePlayer.transform.position = new Vector3(100f, 100f, 0f);
            smokePlayer.ApplyMovement(Vector2.right, 0.25f);
            Require(smokePlayer.transform.position.x <= bounds.Max.x && smokePlayer.transform.position.y <= bounds.Max.y,
                "Room boundary clamp failed.");
        }

        private static void CompleteSmokeTest()
        {
            Debug.Log("Wi-Fi prototype play-mode smoke test passed: hierarchy, AP corners, signal rows, WASD, all held buttons, bounds, two markers, and bounded noisy estimation.");
            RestorePlayModeSettings();
            EditorApplication.Exit(0);
        }

        private static void RestorePlayModeSettings()
        {
            EditorApplication.update -= SmokeUpdate;
            EditorSettings.enterPlayModeOptionsEnabled = previousEnterPlayModeEnabled;
            EditorSettings.enterPlayModeOptions = previousEnterPlayModeOptions;
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
        }
    }
}
