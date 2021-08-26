using System.Collections;
using System.Collections.Generic;
using DCL;
using UnityEngine;
using UnityEngine.UI;

public class TestUI : MonoBehaviour
{
    public Slider nameCountSlider;
    public Text nameCountSliderText;
    public Button faceVisibleButton;
    public Text faceVisibleText;

    private bool faceEnabled = true;

    void Awake()
    {
        nameCountSlider.onValueChanged.AddListener( OnNameCountChange );
        faceVisibleButton.onClick.AddListener( OnFaceVisibleButtonChange );
    }

    private void OnFaceVisibleButtonChange()
    {
        faceEnabled = !faceEnabled;
        DataStore.i.avatarsLOD.facesEnabled.Set( faceEnabled );
        faceVisibleText.text = $"FACE ENABLED: {faceEnabled}";
    }

    private void OnNameCountChange(float value)
    {
        DataStore.i.avatarsLOD.maxNames.Set( (int)value );
        nameCountSliderText.text = $"MAX NAMES: {(int)value}";
    }
}