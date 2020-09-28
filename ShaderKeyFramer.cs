using System;
using System.Collections;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[RequireComponent(typeof(MeshRenderer))]
public class ShaderKeyFramer : MonoBehaviour
{
    [Tooltip("Toggle this if you want to use a single common frequency for all properties.")]
    public bool globalFrequency;
    [Tooltip("The single common frequency to use if Use Same Frequency is toggled on."), Range(0, 7)]
    public int frequency;
    [Tooltip("Toggle this if you want to set a global value to all filters.")]
    public bool globalFilter;
    [Tooltip("Filter any value below this.")]
    public float globalFilterAllBelow;
    [Tooltip("All filtered values will be equal to this.")]
    public float globalFilterTo;
    public AnimationClip animation;
    public List<ShaderProperties> properties;
    private AnimationCurve[] curve;
    [HideInInspector]
    public Material material;
    [HideInInspector]
    public Shader shader;
    private List<ShaderProperties> keyframeProperties;

    void Start()
    {
        //Clear animation so we get a fresh one with no keyframes or curves
        animation.ClearCurves();
        material = GetComponent<Renderer>().material;

        //Create a new list of the ShaderProperties that has Keyframing enabled (So we dont uselessly write to non existent curves)
        keyframeProperties = properties.Where(x => x._UseForKeyframing).ToList();

        List<AnimationCurve> c = new List<AnimationCurve>();
        for(int i = 0; i < keyframeProperties.Count; i++)
        {
            //add animationcurve for every shaderproperty that we want to keyframe
            c.Add(new AnimationCurve()); 
        }
        curve = c.ToArray();
    }

    void Update()
    {
        //Making sure our material and animation are not null and our curve length is greater than 0
        //if curve would be 0 and we dont have this check we would keyframe into non existent curves
        if(material != null && animation != null && curve.Length > 0)
        {
            for(int i = 0; i < keyframeProperties.Count; i++)
            {
                string targetProperty = keyframeProperties[i]._TrueName;

                if (ShouldKeyFrame(keyframeProperties[i]) && keyframeProperties[i]._PropertyType != ShaderUtil.ShaderPropertyType.Color)
                {
                    //Set the animation curve for the current property index
                    animation.SetCurve("", typeof(MeshRenderer), $"material.{targetProperty}", curve[i]);
                    //Calculate and set the value of our current target property
                    material.SetFloat(targetProperty, CalculateShaderSync(keyframeProperties[i]));
                    //Add the keyframe key to the animation and set it to the current material value (with all audio calculations)
                    curve[i].AddKey(Time.time, material.GetFloat(targetProperty));
                }
                //else if (ShouldKeyFrame(keyframeProperties[i]) && keyframeProperties[i]._PropertyType == ShaderUtil.ShaderPropertyType.Color)
                //{
                //    //Color keyframing here
                //}
                //if debug logging is enabled we want to print the property name with the property current value 
                //this will allow us to see what values we wish to manually set to filter out with our filter variables
                if (keyframeProperties[i]._LogValue)
                {
                    Debug.Log($"{keyframeProperties[i]._Name}: {material.GetFloat(targetProperty)} Type: {keyframeProperties[i]._PropertyType}");
                }
            }
        }
    }

    private float CalculateShaderSync(ShaderProperties sp)
    {
        float result = sp._Offset + KeyframeListener.frequencyBands[sp._Frequency] * sp._Strength;

        //if UseFilter is enabled we want to set all values that are under the FilterAllBelow to the selected filter value (FilterTo)
        //this is so we can filter out minor values so we can ignore keyframing values that are lower than ie 0.3 => 0
        if (sp._UseFilter)
        {
            if (result < sp._FilterAllBelow) result = sp._FilterTo;
        }
        return result;
    }

    private Color CalculateColorSync(ShaderProperties sp)
    {
        sp._Color.r = sp._Color.r * (sp._Offset + KeyframeListener.frequencyBands[sp._Frequency] * sp._Strength);
        sp._Color.b = sp._Color.b * (sp._Offset + KeyframeListener.frequencyBands[sp._Frequency] * sp._Strength);
        sp._Color.g = sp._Color.g * (sp._Offset + KeyframeListener.frequencyBands[sp._Frequency] * sp._Strength);

        return sp._Color;
    }

    private bool ShouldKeyFrame(ShaderProperties sp)
    {
        bool result = true;

        //if we want to keyframe between a specific time frame (ie from 3 seconds to 6 seconds) 
        if(sp._SpecificTime)
        {
            //return true if current time is more than or equal to start time and less than or equal to end time, else we should not keyframe it
            result = Time.time >= sp._StartTime && Time.time <= sp._EndTime;
        }
        return result;
    }
}

//Custom editor so we can set values before playing scene
[CustomEditor(typeof(ShaderKeyFramer), true)]
public class ShaderEditor : Editor
{
    private ShaderKeyFramer skf = null;
    private string searchInput = string.Empty;

    public void OnEnable()
    {
        if (skf == null) AddKeyFramer(); //Creates the serialized object
        if (skf.properties == null) AddProperties(); //Adds all the shader properties to the scripts list
        //Remove collider from object to prevent funky things when uploading later
        if(skf.gameObject.GetComponent<Collider>() != null)
        {
            DestroyImmediate(skf.gameObject.GetComponent<Collider>());
        }

        skf.properties = skf.properties
                        .Where(x =>
                        !x._Name.ToLower().StartsWith("is ") ||
                        !x._Name.ToLower().Contains("active"))
                        .OrderBy(x => x._Name)
                        .ToList();
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        //Draw the animation and same frequency options in inspector
        EditorGUILayout.PropertyField(serializedObject.FindProperty("animation"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("globalFrequency"));

        //If Use Same Frequency is enabled we want to use the same frequency on all shaderproperties
        if (serializedObject.FindProperty("globalFrequency").boolValue)
        {
            //Draw the frequency variable window
            EditorGUILayout.PropertyField(serializedObject.FindProperty("frequency"));
            ChangeValue(skf.properties.ToArray(), "frequency");
        }

        EditorGUILayout.Space();
        EditorGUILayout.Space();
        EditorGUILayout.PropertyField(serializedObject.FindProperty("globalFilter"));

        //If global filter is enabled we want to use the same filters on all of our shaderproperties, so we need to set them accordingly
        if (serializedObject.FindProperty("globalFilter").boolValue)
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty("globalFilterAllBelow"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("globalFilterTo"));
            ChangeValue(skf.properties.ToArray(), "filter");
        }

        EditorGUILayout.Space();
        EditorGUILayout.Space();

        //Search bar to make editing easier so you can find a specific property
        GUILayout.BeginHorizontal(GUI.skin.FindStyle("Toolbar"));
        searchInput = GUILayout.TextField(searchInput, GUI.skin.FindStyle("ToolbarSeachTextField"));
        if (GUILayout.Button("", GUI.skin.FindStyle("ToolbarSeachCancelButton")))
        {
            searchInput = "";
            GUI.FocusControl(null);
        }
        GUILayout.EndHorizontal();

        //if searchinput is not empty we want to display the elements that contains the search string and filter out the ones that dont
        if (!string.IsNullOrEmpty(searchInput))
        {
            DisplaySingleElement(serializedObject.FindProperty("properties"));
        }
        else
        {
            DisplayAll(serializedObject.FindProperty("properties"));
        }

        serializedObject.ApplyModifiedProperties();
    }

    //Change the targetted variable and set the list to obtain the new values
    public void ChangeValue(ShaderProperties[] prop, string varName)
    {
        ShaderProperties[] array = skf.properties.ToArray();
        for (int i = 0; i < skf.properties.Count; i++)
        {
            switch(varName.ToLower())
            {
                case "frequency":
                    array[i]._Frequency = skf.frequency;
                    break;

                case "filter":
                    array[i]._FilterAllBelow = skf.globalFilterAllBelow;
                    array[i]._FilterTo = skf.globalFilterTo;
                    array[i]._UseFilter = skf.globalFilter;
                    break;
            }
        }
        skf.properties = array.ToList();
    }

    //This will be called when gameobject is selected (through onEnable) and set our ShaderKeyFramer object target
    public void AddKeyFramer()
    {
        skf = (ShaderKeyFramer)target;
        skf.material = skf.gameObject.GetComponent<Renderer>().sharedMaterial;
        skf.shader = skf.material.shader;
    }

    //Dynamically create a shaderproperty for each shader property in the actual shader target
    public void AddProperties()
    {
        List<ShaderProperties> p = new List<ShaderProperties>();

        //Check each of the properties in the actual shader target and create out ShaderProperties structure accordingly
        for (int i = 0; i < ShaderUtil.GetPropertyCount(skf.shader); i++)
        {
            //Check for the type of the shader property index since there are some types we never want to change (ie vectors)
            ShaderUtil.ShaderPropertyType type = ShaderUtil.GetPropertyType(skf.shader, i);

            //the true variable inside the shader target (ie _ZoomValue)
            string trueName = ShaderUtil.GetPropertyName(skf.shader, i);
            string displayName = string.Empty;

            //Create a "clean" string of the shader property name (ie _ZoomValue to Zoom Value) that we'll use as the display name
            for (int k = 0; k < trueName.Length; k++)
            {
                if (char.IsUpper(trueName[k]) && k != 1) displayName += $" {trueName[k]}";
                else displayName += trueName[k];
            }

            ShaderProperties prop = new ShaderProperties();

            //since we dont want to modify textures we'll skip all of these
            if (type != ShaderUtil.ShaderPropertyType.TexEnv)
            {
                //Create our properties structure and add it to our list
                prop = new ShaderProperties
                {
                    _TrueName = trueName,
                    _Name = $"{displayName.Replace("_", "")} ({type.ToString()})",
                    _Strength = 0,
                    _Offset = 0,
                    _UseForKeyframing = false,
                    _UseFilter = false,
                    _LogValue = false,
                    _Frequency = 0,
                    _PropertyType = type
                };
                p.Add(prop);
            }
        }

        //Update the ShaderKeyFramers shaderproperties list with the list of structures we created in this method
        skf.properties = p;
        serializedObject.ApplyModifiedProperties();
    }

    public void DisplayAll(SerializedProperty list)
    {
        for (int i = 0; i < list.arraySize; i++)
        {
            //Use .hasChildren to make sure all of the subelements of an element shows up in the list
            EditorGUILayout.PropertyField(list.GetArrayElementAtIndex(i), list.GetArrayElementAtIndex(i).hasChildren);
        }
    }

    public void DisplaySingleElement(SerializedProperty list)
    {
        for (int i = 0; i < list.arraySize; i++)
        {
            //Convert displayname to lower and compare it to searchinput to lower so we can search without case sensitivity
            if(list.GetArrayElementAtIndex(i).displayName.ToLower().Contains(searchInput.ToLower()))
                EditorGUILayout.PropertyField(list.GetArrayElementAtIndex(i), list.GetArrayElementAtIndex(i).hasChildren);
        }
    }
}

//haha cant use abstraction for inspector haha
[System.Serializable]
public struct ShaderProperties
{
    [HideInInspector]
    public string _Name;
    [HideInInspector]
    public string _TrueName;
    [Tooltip("Use this to toggle whether you want to use this property or not. (off by default)")]
    public bool _UseForKeyframing;
    [Space(10)]
    [Tooltip("The strength that the float should be multiplied by in the selected frequency.")]
    public float _Strength;
    [Tooltip("Will be additively added in the strength calculation.")]
    public float _Offset;
    [Range(0, 7), Tooltip("The audio frequency band to sync with.")]
    public int _Frequency;
    [Space(10)]
    [Tooltip("Toggle if you want to set a custom filter value.")]
    public bool _UseFilter;
    [Tooltip("Ignores any value below this to filter out subtle and very minor changes.")]
    public float _FilterAllBelow;
    [Tooltip("The value you want to filter to (set it to the default \"resting\" value of the property)")]
    public float _FilterTo;
    [Space(10)]
    [Tooltip("Toggle to show the value in the debug log so you can see what values you want to filter out in smaller changes of audio.")]
    public bool _LogValue;
    [Space(10)]
    [Tooltip("Toggle this if you want to keyframe between specific times (ie from 5 seconds to 15 seconds).")]
    public bool _SpecificTime;
    [Tooltip("Start keyframing this property at this time (If Specific Time is toggled).")]
    public float _StartTime;
    [Tooltip("Stop keyframing this property at this time (If Specific Time is toggled).")]
    public float _EndTime;
    [Tooltip("This will set a default color value for this property that'll fade as the frequency band gets weaker and increase in intesity if the frequency band becomes stronger.")]
    public Color _Color;
    [HideInInspector]
    public ShaderUtil.ShaderPropertyType _PropertyType;
}