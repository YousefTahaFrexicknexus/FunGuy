using UnityEngine;
using UnityEngine.UI;

namespace Funguy.IdkPlatformer
{
    [DisallowMultipleComponent]
    public sealed class PlayerSpeedMeterHud : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private MovementMotor movementMotor;
        [SerializeField] private Canvas targetCanvas;
        [SerializeField] private bool autoFindMovementMotor = true;
        [SerializeField] private bool autoFindCanvas = true;

        [Header("Display")]
        [SerializeField] private bool usePlanarSpeed = true;
        [SerializeField] private float speedDisplayMultiplier = 1f;
        [SerializeField] private float maxDisplaySpeed = 48f;
        [SerializeField] private float smoothing = 10f;
        [SerializeField] private string speedLabel = "SPEED";
        [SerializeField] private string unitsSuffix = " u/s";

        [Header("Layout")]
        [SerializeField] private Vector2 panelSize = new(120f, 360f);
        [SerializeField] private Vector2 anchoredOffset = new(-72f, 0f);

        [Header("Colors")]
        [SerializeField] private Color panelColor = new(0.07f, 0.11f, 0.15f, 0.72f);
        [SerializeField] private Color trackColor = new(0.16f, 0.22f, 0.28f, 0.92f);
        [SerializeField] private Color lowSpeedColor = new(0.33f, 0.81f, 0.65f, 0.95f);
        [SerializeField] private Color highSpeedColor = new(0.98f, 0.56f, 0.24f, 0.98f);
        [SerializeField] private Color textColor = new(0.97f, 0.99f, 1f, 0.98f);

        private RectTransform runtimeRoot;
        private Image fillImage;
        private Text labelText;
        private Text valueText;
        private float displayedSpeed;

        private void Reset()
        {
            movementMotor = GetComponent<MovementMotor>();
        }

        private void Awake()
        {
            ResolveMovementMotor();
            EnsureRuntimeUi();
        }

        private void OnEnable()
        {
            EnsureRuntimeUi();
            SetUiVisible(true);
            displayedSpeed = MeasureTargetSpeed();
            RefreshUi();
        }

        private void OnDisable()
        {
            SetUiVisible(false);
        }

        private void OnDestroy()
        {
            if (runtimeRoot != null)
            {
                Destroy(runtimeRoot.gameObject);
            }
        }

        private void OnValidate()
        {
            speedDisplayMultiplier = Mathf.Max(0.01f, speedDisplayMultiplier);
            maxDisplaySpeed = Mathf.Max(0.1f, maxDisplaySpeed);
            smoothing = Mathf.Max(0f, smoothing);
            panelSize.x = Mathf.Max(72f, panelSize.x);
            panelSize.y = Mathf.Max(160f, panelSize.y);

            if (string.IsNullOrWhiteSpace(speedLabel))
            {
                speedLabel = "SPEED";
            }

            if (labelText != null)
            {
                labelText.text = speedLabel;
            }
        }

        private void Update()
        {
            EnsureRuntimeUi();

            float targetSpeed = MeasureTargetSpeed();
            float blendFactor = smoothing <= 0f
                ? 1f
                : 1f - Mathf.Exp(-smoothing * Time.unscaledDeltaTime);

            displayedSpeed = Mathf.Lerp(displayedSpeed, targetSpeed, blendFactor);
            RefreshUi();
        }

        private void EnsureRuntimeUi()
        {
            if (runtimeRoot != null)
            {
                return;
            }

            Canvas canvas = ResolveCanvas();
            if (canvas == null)
            {
                return;
            }

            Sprite uiSprite = Resources.GetBuiltinResource<Sprite>("UI/Skin/UISprite.psd");
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            GameObject rootObject = new("SpeedMeter", typeof(RectTransform), typeof(Image));
            rootObject.transform.SetParent(canvas.transform, false);

            runtimeRoot = rootObject.GetComponent<RectTransform>();
            runtimeRoot.anchorMin = new Vector2(1f, 0.5f);
            runtimeRoot.anchorMax = new Vector2(1f, 0.5f);
            runtimeRoot.pivot = new Vector2(1f, 0.5f);
            runtimeRoot.sizeDelta = panelSize;
            runtimeRoot.anchoredPosition = anchoredOffset;

            Image panelImage = rootObject.GetComponent<Image>();
            ConfigureImage(panelImage, uiSprite, panelColor, Image.Type.Sliced);

            labelText = CreateText(
                "Label",
                runtimeRoot,
                font,
                24,
                FontStyle.Bold,
                TextAnchor.MiddleCenter,
                speedLabel,
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -24f),
                new Vector2(-24f, 40f));

            RectTransform trackRect = CreateImage(
                "Track",
                runtimeRoot,
                uiSprite,
                trackColor,
                Image.Type.Sliced,
                out Image trackImage,
                new Vector2(0f, 0f),
                new Vector2(1f, 1f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(30f, 72f),
                new Vector2(-30f, -78f));

            trackImage.raycastTarget = false;

            RectTransform fillRect = CreateImage(
                "Fill",
                trackRect,
                uiSprite,
                lowSpeedColor,
                Image.Type.Filled,
                out fillImage,
                Vector2.zero,
                Vector2.one,
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                Vector2.zero,
                Vector2.zero);

            fillRect.SetAsFirstSibling();
            fillImage.fillMethod = Image.FillMethod.Vertical;
            fillImage.fillOrigin = (int)Image.OriginVertical.Bottom;
            fillImage.fillAmount = 0f;
            fillImage.raycastTarget = false;

            valueText = CreateText(
                "Value",
                runtimeRoot,
                font,
                28,
                FontStyle.Bold,
                TextAnchor.MiddleCenter,
                "0.0 u/s",
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 24f),
                new Vector2(-24f, 56f));
        }

        private float MeasureTargetSpeed()
        {
            if (!ResolveMovementMotor())
            {
                return 0f;
            }

            Vector3 velocity = movementMotor.Velocity;
            if (usePlanarSpeed)
            {
                velocity = Vector3.ProjectOnPlane(velocity, movementMotor.UpDirection);
            }

            return velocity.magnitude * speedDisplayMultiplier;
        }

        private float ResolveReferenceSpeed()
        {
            float referenceSpeed = Mathf.Max(0.1f, maxDisplaySpeed) * speedDisplayMultiplier;
            if (!ResolveMovementMotor())
            {
                return referenceSpeed;
            }

            MovementTuningProfile tuningProfile = movementMotor.TuningProfile;
            if (tuningProfile == null)
            {
                return referenceSpeed;
            }

            float profileReferenceSpeed = usePlanarSpeed
                ? tuningProfile.MaxSpeed
                : tuningProfile.MaxSpeed + tuningProfile.DashForce;

            return Mathf.Max(referenceSpeed, profileReferenceSpeed * speedDisplayMultiplier);
        }

        private bool ResolveMovementMotor()
        {
            if (movementMotor != null)
            {
                return true;
            }

            if (TryGetComponent(out movementMotor))
            {
                return true;
            }

            if (!autoFindMovementMotor)
            {
                return false;
            }

            movementMotor = FindFirstObjectByType<MovementMotor>();
            return movementMotor != null;
        }

        private Canvas ResolveCanvas()
        {
            if (targetCanvas != null)
            {
                return targetCanvas;
            }

            if (autoFindCanvas)
            {
                targetCanvas = FindFirstObjectByType<Canvas>();
            }

            if (targetCanvas != null)
            {
                return targetCanvas;
            }

            GameObject canvasObject = new("HUD", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            targetCanvas = canvasObject.GetComponent<Canvas>();
            targetCanvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;

            return targetCanvas;
        }

        private void RefreshUi()
        {
            if (fillImage == null || valueText == null || labelText == null)
            {
                return;
            }

            float normalizedSpeed = Mathf.Clamp01(displayedSpeed / ResolveReferenceSpeed());
            fillImage.fillAmount = normalizedSpeed;
            fillImage.color = Color.Lerp(lowSpeedColor, highSpeedColor, normalizedSpeed);
            labelText.text = speedLabel;
            valueText.text = $"{displayedSpeed:0.0}{unitsSuffix}";
        }

        private void SetUiVisible(bool visible)
        {
            if (runtimeRoot == null)
            {
                return;
            }

            runtimeRoot.gameObject.SetActive(visible);
        }

        private RectTransform CreateImage(
            string objectName,
            Transform parent,
            Sprite sprite,
            Color color,
            Image.Type imageType,
            out Image image,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 anchoredPosition,
            Vector2 offsetMin,
            Vector2 offsetMax)
        {
            GameObject imageObject = new(objectName, typeof(RectTransform), typeof(Image));
            imageObject.transform.SetParent(parent, false);

            RectTransform rectTransform = imageObject.GetComponent<RectTransform>();
            rectTransform.anchorMin = anchorMin;
            rectTransform.anchorMax = anchorMax;
            rectTransform.pivot = pivot;
            rectTransform.anchoredPosition = anchoredPosition;
            rectTransform.offsetMin = offsetMin;
            rectTransform.offsetMax = offsetMax;

            image = imageObject.GetComponent<Image>();
            ConfigureImage(image, sprite, color, imageType);
            return rectTransform;
        }

        private Text CreateText(
            string objectName,
            Transform parent,
            Font font,
            int fontSize,
            FontStyle fontStyle,
            TextAnchor alignment,
            string text,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 anchoredPosition,
            Vector2 sizeDelta)
        {
            GameObject textObject = new(objectName, typeof(RectTransform), typeof(Text), typeof(Outline));
            textObject.transform.SetParent(parent, false);

            RectTransform rectTransform = textObject.GetComponent<RectTransform>();
            rectTransform.anchorMin = anchorMin;
            rectTransform.anchorMax = anchorMax;
            rectTransform.pivot = pivot;
            rectTransform.anchoredPosition = anchoredPosition;
            rectTransform.sizeDelta = sizeDelta;

            Text uiText = textObject.GetComponent<Text>();
            uiText.font = font;
            uiText.fontSize = fontSize;
            uiText.fontStyle = fontStyle;
            uiText.alignment = alignment;
            uiText.color = textColor;
            uiText.raycastTarget = false;
            uiText.text = text;

            Outline outline = textObject.GetComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.35f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);

            return uiText;
        }

        private static void ConfigureImage(Image image, Sprite sprite, Color color, Image.Type imageType)
        {
            image.sprite = sprite;
            image.type = imageType;
            image.color = color;
            image.raycastTarget = false;
        }
    }
}
