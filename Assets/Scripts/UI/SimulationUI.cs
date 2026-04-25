using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SimulationUI : MonoBehaviour
{
    public SimulationController simulationController;

    public Button playPauseButton;
    public Button stepButton;
    public Button resetButton;

    public Slider speedSlider;
    public TMP_Text speedLabel;
    public TMP_Text yearLabel;
    public TMP_Text playPauseLabel;

    private void Awake()
    {
        if (playPauseLabel == null && playPauseButton != null)
        {
            playPauseLabel = playPauseButton.GetComponentInChildren<TMP_Text>();
        }

        if (playPauseButton != null)
        {
            playPauseButton.onClick.AddListener(OnPlayPauseClicked);
        }

        if (stepButton != null)
        {
            stepButton.onClick.AddListener(OnStepClicked);
        }

        if (resetButton != null)
        {
            resetButton.onClick.AddListener(OnResetClicked);
        }

        if (speedSlider != null)
        {
            speedSlider.minValue = 1f;
            speedSlider.maxValue = 20f;
            speedSlider.wholeNumbers = true;
            speedSlider.onValueChanged.AddListener(OnSpeedChanged);
        }
    }

    private void Start()
    {
        RefreshState();
    }

    public void RefreshState()
    {
        if (simulationController == null)
        {
            return;
        }

        if (speedSlider != null)
        {
            speedSlider.SetValueWithoutNotify(simulationController.stepsPerFrame);
        }

        UpdateSpeedLabel(simulationController.stepsPerFrame);
        SetYear(simulationController.currentYear);

        if (playPauseLabel != null)
        {
            playPauseLabel.text = simulationController.isRunning ? "Pause" : "Play";
        }

        if (stepButton != null)
        {
            stepButton.interactable = !simulationController.isRunning;
        }
    }

    public void SetYear(int year)
    {
        if (yearLabel != null)
        {
            yearLabel.text = $"Year: {year}";
        }
    }

    private void OnPlayPauseClicked()
    {
        if (simulationController == null)
        {
            return;
        }

        simulationController.TogglePlayPause();
        RefreshState();
    }

    private void OnStepClicked()
    {
        if (simulationController == null)
        {
            return;
        }

        simulationController.StepOnce();
        RefreshState();
    }

    private void OnResetClicked()
    {
        if (simulationController == null)
        {
            return;
        }

        simulationController.ResetSimulation();
        RefreshState();
    }

    private void OnSpeedChanged(float value)
    {
        int speed = Mathf.RoundToInt(value);
        if (simulationController != null)
        {
            simulationController.SetStepsPerFrame(speed);
        }

        UpdateSpeedLabel(speed);
    }

    private void UpdateSpeedLabel(int speed)
    {
        if (speedLabel != null)
        {
            speedLabel.text = $"{speed} ticks/frame";
        }
    }
}
