using UnityEngine;
using UnityEditor;
using System;

[RequireComponent(typeof(AudioSource))]
public class KeyframeListener : MonoBehaviour
{
    private AudioSource audioClip;
    const string summaryOne = "Higher sample rates will be more precise since there will be more samples per band. The calculation will be; Sample / 8 (bands).\n\n";
    const string summaryTwo = "i.e: 512 samples / 8 bands = 64 samples per band and then we calculate the average of the 64 samples in each band to get our target value.\n\n";
    const string summaryThree = "1024 samples / 8 bands = 128 samples per band and then we calculate the average of the 128 samples in each band to get our target value.";
    //Sample sizing must be a power of 2 from min 64 to max 8192 but we want our min to 512 since anything lower will have large inaccuracies.
    [
        RangeTargets(512, 1024, 2048, 4096, 8192),
        Tooltip(summaryOne + summaryTwo + summaryThree)
    ]
    public int samples;
    public static float[] generateSamples;
    public static float[] frequencyBands = new float[8];
    public static float[] bandBuffer = new float[8];
    private float[] bufferDecrease = new float[8];

    void Start()
    {
        if (GetComponent<AudioSource>() != null)
        {
            //Generate the samples according to the inputted sample sizing from inspector
            generateSamples = new float[GetSampleSize()];
            audioClip = GetComponent<AudioSource>();
        }
    }

    void Update()
    {
        if (audioClip != null)
        {
            GetAudioSourceSpectrum();
            FrequencyBands();
            BandBuffer();
        }
    }

    void GetAudioSourceSpectrum()
    {
        audioClip.GetSpectrumData(generateSamples, 0, FFTWindow.Blackman);
    }


    void FrequencyBands()
    {
        int count = 0;
        for (int i = 0; i < frequencyBands.Length; i++)
        {
            float average = 0;
            int sampleCount = (int)Mathf.Pow(2, i) * 2;

            if (i == 7)
            {
                sampleCount += 2;
            }

            for (int j = 0; j < sampleCount; j++)
            {
                average += generateSamples[count] * (count + 1);
                count++;
            }

            average /= count;
            frequencyBands[i] = average * 10;
        }
    }

    void BandBuffer()
    {
        for (int i = 0; i < frequencyBands.Length; ++i)
        {
            if (frequencyBands[i] > bandBuffer[i])
            {
                bandBuffer[i] = frequencyBands[i];
                bufferDecrease[i] = 0.005f;
            }

            //if (frequencyBands[i] < bandBuffer[i])
            //{
            //    bandBuffer[i] -= bufferDecrease[i];
            //    bufferDecrease[i] *= 1.4f;
            //}
            if (frequencyBands[i] < bandBuffer[i])
            {
                bufferDecrease[i] = (bandBuffer[i] - frequencyBands[i]) / 8;
                bandBuffer[i] -= bufferDecrease[i];
            }
        }
    }

    private int GetSampleSize()
    {
        //since our slider will be a value between 0-4 we need to return the correct sample sizing according to slider value
        //since we DEFINITELY do not want any sample size to be ie 2
        switch(samples)
        {
            case 0: return 512;
            case 1: return 1024;
            case 2: return 2048;
            case 3: return 4096;
            case 4: return 8192;
        }
        //default the value to 512 samples if the slider somehow fucks up (lol)
        return 512;
    }
}

[AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
public sealed class RangeTargets : PropertyAttribute
{
    public int[] targetValues;

    public RangeTargets(params int[] values)
    {
        targetValues = values;
    }
}

[CustomPropertyDrawer(typeof(RangeTargets))]
public sealed class RangeTargetsDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        RangeTargets targets = (RangeTargets)base.attribute;
        //position the value of our selected sample size next to the slider
        EditorGUI.LabelField(new Rect(new Vector2(position.x * 6f, position.y), position.size), targets.targetValues[property.intValue].ToString());
        //create our slider which will be min 0 and max equal to the size of the target values array length - 1
        EditorGUI.IntSlider(position, property, 0, targets.targetValues.Length - 1);
    }
}

