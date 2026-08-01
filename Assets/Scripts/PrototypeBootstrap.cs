using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace WifiPositioningPrototype
{
    public sealed class PrototypeBootstrap : MonoBehaviour
    {
        [Header("Room (1 unit = 1 meter)")]
        [SerializeField, Min(1f)] private float roomWidth = 10f;
        [SerializeField, Min(1f)] private float roomHeight = 10f;
        [SerializeField, Min(0.1f)] private float playerSpeed = 3f;

        [Header("Signal model")]
        [SerializeField] private float referenceSignalStrength = -20f;
        [SerializeField, Min(0.001f)] private float signalLossPerMeter = 5f;
        [SerializeField, Min(0.1f)] private float signalMeasurementInterval = 1f;
        [SerializeField, Min(0f)] private float signalNoiseAmplitude = 3f;
        [SerializeField, Range(0.1f, 1f)] private float accessPointInset = 0.45f;

        [Header("Radio interference")]
        [SerializeField] private RadioInterferenceCreature interferenceCreature;
        [SerializeField] private Vector2 fallbackCreaturePosition = new(-1.2f, 1.1f);
        [SerializeField] private bool showInterferenceDebug;

        private readonly List<AccessPointSignal> accessPoints = new();
        private PlayerMovement player;
        private SignalPositionEstimator estimator;
        private SignalPanelController signalPanel;
        private Transform estimatedMarker;
        private Material whiteMaterial;
        private Font uiFont;
        private float nextSignalMeasurementTime;

        public void ConfigureCreature(RadioInterferenceCreature creature) => interferenceCreature = creature;

        private void Awake()
        {
            Application.targetFrameRate = 120;
            BuildPrototype();
        }

        private void LateUpdate()
        {
            if (player == null || Time.time < nextSignalMeasurementTime) return;
            float interval = signalMeasurementInterval > 0f ? Mathf.Max(0.1f, signalMeasurementInterval) : 1f;
            nextSignalMeasurementTime = Time.time + interval;

            Vector2 actual = player.transform.position;
            foreach (AccessPointSignal accessPoint in accessPoints)
                accessPoint.Refresh(actual, interferenceCreature, Time.time);

            bool valid = estimator.Refresh(accessPoints);
            if (valid) estimatedMarker.position = new Vector3(estimator.EstimatedPosition.x, estimator.EstimatedPosition.y, -0.2f);
            estimatedMarker.gameObject.SetActive(valid);
            signalPanel.Refresh(estimator.EstimatedPosition,
                valid ? Vector2.Distance(actual, estimator.EstimatedPosition) : 0f, valid);
        }

        private void BuildPrototype()
        {
            uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
            whiteMaterial = new Material(shader) { color = Color.white };

            Camera camera = BuildCamera();
            Transform world = new GameObject("World").transform;
            world.SetParent(transform);

            RoomBounds bounds = BuildRoom(world);
            player = BuildPlayer(world, bounds);
            estimator = new GameObject("Signal Position Estimator").AddComponent<SignalPositionEstimator>();
            estimator.transform.SetParent(transform);
            estimatedMarker = BuildRing("Signal Estimated Position (Red Ring)", world,
                0.22f, 0.04f, 40, Color.red);
            estimatedMarker.position = Vector3.zero;

            BuildAccessPoints(world, bounds);
            EnsureInterferenceCreature(world);
            RadioInterferenceDebugVisualizer visualizer = gameObject.AddComponent<RadioInterferenceDebugVisualizer>();
            visualizer.Configure(accessPoints, player.transform, interferenceCreature, showInterferenceDebug);
            Canvas canvas = BuildCanvas(camera);
            signalPanel = BuildSignalPanel(canvas.transform);
            BuildDirectionalPad(canvas.transform);
            BuildEventSystem();

            foreach (AccessPointSignal accessPoint in accessPoints)
                accessPoint.Refresh(player.transform.position, interferenceCreature, Time.time);
        }

        private void EnsureInterferenceCreature(Transform world)
        {
            if (interferenceCreature == null)
                interferenceCreature = FindFirstObjectByType<RadioInterferenceCreature>();
            if (interferenceCreature != null) return;

            GameObject creatureObject = new("Radio Interference Creature");
            creatureObject.transform.SetParent(world);
            creatureObject.transform.position = new Vector3(fallbackCreaturePosition.x, fallbackCreaturePosition.y, 0f);
            interferenceCreature = creatureObject.AddComponent<RadioInterferenceCreature>();
            Debug.LogWarning("No scene creature reference was assigned; a runtime fallback creature was created.", this);
        }

        private Camera BuildCamera()
        {
            GameObject cameraObject = new("Main Camera");
            cameraObject.transform.SetParent(transform);
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = Mathf.Max(roomHeight * 0.62f, 6.2f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.rect = new Rect(0f, 0.18f, 0.68f, 0.82f);
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 100f;
            return camera;
        }

        private RoomBounds BuildRoom(Transform parent)
        {
            GameObject roomObject = new("Room (10m x 10m)");
            roomObject.transform.SetParent(parent);
            RoomBounds bounds = roomObject.AddComponent<RoomBounds>();
            bounds.Configure(roomWidth, roomHeight);

            LineRenderer line = roomObject.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.loop = true;
            line.positionCount = 4;
            line.startWidth = line.endWidth = 0.035f;
            line.material = whiteMaterial;
            line.startColor = line.endColor = Color.white;
            float halfW = roomWidth * 0.5f;
            float halfH = roomHeight * 0.5f;
            line.SetPositions(new[]
            {
                new Vector3(-halfW, -halfH), new Vector3(-halfW, halfH),
                new Vector3(halfW, halfH), new Vector3(halfW, -halfH)
            });
            return bounds;
        }

        private PlayerMovement BuildPlayer(Transform parent, RoomBounds bounds)
        {
            GameObject playerObject = BuildFilledShape("Inertial Position (Solid White Dot)", parent,
                BuildCircleMesh(0.14f, 28), Color.white, -0.1f);
            PlayerMovement movement = playerObject.AddComponent<PlayerMovement>();
            movement.Configure(bounds, playerSpeed, 0.14f);
            return movement;
        }

        private void BuildAccessPoints(Transform parent, RoomBounds bounds)
        {
            Vector2 min = bounds.Min + Vector2.one * accessPointInset;
            Vector2 max = bounds.Max - Vector2.one * accessPointInset;
            CreateAccessPoint("NW", new Vector2(min.x, max.y), new Vector2(0.35f, -0.42f), parent);
            CreateAccessPoint("NE", new Vector2(max.x, max.y), new Vector2(-0.35f, -0.42f), parent);
            CreateAccessPoint("SW", new Vector2(min.x, min.y), new Vector2(0.35f, 0.42f), parent);
            CreateAccessPoint("SE", new Vector2(max.x, min.y), new Vector2(-0.35f, 0.42f), parent);
        }

        private void CreateAccessPoint(string id, Vector2 position, Vector2 labelOffset, Transform parent)
        {
            GameObject pointObject = BuildFilledShape($"Access Point {id}", parent,
                BuildDiamondMesh(0.22f), Color.white, 0f);
            AccessPointSignal signal = pointObject.AddComponent<AccessPointSignal>();
            signal.Configure(id, position, referenceSignalStrength, signalLossPerMeter, signalNoiseAmplitude);
            accessPoints.Add(signal);

            GameObject labelObject = new($"{id} Label");
            labelObject.transform.SetParent(pointObject.transform);
            labelObject.transform.localPosition = new Vector3(labelOffset.x, labelOffset.y, 0f);
            TextMesh label = labelObject.AddComponent<TextMesh>();
            label.text = id;
            label.font = uiFont;
            label.fontSize = 54;
            label.characterSize = 0.055f;
            label.color = Color.white;
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
        }

        private Canvas BuildCanvas(Camera camera)
        {
            GameObject canvasObject = new("Diagnostic UI");
            canvasObject.transform.SetParent(transform);
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        private SignalPanelController BuildSignalPanel(Transform canvas)
        {
            RectTransform panel = CreateRect("Signal Information Panel", canvas,
                new Vector2(0.70f, 0.08f), new Vector2(0.98f, 0.95f), Vector2.zero, Vector2.zero);
            Image panelImage = panel.gameObject.AddComponent<Image>();
            panelImage.color = new Color(0.035f, 0.035f, 0.035f, 0.98f);
            Outline outline = panel.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(1f, 1f, 1f, 0.55f);
            outline.effectDistance = new Vector2(1f, -1f);

            CreateText("Title", panel, "WI-FI POSITIONING DIAGNOSTIC", 20, TextAnchor.MiddleLeft,
                new Vector2(0.06f, 0.88f), new Vector2(0.94f, 0.97f), Color.white);
            CreateText("Header", panel, "AP        SIGNAL       VALUE / DELTA        STATUS", 11, TextAnchor.MiddleLeft,
                new Vector2(0.06f, 0.82f), new Vector2(0.94f, 0.88f), new Color(0.72f, 0.72f, 0.72f));

            SignalPanelController controller = panel.gameObject.AddComponent<SignalPanelController>();
            for (int i = 0; i < accessPoints.Count; i++)
                controller.AddRow(BuildSignalRow(panel, accessPoints[i], 0.74f - i * 0.105f));

            Text coordinateText = CreateText("Estimated Coordinates", panel, string.Empty, 18, TextAnchor.UpperLeft,
                new Vector2(0.06f, 0.21f), new Vector2(0.94f, 0.47f-0.12f), Color.white);
            controller.Configure(coordinateText);

            CreateText("Legend", panel,
                "LEGEND\nWHITE DOT   Inertial / trusted\nRED RING    Signal estimate / 1 Hz", 14, TextAnchor.UpperLeft,
                new Vector2(0.06f, 0.07f), new Vector2(0.94f, 0.22f), new Color(0.82f, 0.82f, 0.82f));
            return controller;
        }

        private SignalRowView BuildSignalRow(RectTransform panel, AccessPointSignal accessPoint, float top)
        {
            RectTransform row = CreateRect($"{accessPoint.Identifier} Signal Row", panel,
                new Vector2(0.06f, top - 0.08f), new Vector2(0.94f, top), Vector2.zero, Vector2.zero);
            Text identifier = CreateText("Identifier", row, accessPoint.Identifier, 17, TextAnchor.MiddleLeft,
                new Vector2(0f, 0f), new Vector2(0.12f, 1f), Color.white);

            RectTransform bar = CreateRect("Signal Bar", row, new Vector2(0.13f, 0.31f), new Vector2(0.39f, 0.69f), Vector2.zero, Vector2.zero);
            Image barBackground = bar.gameObject.AddComponent<Image>();
            barBackground.color = new Color(1f, 1f, 1f, 0.16f);
            RectTransform fillRect = CreateRect("Fill", bar, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            Image fill = fillRect.gameObject.AddComponent<Image>();
            fill.color = Color.white;
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = 0;

            Text value = CreateText("Value", row, string.Empty, 15, TextAnchor.MiddleRight,
                new Vector2(0.40f, 0f), new Vector2(0.69f, 1f), Color.white);
            Text status = CreateText("Status", row, string.Empty, 11, TextAnchor.MiddleRight,
                new Vector2(0.70f, 0f), new Vector2(1f, 1f), new Color(0.78f, 0.78f, 0.78f));

            SignalRowView view = row.gameObject.AddComponent<SignalRowView>();
            view.Configure(accessPoint, identifier, value, status, fill);
            return view;
        }

        private void BuildDirectionalPad(Transform canvas)
        {
            RectTransform pad = CreateRect("Held Input Directional Pad", canvas,
                Vector2.zero, Vector2.zero, new Vector2(126f, 78f), new Vector2(224f, 132f));
            CreateDirectionButton(pad, "W / UP", Vector2.up, new Vector2(74f, 76f));
            CreateDirectionButton(pad, "A / LEFT", Vector2.left, new Vector2(12f, 18f));
            CreateDirectionButton(pad, "S / DOWN", Vector2.down, new Vector2(74f, 18f));
            CreateDirectionButton(pad, "D / RIGHT", Vector2.right, new Vector2(136f, 18f));
            CreateText("Instructions", canvas, "Move with WASD, arrow keys, or hold the on-screen controls.",
                18, TextAnchor.MiddleLeft, Vector2.zero, Vector2.zero, new Color(0.82f, 0.82f, 0.82f),
                new Vector2(260f+320f, 22f), new Vector2(470f, 28f));
        }

        private void CreateDirectionButton(Transform parent, string label, Vector2 direction, Vector2 position)
        {
            RectTransform rect = CreateRect(label, parent, Vector2.zero, Vector2.zero, position, new Vector2(56f, 50f));
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = new Color(0.12f, 0.12f, 0.12f, 1f);
            Outline outline = rect.gameObject.AddComponent<Outline>();
            outline.effectColor = Color.white;
            outline.effectDistance = new Vector2(1f, -1f);
            CreateText("Label", rect, label, 11, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Color.white);
            DirectionalInputButton button = rect.gameObject.AddComponent<DirectionalInputButton>();
            button.Configure(player, direction);
        }

        private void BuildEventSystem()
        {
            GameObject eventObject = new("Event System (Input System)");
            eventObject.transform.SetParent(transform);
            eventObject.AddComponent<EventSystem>();
            InputSystemUIInputModule module = eventObject.AddComponent<InputSystemUIInputModule>();
            module.AssignDefaultActions();
        }

        private GameObject BuildFilledShape(string name, Transform parent, Mesh mesh, Color color, float z)
        {
            GameObject shape = new(name);
            shape.transform.SetParent(parent);
            shape.transform.position = new Vector3(0f, 0f, z);
            shape.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = shape.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = whiteMaterial;
            renderer.material.color = color;
            return shape;
        }

        private Transform BuildRing(string name, Transform parent, float radius, float width, int segments, Color color)
        {
            GameObject ring = new(name);
            ring.transform.SetParent(parent);
            LineRenderer line = ring.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.loop = true;
            line.positionCount = segments;
            line.startWidth = line.endWidth = width;
            line.material = whiteMaterial;
            line.startColor = line.endColor = color;
            for (int i = 0; i < segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                line.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius));
            }
            return ring.transform;
        }

        private static Mesh BuildCircleMesh(float radius, int segments)
        {
            Vector3[] vertices = new Vector3[segments + 1];
            int[] triangles = new int[segments * 3];
            for (int i = 0; i < segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                vertices[i + 1] = new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius);
                int triangle = i * 3;
                triangles[triangle] = 0;
                triangles[triangle + 1] = i + 1;
                triangles[triangle + 2] = i == segments - 1 ? 1 : i + 2;
            }
            Mesh mesh = new() { name = "Runtime Circle" };
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh BuildDiamondMesh(float radius)
        {
            Mesh mesh = new() { name = "Runtime Diamond" };
            mesh.vertices = new[] { Vector3.zero, Vector3.up * radius, Vector3.right * radius, Vector3.down * radius, Vector3.left * radius };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3, 0, 3, 4, 0, 4, 1 };
            mesh.RecalculateBounds();
            return mesh;
        }

        private Text CreateText(string name, Transform parent, string value, int size, TextAnchor alignment,
            Vector2 anchorMin, Vector2 anchorMax, Color color, Vector2? anchoredPosition = null, Vector2? sizeDelta = null)
        {
            RectTransform rect = CreateRect(name, parent, anchorMin, anchorMax,
                anchoredPosition ?? Vector2.zero, sizeDelta ?? Vector2.zero);
            Text text = rect.gameObject.AddComponent<Text>();
            text.font = uiFont;
            text.text = value;
            text.fontSize = size;
            text.alignment = alignment;
            text.color = color;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        private static RectTransform CreateRect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 anchoredPosition, Vector2 sizeDelta)
        {
            GameObject gameObject = new(name, typeof(RectTransform));
            RectTransform rect = (RectTransform)gameObject.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;
            return rect;
        }
    }
}
