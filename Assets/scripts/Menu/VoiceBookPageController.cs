using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Connects the Living Book Voice page to the existing local microphone provider.
/// It presents settings only; microphone capture remains owned by VoiceAmplitudeProvider.
/// </summary>
public sealed class VoiceBookPageController : MonoBehaviour
{
    private enum VoiceDetail
    {
        InputDevice,
        OutputDevice,
        MicrophoneTest
    }

    [Header("Voice Source")]
    [SerializeField] private VoiceAmplitudeProvider voiceAmplitudeProvider;
    [SerializeField] private WindowsAudioOutputDeviceProvider outputDeviceProvider;

    [Header("Existing Voice Page Controls")]
    [SerializeField] private Slider microphoneLevelMeter;

    private readonly TMP_Text[] detailTexts = new TMP_Text[6];
    private readonly BookMenuItem[] detailMenuItems = new BookMenuItem[5];
    private VoiceDetail selectedDetail = VoiceDetail.InputDevice;
    private bool pageOpen;

    private void Awake()
    {
        ConfigureMeter();
    }

    private void OnEnable()
    {
        if (outputDeviceProvider != null)
        {
            outputDeviceProvider.DeviceNameChanged += HandleOutputDeviceNameChanged;
        }
    }

    private void OnDisable()
    {
        if (outputDeviceProvider != null)
        {
            outputDeviceProvider.DeviceNameChanged -= HandleOutputDeviceNameChanged;
        }
    }

    private void Update()
    {
        if (!pageOpen || voiceAmplitudeProvider == null)
        {
            return;
        }

        float amplitude = voiceAmplitudeProvider.CurrentAmplitude;
        if (microphoneLevelMeter != null)
        {
            microphoneLevelMeter.SetValueWithoutNotify(amplitude);
        }

        if (selectedDetail == VoiceDetail.MicrophoneTest && detailTexts[1] != null)
        {
            detailTexts[1].text = BuildMeterText(amplitude);
        }
    }

    public void PreparePage(
        List<TMP_Text> texts,
        List<string> targets,
        TMP_Text title,
        TMP_Text line1,
        TMP_Text line2,
        TMP_Text line3,
        TMP_Text line4,
        TMP_Text line5,
        BookMenuItem menuItem1,
        BookMenuItem menuItem2,
        BookMenuItem menuItem3,
        BookMenuItem menuItem4,
        BookMenuItem menuItem5)
    {
        detailTexts[0] = title;
        detailTexts[1] = line1;
        detailTexts[2] = line2;
        detailTexts[3] = line3;
        detailTexts[4] = line4;
        detailTexts[5] = line5;
        detailMenuItems[0] = menuItem1;
        detailMenuItems[1] = menuItem2;
        detailMenuItems[2] = menuItem3;
        detailMenuItems[3] = menuItem4;
        detailMenuItems[4] = menuItem5;

        string[] values = BuildSelectedDetailValues();
        for (int i = 0; i < detailTexts.Length; i++)
        {
            AddEntry(texts, targets, detailTexts[i], values[i]);
        }
    }

    public void SetPageOpen(bool isOpen)
    {
        pageOpen = isOpen;

        if (microphoneLevelMeter != null)
        {
            microphoneLevelMeter.gameObject.SetActive(isOpen);
        }

        if (isOpen && voiceAmplitudeProvider != null)
        {
            voiceAmplitudeProvider.RefreshDevices();
        }

        if (isOpen && outputDeviceProvider != null)
        {
            outputDeviceProvider.Refresh();
        }

        UpdateControlVisibility();
    }

    public void ShowInputDevice()
    {
        SelectDetail(VoiceDetail.InputDevice);
    }

    public void ShowOutputDevice()
    {
        if (outputDeviceProvider != null)
        {
            outputDeviceProvider.Refresh();
        }

        SelectDetail(VoiceDetail.OutputDevice);
    }

    public void ShowMicrophoneTest()
    {
        SelectDetail(VoiceDetail.MicrophoneTest);
    }

    public void SelectPreviousInputDevice()
    {
        CycleInputDevice(-1);
    }

    public void SelectNextInputDevice()
    {
        CycleInputDevice(1);
    }

    private void CycleInputDevice(int direction)
    {
        if (voiceAmplitudeProvider == null)
        {
            return;
        }

        voiceAmplitudeProvider.RefreshDevices();
        IReadOnlyList<string> devices = voiceAmplitudeProvider.AvailableDevices;
        if (devices.Count == 0)
        {
            RefreshSelectedDetail();
            return;
        }
        int currentIndex = IndexOf(devices, voiceAmplitudeProvider.CurrentDevice);
        int nextIndex = (currentIndex + direction + devices.Count) % devices.Count;
        voiceAmplitudeProvider.SetInputDevice(devices[nextIndex]);
        RefreshSelectedDetail();
    }

    private void ConfigureMeter()
    {
        if (microphoneLevelMeter != null)
        {
            microphoneLevelMeter.minValue = 0f;
            microphoneLevelMeter.maxValue = 1f;
            microphoneLevelMeter.interactable = false;
            microphoneLevelMeter.gameObject.SetActive(false);
        }
    }

    private string GetInputDeviceDisplay()
    {
        if (voiceAmplitudeProvider == null)
        {
            return "Unavailable";
        }

        string device = voiceAmplitudeProvider.CurrentDevice;
        if (string.IsNullOrEmpty(device))
        {
            device = voiceAmplitudeProvider.SelectedInputDevice;
        }

        return string.IsNullOrEmpty(device) ? "No Microphone" : device;
    }

    private void SelectDetail(VoiceDetail detail)
    {
        if (!pageOpen || selectedDetail == detail)
        {
            return;
        }

        selectedDetail = detail;
        RefreshSelectedDetail();
        UpdateControlVisibility();
    }

    private void RefreshSelectedDetail()
    {
        string[] values = BuildSelectedDetailValues();
        for (int i = 0; i < detailTexts.Length; i++)
        {
            if (detailTexts[i] != null)
            {
                detailTexts[i].text = values[i];
                detailTexts[i].maxVisibleCharacters = int.MaxValue;
            }
        }
    }

    private string[] BuildSelectedDetailValues()
    {
        string[] values = new string[6];
        switch (selectedDetail)
        {
            case VoiceDetail.InputDevice:
                values[0] = "INPUT DEVICE";
                values[1] = GetInputDeviceDisplay();
                values[4] = "Previous";
                values[5] = "Next";
                break;
            case VoiceDetail.OutputDevice:
                values[0] = "OUTPUT DEVICE";
                values[1] = outputDeviceProvider != null
                    ? outputDeviceProvider.CurrentDeviceName
                    : WindowsAudioOutputDeviceProvider.FallbackDeviceName;
                break;
            case VoiceDetail.MicrophoneTest:
                values[0] = "MICROPHONE TEST";
                values[1] = BuildMeterText(voiceAmplitudeProvider != null ? voiceAmplitudeProvider.CurrentAmplitude : 0f);
                break;
        }

        return values;
    }

    private void HandleOutputDeviceNameChanged()
    {
        if (pageOpen && selectedDetail == VoiceDetail.OutputDevice)
        {
            RefreshSelectedDetail();
        }
    }

    private void UpdateControlVisibility()
    {
        SetMenuAction(0, null);
        SetMenuAction(1, null);
        SetMenuAction(2, null);
        SetMenuAction(3, selectedDetail == VoiceDetail.InputDevice ? SelectPreviousInputDevice : null);
        SetMenuAction(4, selectedDetail == VoiceDetail.InputDevice ? SelectNextInputDevice : null);

        if (microphoneLevelMeter != null)
        {
            microphoneLevelMeter.gameObject.SetActive(pageOpen && selectedDetail == VoiceDetail.MicrophoneTest);
        }

    }

    private void SetMenuAction(int index, UnityEngine.Events.UnityAction action)
    {
        BookMenuItem menuItem = detailMenuItems[index];
        if (menuItem == null)
        {
            return;
        }

        menuItem.SetOnClickAction(action);
        bool hasVisibleText = detailTexts[index + 1] != null && !string.IsNullOrEmpty(detailTexts[index + 1].text);
        menuItem.SetInteractionEnabled(pageOpen && action != null && hasVisibleText);
    }

    private static string BuildMeterText(float amplitude)
    {
        const int segmentCount = 10;
        int activeSegments = Mathf.RoundToInt(Mathf.Clamp01(amplitude) * segmentCount);
        return $"[{new string('|', activeSegments)}{new string('.', segmentCount - activeSegments)}]";
    }

    private static int IndexOf(IReadOnlyList<string> devices, string device)
    {
        for (int i = 0; i < devices.Count; i++)
        {
            if (string.Equals(devices[i], device, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }

    private static void AddEntry(List<TMP_Text> texts, List<string> targets, TMP_Text text, string value)
    {
        texts.Add(text);
        targets.Add(value ?? string.Empty);
    }
}
