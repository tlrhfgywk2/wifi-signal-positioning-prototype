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
            new GameObject("Wi-Fi Positioning Prototype").AddComponent<PrototypeBootstrap>();
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

            Debug.Log("Wi-Fi prototype core validation passed: forward/inverse signal model, invalid-loss defense, and four-point multilateration.");
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

            Require(smokePlayer != null && bounds != null, "Player or room was not generated.");
            Require(points.Length == 4, $"Expected 4 access points, found {points.Length}.");
            Require(buttons.Length == 4, $"Expected 4 held-input buttons, found {buttons.Length}.");
            Require(rows.Length == 4, $"Expected 4 signal rows, found {rows.Length}.");
            Require(estimator != null && estimator.HasValidEstimate, "Position estimator has no valid result.");
            Require(Vector2.Distance(smokePlayer.transform.position, estimator.EstimatedPosition) < 0.001f,
                "Noise-free estimation error is not near zero.");

            foreach (AccessPointSignal point in points)
            {
                Require(point.Identifier is "NW" or "NE" or "SW" or "SE", "An AP has an invalid stable identifier.");
                Vector2 offset = point.Position - (Vector2)bounds.transform.position;
                Require(Mathf.Abs(offset.x) > bounds.Width * 0.35f && Mathf.Abs(offset.y) > bounds.Height * 0.35f,
                    $"AP {point.Identifier} is not placed at a room corner.");
            }

            AccessPointSignal northwest = Array.Find(points, point => point.Identifier == "NW");
            Vector2 original = smokePlayer.transform.position;
            Vector2 towardNorthwest = Vector2.MoveTowards(original, northwest.Position, 1f);
            Vector2 awayFromNorthwest = original + (original - northwest.Position).normalized;
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
            Debug.Log("Wi-Fi prototype play-mode smoke test passed: hierarchy, AP corners, signal rows, WASD, all held buttons, bounds, and near-zero estimation error.");
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
