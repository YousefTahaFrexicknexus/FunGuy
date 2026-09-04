using UnityEngine;
using UnityEngine.UI;

using TMPro;

public class SpeedMeter_HUDUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] ProgressBar speedMeter;
    [SerializeField] Scrollbar speedMeterScrollbar;
    [SerializeField] TextMeshProUGUI speedText;

    void OnEnable()
    {
        // TODO: subscribe to gameplay events
        GameplayEvents.OnSpeedChanged += UpdateSpeedMeter;
    }

    void OnDisable()
    {
        GameplayEvents.OnSpeedChanged -= UpdateSpeedMeter;
    }

    public void UpdateSpeedMeter(float _speed, float _maxSpeed)
    {
        speedMeter.AnimateProgress(Mathf.Clamp01(_speed / _maxSpeed));
        speedText.text = Mathf.Round(_speed).ToString();
        
        speedMeterScrollbar.value = Mathf.Clamp01(_speed / _maxSpeed);
    }
}
