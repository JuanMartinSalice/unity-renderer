using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace DCL.Skybox
{
    public class SkyboxEditorWindow : EditorWindow
    {
        private List<SkyboxConfiguration> configurations;
        private List<string> configurationNames;
        private int selectedConfigurationIndex;
        private int newConfigIndex;
        private SkyboxConfiguration selectedConfiguration;

        public bool isPaused;
        public float timeOfTheDay;
        private Material selectedMat;
        private Vector2 panelScrollPos;
        private Light directionalLight;
        private bool creatingNewConfig;
        private string newConfigName;

        private bool showBackgroundLayer;
        private bool showAmbienLayer;
        private bool showFogLayer;
        private bool showDLLayer;

        public static SkyboxEditorWindow instance { get { return GetWindow<SkyboxEditorWindow>(); } }

        private void OnEnable()
        {
            if (selectedConfiguration == null)
            {
                EnsureDependencies();
            }

        }

        void OnFocus()
        {
            if (selectedConfiguration == null)
            {
                OnEnable();
            }
        }

        [MenuItem("Window/Skybox Editor")]
        static void Init()
        {

            SkyboxEditorWindow window = (SkyboxEditorWindow)EditorWindow.GetWindow(typeof(SkyboxEditorWindow));
            //SkyboxEditorWindow.selectedConfiguration = new SkyboxConfiguration();
            window.minSize = new Vector2(500, 500);
            window.Show();
            window.InitializeWindow();
        }

        public void InitializeWindow()
        {
            if (Application.isPlaying)
            {
                if (SkyboxController.i != null)
                {
                    isPaused = SkyboxController.i.IsPaused();
                }

            }
            EnsureDependencies();
        }

        void OnGUI()
        {
            GUILayout.BeginArea(new Rect(10, 0, position.width - 20, position.height));

            GUILayout.Space(32);
            RenderConfigurations();
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

            GUILayout.Space(32);
            RenderTimePanel();
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
            GUILayout.Space(12);

            panelScrollPos = EditorGUILayout.BeginScrollView(panelScrollPos, GUILayout.Width(position.width - 20), GUILayout.Height(position.height - 90));
            GUILayout.Space(32);
            showBackgroundLayer = EditorGUILayout.Foldout(showBackgroundLayer, "BG Layer", true);
            if (showBackgroundLayer)
            {
                EditorGUI.indentLevel++;
                RenderBackgroundColorLayer();
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

            GUILayout.Space(32);
            showAmbienLayer = EditorGUILayout.Foldout(showAmbienLayer, "Ambient Layer", true);
            if (showAmbienLayer)
            {
                EditorGUI.indentLevel++;
                RenderAmbientLayer();
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

            GUILayout.Space(32);
            showFogLayer = EditorGUILayout.Foldout(showFogLayer, "Fog Layer", true);
            if (showFogLayer)
            {
                EditorGUI.indentLevel++;
                RenderFogLayer();
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

            GUILayout.Space(32);
            showDLLayer = EditorGUILayout.Foldout(showDLLayer, "Directional Light Layer", true);
            if (showDLLayer)
            {
                EditorGUI.indentLevel++;
                RenderDirectionalLightLayer();
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

            GUILayout.Space(32);

            // Render Slots
            RenderSlots();

            // Render Texture Layers
            //RenderTextureLayers();

            GUILayout.Space(300);
            EditorGUILayout.EndScrollView();
            GUILayout.EndArea();
            if (GUI.changed)
            {
                ApplyOnMaterial();
            }
        }

        private void Update()
        {
            if (selectedConfiguration == null || isPaused)
            {
                return;
            }

            timeOfTheDay += Time.deltaTime;
            timeOfTheDay = Mathf.Clamp(timeOfTheDay, 0.01f, 24);

            ApplyOnMaterial();

            if (timeOfTheDay >= 24)
            {
                timeOfTheDay = 0.01f;
            }
        }

        private void EnsureDependencies()
        {
            if (selectedConfiguration == null)
            {
                UpdateConfigurationsList();
            }

            UpdateMaterial();

            EditorUtility.SetDirty(selectedConfiguration);

            // Cache directional light reference
            directionalLight = GameObject.FindObjectsOfType<Light>(true).Where(s => s.type == LightType.Directional).FirstOrDefault();
        }

        //bool initialized = false;
        MaterialReferenceContainer.Mat_Layer matLayer = null;

        void InitializeMaterial()
        {

            matLayer = MaterialReferenceContainer.i.GetMat_LayerForLayers(selectedConfiguration.slots.Count);

            if (matLayer == null)
            {
                matLayer = MaterialReferenceContainer.i.materials[0];
            }

            selectedMat = matLayer.material;
            selectedConfiguration.ResetMaterial(selectedMat, matLayer.numberOfSlots);
            RenderSettings.skybox = selectedMat;
            //initialized = true;
        }

        private void UpdateMaterial() { InitializeMaterial(); }

        SkyboxConfiguration AddNewConfiguration(string name)
        {
            SkyboxConfiguration temp = null;
            temp = ScriptableObject.CreateInstance<SkyboxConfiguration>();
            temp.skyboxID = name;

            string path = AssetDatabase.GenerateUniqueAssetPath("Assets/Rendering/ProceduralSkybox/ToolProceduralSkybox/Scripts/Resources/Skybox Configurations/" + name + ".asset");
            AssetDatabase.CreateAsset(temp, path);
            AssetDatabase.SaveAssets();

            // Add number of slots available in the material.
            //TODO: Change this to something new.
            for (int i = 0; i < 5; i++)
            {
                temp.AddSlots(i);
            }

            return temp;
        }

        private void RenderConfigurations()
        {
            GUILayout.Label("Configurations", EditorStyles.boldLabel);

            GUIStyle tStyle = new GUIStyle();
            tStyle.alignment = TextAnchor.MiddleCenter;
            tStyle.margin = new RectOffset(150, 200, 0, 0);
            GUILayout.Label("Loaded: " + selectedConfiguration.skyboxID);
            GUILayout.BeginHorizontal(tStyle);

            if (creatingNewConfig)
            {
                GUILayout.Label("Name");
                newConfigName = EditorGUILayout.TextField(newConfigName, GUILayout.Width(200));

                if (GUILayout.Button("Create", GUILayout.Width(50)))
                {
                    // Make new configuration
                    selectedConfiguration = AddNewConfiguration(newConfigName);

                    // Update configuration list
                    UpdateConfigurationsList();
                    creatingNewConfig = false;
                }

                if (GUILayout.Button("Cancel", GUILayout.Width(50)))
                {
                    creatingNewConfig = false;
                }
            }
            else
            {
                newConfigIndex = EditorGUILayout.Popup(selectedConfigurationIndex, configurationNames.ToArray(), GUILayout.Width(200));

                if (newConfigIndex != selectedConfigurationIndex)
                {
                    selectedConfiguration = configurations[newConfigIndex];
                    selectedConfigurationIndex = newConfigIndex;

                    UpdateSlotsID();
                }

                if (GUILayout.Button("+", GUILayout.Width(50)))
                {
                    creatingNewConfig = true;
                }
            }


            GUILayout.EndHorizontal();
        }

        private void UpdateConfigurationsList()
        {
            SkyboxConfiguration[] tConfigurations = Resources.LoadAll<SkyboxConfiguration>("Skybox Configurations/");
            configurations = new List<SkyboxConfiguration>(tConfigurations);
            configurationNames = new List<string>();

            // If no configurations exist, make and select new one.
            if (configurations == null || configurations.Count < 1)
            {
                selectedConfiguration = AddNewConfiguration("Generic Skybox");

                configurations = new List<SkyboxConfiguration>();
                configurations.Add(selectedConfiguration);
            }

            if (selectedConfiguration == null)
            {
                selectedConfiguration = configurations[0];
            }

            for (int i = 0; i < configurations.Count; i++)
            {
                configurationNames.Add(configurations[i].skyboxID);
                if (selectedConfiguration == configurations[i])
                {
                    selectedConfigurationIndex = i;
                }
            }

            UpdateSlotsID();

            InitializeMaterial();

            //isPaused = true;

            if (!Application.isPlaying)
            {
                isPaused = true;
            }


        }

        /// <summary>
        /// Update slot ID for old configurations. (Backward comptibility)
        /// </summary>
        private void UpdateSlotsID()
        {
            for (int i = 0; i < selectedConfiguration.slots.Count; i++)
            {
                selectedConfiguration.slots[i].UpdateSlotsID(i);
            }
        }

        private void RenderTimePanel()
        {

            GUILayout.Label("Preview", EditorStyles.boldLabel);

            GUILayout.BeginHorizontal();
            GUILayout.Label("Time : " + timeOfTheDay, EditorStyles.label, GUILayout.MinWidth(100));
            timeOfTheDay = EditorGUILayout.Slider(timeOfTheDay, 0.01f, 24.00f, GUILayout.MinWidth(100), GUILayout.MaxWidth(300));
            EditorGUILayout.LabelField((GetNormalizedDayTime() * 100).ToString("f2"), GUILayout.MaxWidth(50));

            if (isPaused)
            {
                if (GUILayout.Button("Play"))
                {
                    ResumeTime();
                }
            }
            else
            {
                if (GUILayout.Button("Pause"))
                {
                    PauseTime();
                }
            }

            GUILayout.EndHorizontal();
        }

        void ResumeTime()
        {
            isPaused = false;
            if (Application.isPlaying)
            {
                if (SkyboxController.i != null)
                {
                    SkyboxController.i.ResumeTime(true, timeOfTheDay);
                }
            }
        }

        void PauseTime()
        {
            isPaused = true;
            if (Application.isPlaying)
            {
                if (SkyboxController.i != null)
                {
                    SkyboxController.i.PauseTime();
                }
            }
        }

        void RenderBackgroundColorLayer()
        {

            //GUILayout.Label("Background Layer", EditorStyles.boldLabel);
            RenderColorGradientField(selectedConfiguration.skyColor, "Sky Color", 0, 24);
            RenderColorGradientField(selectedConfiguration.horizonColor, "Horizon Color", 0, 24);
            RenderColorGradientField(selectedConfiguration.groundColor, "Ground Color", 0, 24);
            RenderHorizonLayer();
        }

        void RenderHorizonLayer()
        {
            EditorGUILayout.Separator();
            RenderTransitioningFloat(selectedConfiguration.horizonHeight, "Horizon Height", "%", "value", true, -1, 1);

            EditorGUILayout.Space(10);
            RenderTransitioningFloat(selectedConfiguration.horizonWidth, "Horizon Width", "%", "value", true, -1, 1);

            EditorGUILayout.Separator();

            // Horizon Mask
            RenderTexture("Texture", ref selectedConfiguration.horizonMask);

            // Horizon mask values
            RenderVector3Field("Horizon Mask Values", ref selectedConfiguration.horizonMaskValues);
        }

        void RenderAmbientLayer()
        {

            //GUILayout.Label("Ambient Layer", EditorStyles.boldLabel);
            selectedConfiguration.ambientTrilight = EditorGUILayout.Toggle("Use Gradient", selectedConfiguration.ambientTrilight);

            if (selectedConfiguration.ambientTrilight)
            {
                RenderColorGradientField(selectedConfiguration.ambientSkyColor, "Ambient Sky Color", 0, 24, true);
                RenderColorGradientField(selectedConfiguration.ambientEquatorColor, "Ambient Equator Color", 0, 24, true);
                RenderColorGradientField(selectedConfiguration.ambientGroundColor, "Ambient Ground Color", 0, 24, true);
            }

        }

        void RenderFogLayer()
        {

            //GUILayout.Label("Fog Layer", EditorStyles.boldLabel);
            selectedConfiguration.useFog = EditorGUILayout.Toggle("Use Fog", selectedConfiguration.useFog);
            if (selectedConfiguration.useFog)
            {
                RenderColorGradientField(selectedConfiguration.fogColor, "Fog Color", 0, 24);
                //selectedConfiguration.fogColor = EditorGUILayout.GradientField("Fog Color", selectedConfiguration.fogColor);
                selectedConfiguration.fogMode = (FogMode)EditorGUILayout.EnumPopup("Fog Mode", selectedConfiguration.fogMode);

                switch (selectedConfiguration.fogMode)
                {
                    case FogMode.Linear:
                        RenderFloatField("Start Distance:", ref selectedConfiguration.fogStartDistance);
                        RenderFloatField("End Distance:", ref selectedConfiguration.fogEndDistance);
                        break;
                    default:
                        RenderFloatField("Density: ", ref selectedConfiguration.fogDensity);
                        break;
                }
            }

        }

        void RenderDirectionalLightLayer()
        {

            //GUILayout.Label("Directional Light Layer", EditorStyles.boldLabel);
            selectedConfiguration.useDirectionalLight = EditorGUILayout.Toggle("Use Directional Light", selectedConfiguration.useDirectionalLight);

            if (!selectedConfiguration.useDirectionalLight)
            {
                return;
            }
            RenderColorGradientField(selectedConfiguration.directionalLightLayer.lightColor, "Light Color", 0, 24);
            RenderColorGradientField(selectedConfiguration.directionalLightLayer.tintColor, "Tint Color", 0, 24, true);

            GUILayout.Space(10);

            // Light Intesity
            RenderTransitioningFloat(selectedConfiguration.directionalLightLayer.intensity, "Light Intensity", "%", "Intensity");

            GUILayout.Space(10);


            RenderTransitioningQuaternionAsVector3(selectedConfiguration.directionalLightLayer.lightDirection, "Light Direction", "%", "Direction", GetDLDirection, 0, 24);
        }

        private Quaternion GetDLDirection() { return directionalLight.transform.rotation; }

        #region Render Slots and Layers

        private void RenderSlots()
        {
            GUIStyle style = new GUIStyle(EditorStyles.foldout);
            style.fixedWidth = 20;
            for (int i = 0; i < selectedConfiguration.slots.Count; i++)
            {
                EditorGUILayout.BeginHorizontal(GUILayout.ExpandWidth(false));
                selectedConfiguration.slots[i].enabled = EditorGUILayout.Toggle(selectedConfiguration.slots[i].enabled, GUILayout.Width(20), GUILayout.Height(10), GUILayout.ExpandWidth(false));
                selectedConfiguration.slots[i].expandedInEditor = EditorGUILayout.Foldout(selectedConfiguration.slots[i].expandedInEditor, "Slot " + i, true, style);
                //selectedConfiguration.slots[i].slotName = EditorGUILayout.TextField(selectedConfiguration.slots[i].slotName, GUILayout.Width(100));
                EditorGUILayout.EndHorizontal();

                // Render slots texture layers
                if (selectedConfiguration.slots[i].expandedInEditor)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.Separator();
                    RenderTextureLayers(selectedConfiguration.slots[i]);
                    EditorGUI.indentLevel--;
                }

                EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
                EditorGUILayout.Separator();
            }
        }

        void RenderTextureLayers(SkyboxSlots slot)
        {
            GUIStyle style = new GUIStyle(EditorStyles.foldout);
            style.fixedWidth = 20;
            for (int i = 0; i < slot.layers.Count; i++)
            {
                // Name and buttons
                EditorGUILayout.BeginHorizontal(GUILayout.ExpandWidth(false));
                slot.layers[i].enabled = EditorGUILayout.Toggle(slot.layers[i].enabled, GUILayout.Width(20), GUILayout.Height(10));
                GUILayout.Space(10);
                slot.layers[i].expandedInEditor = EditorGUILayout.Foldout(slot.layers[i].expandedInEditor, "Layer ", true, style);
                slot.layers[i].nameInEditor = EditorGUILayout.TextField(slot.layers[i].nameInEditor, GUILayout.Width(200), GUILayout.ExpandWidth(false));

                if (GUILayout.Button("Remove", GUILayout.Width(100), GUILayout.ExpandWidth(false)))
                {
                    slot.layers.RemoveAt(i);
                    break;
                }

                if (i == 0 && slot.slotID == 0)
                {
                    GUI.enabled = false;
                }

                if (GUILayout.Button("Move Up", GUILayout.Width(100), GUILayout.ExpandWidth(false)))
                {
                    TextureLayer temp = null;

                    if (i >= 1)
                    {
                        temp = slot.layers[i - 1];
                        slot.layers[i - 1] = slot.layers[i];
                        slot.layers[i] = temp;
                    }
                    else if (slot.slotID >= 1)
                    {
                        selectedConfiguration.slots[slot.slotID - 1].AddNewLayer(slot.layers[i]);
                        slot.layers.RemoveAt(i);
                    }
                    break;
                }

                GUI.enabled = true;

                if (i == slot.layers.Count - 1 && slot.slotID == selectedConfiguration.slots.Count - 1)
                {
                    GUI.enabled = false;
                }

                if (GUILayout.Button("Move down", GUILayout.Width(100), GUILayout.ExpandWidth(false)))
                {
                    TextureLayer temp = null;
                    if (i < (slot.layers.Count - 1))
                    {
                        temp = slot.layers[i + 1];
                        slot.layers[i + 1] = slot.layers[i];
                        slot.layers[i] = temp;
                    }
                    else if (slot.slotID < (selectedConfiguration.slots.Count - 1))
                    {
                        selectedConfiguration.slots[slot.slotID + 1].AddNewLayer(slot.layers[i], true);
                        slot.layers.RemoveAt(i);
                    }

                    break;
                }

                EditorGUILayout.EndHorizontal();

                GUI.enabled = true;

                if (slot.layers[i].expandedInEditor)
                {
                    EditorGUILayout.Separator();
                    EditorGUI.indentLevel++;
                    RenderTextureLayer(slot.layers[i]);

                    EditorGUI.indentLevel--;
                }

                EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);


                GUILayout.Space(32);
            }

            GUI.enabled = true;

            //EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
            if (GUILayout.Button("+", GUILayout.MaxWidth(20)))
            {
                slot.layers.Add(new TextureLayer("Tex Layer " + (slot.layers.Count + 1)));
            }
        }

        void RenderTextureLayer(TextureLayer layer)
        {
            EditorGUILayout.Separator();

            // Layer Type
            EditorGUILayout.BeginHorizontal(GUILayout.ExpandWidth(false));
            EditorGUILayout.LabelField("Layer Type:", GUILayout.Width(150), GUILayout.ExpandWidth(false));
            layer.layerType = (LayerType)EditorGUILayout.EnumPopup(layer.layerType, GUILayout.Width(200));
            EditorGUILayout.EndHorizontal();


            EditorGUILayout.Separator();

            // Time Span
            RenderSepratedFloatFields("Time Span", "Starts", ref layer.timeSpan_start, "Ends", ref layer.timeSpan_End);

            // Fading
            //RenderSepratedFloatFields("Fading", "In", ref layer.fadingInTime, "Out", ref layer.fadingOutTime);

            // Tint
            RenderFloatFieldAsSlider("Tint", ref layer.tintercentage, 0, 100);

            if (layer.layerType == LayerType.Cubemap)
            {
                RenderCubemapLayer(layer);

            }
            else if (layer.layerType == LayerType.Planar)
            {
                RenderPlanarLayer(layer);

            }
            else if (layer.layerType == LayerType.Radial)
            {
                RenderPlanarLayer(layer, true);
            }
            else if (layer.layerType == LayerType.Satellite)
            {
                RenderSatelliteLayer(layer);
            }
            else if (layer.layerType == LayerType.Particles)
            {
                RenderParticleLayer(layer);
            }
        }

        void RenderCubemapLayer(TextureLayer layer)
        {
            // Cubemap
            RenderCubemapTexture("Cubemap", ref layer.cubemap);

            // Gradient
            RenderColorGradientField(layer.color, "color", layer.timeSpan_start, layer.timeSpan_End, true);

            EditorGUILayout.Separator();

            // Movement Type
            EditorGUILayout.BeginHorizontal(GUILayout.ExpandWidth(false));
            EditorGUILayout.LabelField("Movemnt Type", GUILayout.Width(150), GUILayout.ExpandWidth(false));
            layer.movementTypeCubemap = (MovementType)EditorGUILayout.EnumPopup(layer.movementTypeCubemap, GUILayout.Width(200));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Separator();

            // Rotation
            if (layer.movementTypeCubemap == MovementType.PointBased)
            {
                RenderTransitioningVector3(layer.cubemapRotations, "Rotation", "%", "Rot:", layer.timeSpan_start, layer.timeSpan_End);

            }
            else
            {
                RenderVector3Field("Speed", ref layer.speed_Vector3);
            }
        }

        void RenderPlanarLayer(TextureLayer layer, bool isRadial = false)
        {
            // Texture
            RenderTexture("Texture", ref layer.texture);

            // Row and Coloumns
            RenderVector2Field("Rows and Columns", ref layer.flipBookRowsAndColumns);

            // Anim Speed
            RenderFloatField("Anim Speed", ref layer.flipbookAnimSpeed);

            // Normal Texture
            RenderTexture("Normal Map", ref layer.textureNormal);

            // Normal Intensity
            RenderFloatFieldAsSlider("Normal Intensity", ref layer.normalIntensity, 0, 1);

            // Gradient
            RenderColorGradientField(layer.color, "color", layer.timeSpan_start, layer.timeSpan_End, true);

            // Tiling
            RenderVector2Field("Tiling", ref layer.tiling);

            // Movement Type
            EditorGUILayout.BeginHorizontal(GUILayout.ExpandWidth(false));
            EditorGUILayout.LabelField("Movemnt Type", GUILayout.Width(150), GUILayout.ExpandWidth(false));
            layer.movementTypePlanar_Radial = (MovementType)EditorGUILayout.EnumPopup(layer.movementTypePlanar_Radial, GUILayout.Width(200));
            EditorGUILayout.EndHorizontal();

            EditorGUI.indentLevel++;
            EditorGUILayout.Separator();

            if (layer.movementTypePlanar_Radial == MovementType.Speed)
            {
                // Speed
                RenderVector2Field("Speed", ref layer.speed_Vec2);
            }
            else
            {
                // Offset
                RenderTransitioningVector2(layer.offset, "Position", "%", "", layer.timeSpan_start, layer.timeSpan_End);
            }

            EditorGUI.indentLevel--;

            EditorGUILayout.Space(15);

            // Render Distance
            RenderTransitioningFloat(layer.renderDistance, "Render Distance", "", "", true, 0, 20, layer.timeSpan_start, layer.timeSpan_End);

            EditorGUILayout.Space(15);

            // Rotation
            if (!isRadial)
            {
                RenderTransitioningFloat(layer.rotation_float, "Rotation", "", "", true, 0, 360, layer.timeSpan_start, layer.timeSpan_End);
                EditorGUILayout.Separator();
            }

            RenderDistortionVariables(layer);

            EditorGUILayout.Space(10);

            //RenderParticleLayer(layer);
        }

        void RenderSatelliteLayer(TextureLayer layer)
        {
            // Texture
            RenderTexture("Texture", ref layer.texture);

            // Row and Coloumns
            RenderVector2Field("Rows and Columns", ref layer.flipBookRowsAndColumns);

            // Anim Speed
            RenderFloatField("Anim Speed", ref layer.flipbookAnimSpeed);

            // Normal Texture
            RenderTexture("Normal Map", ref layer.textureNormal);

            // Normal Intensity
            RenderFloatFieldAsSlider("Normal Intensity", ref layer.normalIntensity, 0, 1);

            // Gradient
            RenderColorGradientField(layer.color, "color", layer.timeSpan_start, layer.timeSpan_End, true);


            EditorGUILayout.Space(10);


            EditorGUILayout.Space(10);


            EditorGUILayout.BeginHorizontal(GUILayout.ExpandWidth(false));
            EditorGUILayout.LabelField("Movemnt Type", GUILayout.Width(150), GUILayout.ExpandWidth(false));
            layer.movementTypeSatellite = (MovementType)EditorGUILayout.EnumPopup(layer.movementTypeSatellite, GUILayout.Width(200));
            EditorGUILayout.EndHorizontal();

            EditorGUI.indentLevel++;
            EditorGUILayout.Separator();

            if (layer.movementTypeSatellite == MovementType.Speed)
            {
                // Speed
                RenderVector2Field("Speed", ref layer.speed_Vec2);
            }
            else
            {
                // Offset
                RenderTransitioningVector2(layer.offset, "Position", "%", "", layer.timeSpan_start, layer.timeSpan_End);
            }

            EditorGUI.indentLevel--;

            EditorGUILayout.Space(20);

            // Rotation
            RenderTransitioningFloat(layer.rotation_float, "Rotation", "", "", true, 0, 360, layer.timeSpan_start, layer.timeSpan_End);

            EditorGUILayout.Space(12);

            // Size
            RenderTransitioningVector2(layer.satelliteWidthHeight, "Width & Height", "%", "", layer.timeSpan_start, layer.timeSpan_End);
        }

        void RenderParticleLayer(TextureLayer layer)
        {
            // Texture
            RenderTexture("Texture", ref layer.texture);

            // Row and Coloumns
            RenderVector2Field("Rows and Columns", ref layer.flipBookRowsAndColumns);

            // Anim Speed
            RenderFloatField("Anim Speed", ref layer.flipbookAnimSpeed);

            // Normal Map
            RenderTexture("Normal Map", ref layer.textureNormal);

            // Normal Intensity
            RenderFloatFieldAsSlider("Normal Intensity", ref layer.normalIntensity, 0, 1);

            // Gradient
            RenderColorGradientField(layer.color, "color", layer.timeSpan_start, layer.timeSpan_End, true);

            // Tiling
            RenderVector2Field("Tiling", ref layer.particleTiling);

            // Offset
            RenderVector2Field("Offset", ref layer.particlesOffset);


            // Amount
            RenderFloatField("Amount", ref layer.particlesAmount);

            // Size
            RenderSepratedFloatFields("Size", "Min", ref layer.particleMinSize, "Max", ref layer.particleMaxSize);

            // Spread
            RenderSepratedFloatFields("Spread", "Horizontal", ref layer.particlesHorizontalSpread, "Vertical", ref layer.particlesVerticalSpread);

            // Fade
            RenderSepratedFloatFields("Fade", "Min", ref layer.particleMinFade, "Max", ref layer.particleMaxFade);

            // Particle Rotation
            RenderTransitioningVector3(layer.particleRotation, "Rotation", "%", "value", layer.timeSpan_start, layer.timeSpan_End);
        }

        void RenderDistortionVariables(TextureLayer layer)
        {
            layer.distortionExpanded = EditorGUILayout.Foldout(layer.distortionExpanded, "Distortion Values", true, EditorStyles.foldoutHeader);

            if (!layer.distortionExpanded)
            {
                return;
            }

            EditorGUILayout.Space(10);

            EditorGUI.indentLevel++;

            // Distortion Intensity
            RenderTransitioningFloat(layer.distortIntensity, "Intensity", "%", "Value", false, 0, 1, layer.timeSpan_start, layer.timeSpan_End);

            EditorGUILayout.Space(10);

            // Distortion Size
            RenderTransitioningFloat(layer.distortSize, "Size", "%", "Value", false, 0, 1, layer.timeSpan_start, layer.timeSpan_End);

            EditorGUILayout.Space(10);

            // Distortion Speed
            RenderTransitioningVector2(layer.distortSpeed, "Speed", "%", "Value", layer.timeSpan_start, layer.timeSpan_End);

            EditorGUILayout.Space(10);

            // Distortion Sharpness
            RenderTransitioningVector2(layer.distortSharpness, "Sharpness", "%", "Value", layer.timeSpan_start, layer.timeSpan_End);

            EditorGUILayout.Space(10);

            EditorGUI.indentLevel--;
        }

        #endregion

        #region Render simple Variables

        void RenderSepratedFloatFields(string label, string label1, ref float value1, string label2, ref float value2)
        {
            GUILayout.BeginHorizontal(GUILayout.ExpandWidth(false));
            EditorGUILayout.LabelField(label, GUILayout.Width(150), GUILayout.ExpandWidth(false));
            EditorGUILayout.LabelField(label1, GUILayout.Width(90), GUILayout.ExpandWidth(false));
            value1 = EditorGUILayout.FloatField("", value1, GUILayout.Width(90));
            EditorGUILayout.LabelField(label2, GUILayout.Width(90), GUILayout.ExpandWidth(false));
            value2 = EditorGUILayout.FloatField("", value2, GUILayout.Width(90));
            GUILayout.EndHorizontal();

            EditorGUILayout.Separator();
        }

        void RenderFloatField(string label, ref float value)
        {
            EditorGUILayout.BeginHorizontal(GUILayout.ExpandWidth(false));
            EditorGUILayout.LabelField(label, GUILayout.Width(150), GUILayout.ExpandWidth(false));
            value = EditorGUILayout.FloatField(value, GUILayout.Width(90));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Separator();
        }

        void RenderFloatFieldAsSlider(string label, ref float value, float min, float max)
        {
            EditorGUILayout.BeginHorizontal(GUILayout.ExpandWidth(false));
            EditorGUILayout.LabelField(label, GUILayout.Width(150), GUILayout.ExpandWidth(false));
            value = EditorGUILayout.Slider(value, min, max, GUILayout.Width(200));
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Separator();
        }

        void RenderVector3Field(string label, ref Vector3 value)
        {
            GUILayout.BeginHorizontal(GUILayout.ExpandWidth(false));
            EditorGUILayout.LabelField(label, GUILayout.Width(150), GUILayout.ExpandWidth(false));
            value = EditorGUILayout.Vector3Field("", value, GUILayout.Width(200), GUILayout.ExpandWidth(false));
            GUILayout.EndHorizontal();
            EditorGUILayout.Separator();
        }

        void RenderVector2Field(string label, ref Vector2 value)
        {
            GUILayout.BeginHorizontal(GUILayout.ExpandWidth(false));
            EditorGUILayout.LabelField(label, GUILayout.Width(150), GUILayout.ExpandWidth(false));
            value = EditorGUILayout.Vector2Field("", value, GUILayout.Width(200), GUILayout.ExpandWidth(false));
            GUILayout.EndHorizontal();
            EditorGUILayout.Separator();
        }

        void RenderTexture(string label, ref Texture2D tex)
        {
            GUILayout.BeginHorizontal(GUILayout.ExpandWidth(false));
            EditorGUILayout.LabelField(label, GUILayout.Width(150), GUILayout.ExpandWidth(false));
            tex = (Texture2D)EditorGUILayout.ObjectField(tex, typeof(Texture2D), false, GUILayout.Width(200));
            GUILayout.EndHorizontal();

            EditorGUILayout.Separator();
        }

        void RenderCubemapTexture(string label, ref Cubemap tex)
        {
            GUILayout.BeginHorizontal(GUILayout.ExpandWidth(false));
            EditorGUILayout.LabelField(label, GUILayout.Width(150), GUILayout.ExpandWidth(false));
            tex = (Cubemap)EditorGUILayout.ObjectField(tex, typeof(Cubemap), false, GUILayout.Width(200));
            GUILayout.EndHorizontal();

            EditorGUILayout.Separator();
        }

        #endregion

        #region Render Transitioning Variables

        void RenderTransitioningVector3(List<TransitioningVector3> _list, string label, string percentTxt, string valueText, float layerStartTime = 0, float layerEndTime = 24)
        {
            GUILayout.BeginHorizontal(GUILayout.ExpandWidth(false));
            EditorGUILayout.LabelField(label, GUILayout.Width(120), GUILayout.ExpandWidth(false));
            EditorGUILayout.BeginVertical();

            if (_list.Count == 0)
            {
                if (GUILayout.Button("+", GUILayout.Width(20), GUILayout.ExpandWidth(false)))
                {
                    Vector3 tLastPos = Vector3.zero;
                    if (_list.Count != 0)
                    {
                        tLastPos = _list[_list.Count - 1].value;
                    }
                    _list.Add(new TransitioningVector3(GetNormalizedLayerCurrentTime(layerStartTime, layerEndTime) * 100, tLastPos));
                }
            }

            for (int i = 0; i < _list.Count; i++)
            {
                GUILayout.BeginHorizontal(GUILayout.ExpandWidth(false));

                if (GUILayout.Button(">", GUILayout.ExpandWidth(false)))
                {
                    timeOfTheDay = GetDayTimeForLayerNormalizedTime(layerStartTime, layerEndTime, _list[i].percentage / 100);
                }

                // Percentage

                GUILayout.Label(percentTxt, GUILayout.ExpandWidth(false));

                RenderPercentagePart(layerStartTime, layerEndTime, ref _list[i].percentage);

                GUILayout.Space(10);

                GUILayout.Label(valueText, GUILayout.ExpandWidth(false));

                GUILayout.Space(10);
                _list[i].value = EditorGUILayout.Vector3Field("", _list[i].value, GUILayout.Width(200), GUILayout.ExpandWidth(false));

                GUILayout.Space(20);
                if (GUILayout.Button("Remove", GUILayout.Width(100), GUILayout.ExpandWidth(false)))
                {
                    _list.RemoveAt(i);
                    //break;
                }

                if (i == (_list.Count - 1))
                {
                    GUILayout.Space(20);
                    if (GUILayout.Button("+", GUILayout.Width(20), GUILayout.ExpandWidth(false)))
                    {
                        Vector3 tLastPos = Vector3.zero;
                        if (_list.Count != 0)
                        {
                            tLastPos = _list[_list.Count - 1].value;
                        }
                        _list.Add(new TransitioningVector3(GetNormalizedLayerCurrentTime(layerStartTime, layerEndTime) * 100, tLastPos));
                        //break;
                    }
                }

                GUILayout.EndHorizontal();
            }

            EditorGUILayout.EndVertical();
            GUILayout.EndHorizontal();

            Rect lastRect = GUILayoutUtility.GetLastRect();
            lastRect.y -= 5;
            lastRect.height += 10;
            GUI.Box(lastRect, "", EditorStyles.helpBox);
        }

        void RenderTransitioningVector2(List<TransitioningVector2> _list, string label, string percentTxt, string valueText, float layerStartTime = 0, float layerEndTime = 24)
        {
            GUILayout.BeginHorizontal(GUILayout.ExpandWidth(false));
            EditorGUILayout.LabelField(label, GUILayout.Width(120), GUILayout.ExpandWidth(false));
            EditorGUILayout.BeginVertical();

            if (_list.Count == 0)
            {
                if (GUILayout.Button("+", GUILayout.Width(20), GUILayout.ExpandWidth(false)))
                {
                    Vector2 tLastPos = Vector2.zero;
                    if (_list.Count != 0)
                    {
                        tLastPos = _list[_list.Count - 1].value;
                    }
                    _list.Add(new TransitioningVector2(GetNormalizedLayerCurrentTime(layerStartTime, layerEndTime) * 100, tLastPos));
                }
            }

            for (int i = 0; i < _list.Count; i++)
            {
                GUILayout.BeginHorizontal(GUILayout.ExpandWidth(false));

                if (GUILayout.Button(">", GUILayout.ExpandWidth(false)))
                {
                    timeOfTheDay = GetDayTimeForLayerNormalizedTime(layerStartTime, layerEndTime, _list[i].percentage / 100);
                }

                // Percentage

                GUILayout.Label(percentTxt, GUILayout.ExpandWidth(false));

                RenderPercentagePart(layerStartTime, layerEndTime, ref _list[i].percentage);

                GUILayout.Label(valueText, GUILayout.ExpandWidth(false));

                GUILayout.Space(10);
                _list[i].value = EditorGUILayout.Vector2Field("", _list[i].value, GUILayout.Width(200), GUILayout.ExpandWidth(false));

                GUILayout.Space(20);
                if (GUILayout.Button("Remove", GUILayout.Width(100), GUILayout.ExpandWidth(false)))
                {
                    _list.RemoveAt(i);
                }

                if (i == (_list.Count - 1))
                {
                    GUILayout.Space(20);
                    if (GUILayout.Button("+", GUILayout.Width(20), GUILayout.ExpandWidth(false)))
                    {
                        Vector2 tLastPos = Vector2.zero;
                        if (_list.Count != 0)
                        {
                            tLastPos = _list[_list.Count - 1].value;
                        }
                        _list.Add(new TransitioningVector2(GetNormalizedLayerCurrentTime(layerStartTime, layerEndTime) * 100, tLastPos));
                    }
                }

                GUILayout.EndHorizontal();
            }

            EditorGUILayout.EndVertical();
            GUILayout.EndHorizontal();

            Rect lastRect = GUILayoutUtility.GetLastRect();
            lastRect.y -= 5;
            lastRect.height += 10;
            GUI.Box(lastRect, "", EditorStyles.helpBox);
        }

        void RenderTransitioningFloat(List<TransitioningFloat> _list, string label, string percentTxt, string valueText, bool slider = false, float min = 0, float max = 1, float layerStartTime = 0, float layerEndTime = 24)
        {
            GUILayout.BeginHorizontal(GUILayout.ExpandWidth(false));
            EditorGUILayout.LabelField(label, GUILayout.Width(120), GUILayout.ExpandWidth(false));
            EditorGUILayout.BeginVertical();

            if (_list.Count == 0)
            {
                GUILayout.BeginHorizontal(GUILayout.ExpandWidth(false));
                if (GUILayout.Button("+", GUILayout.Width(20), GUILayout.ExpandWidth(false)))
                {
                    float tLast = 0;
                    if (_list.Count != 0)
                    {
                        tLast = _list[_list.Count - 1].value;
                    }
                    _list.Add(new TransitioningFloat(GetNormalizedLayerCurrentTime(layerStartTime, layerEndTime) * 100, tLast));
                }
                GUILayout.EndHorizontal();
            }

            for (int i = 0; i < _list.Count; i++)
            {
                GUILayout.BeginHorizontal(GUILayout.ExpandWidth(false));

                GUILayout.Space(10);
                if (GUILayout.Button(">", GUILayout.ExpandWidth(false)))
                {
                    timeOfTheDay = GetDayTimeForLayerNormalizedTime(layerStartTime, layerEndTime, _list[i].percentage / 100);
                }
                GUILayout.Label(percentTxt, GUILayout.ExpandWidth(false));

                RenderPercentagePart(layerStartTime, layerEndTime, ref _list[i].percentage);

                GUILayout.Label(valueText, GUILayout.ExpandWidth(false));

                if (slider)
                {
                    _list[i].value = EditorGUILayout.Slider(_list[i].value, min, max, GUILayout.Width(200), GUILayout.ExpandWidth(false));
                }
                else
                {
                    _list[i].value = EditorGUILayout.FloatField("", _list[i].value, GUILayout.Width(200), GUILayout.ExpandWidth(false));
                }


                GUILayout.Space(20);
                if (GUILayout.Button("Remove", GUILayout.Width(100), GUILayout.ExpandWidth(false)))
                {
                    _list.RemoveAt(i);
                }

                if (i == (_list.Count - 1))
                {
                    GUILayout.Space(20);
                    if (GUILayout.Button("+", GUILayout.Width(20), GUILayout.ExpandWidth(false)))
                    {
                        float tLast = 0;
                        if (_list.Count != 0)
                        {
                            tLast = _list[_list.Count - 1].value;
                        }
                        _list.Add(new TransitioningFloat(GetNormalizedLayerCurrentTime(layerStartTime, layerEndTime) * 100, tLast));
                    }
                }

                GUILayout.EndHorizontal();
            }

            EditorGUILayout.EndVertical();
            GUILayout.EndHorizontal();

            Rect lastRect = GUILayoutUtility.GetLastRect();
            lastRect.y -= 5;
            lastRect.height += 10;
            GUI.Box(lastRect, "", EditorStyles.helpBox);
        }

        void RenderColorGradientField(Gradient color, string label = "color", float startTime = -1, float endTime = -1, bool hdr = false)
        {
            GUILayout.BeginHorizontal(GUILayout.ExpandWidth(false));
            EditorGUILayout.LabelField(label, GUILayout.Width(150), GUILayout.ExpandWidth(false));

            if (startTime != -1)
            {
                EditorGUILayout.LabelField(startTime + "Hr", GUILayout.Width(65), GUILayout.ExpandWidth(false));
            }

            color = EditorGUILayout.GradientField(new GUIContent(""), color, hdr, GUILayout.Width(250), GUILayout.ExpandWidth(false));

            if (endTime != 1)
            {
                EditorGUILayout.LabelField(endTime + "Hr", GUILayout.Width(65), GUILayout.ExpandWidth(false));
            }
            GUILayout.EndHorizontal();
            EditorGUILayout.Separator();
        }

        void RenderTransitioningQuaternionAsVector3(List<TransitioningQuaternion> _list, string label, string percentTxt, string valueText, Func<Quaternion> GetCurrentRotation, float layerStartTime = 0, float layerEndTime = 24)
        {
            GUILayout.BeginHorizontal(GUILayout.ExpandWidth(false));
            GUILayout.Label(label, GUILayout.Width(120), GUILayout.ExpandWidth(false));

            GUILayout.BeginVertical();

            if (_list.Count == 0)
            {
                GUILayout.BeginHorizontal(GUILayout.ExpandWidth(false));
                if (GUILayout.Button("+", GUILayout.Width(20), GUILayout.ExpandWidth(false)))
                {
                    _list.Add(new TransitioningQuaternion(GetNormalizedLayerCurrentTime(layerStartTime, layerEndTime) * 100, GetCurrentRotation()));
                }
                GUILayout.EndHorizontal();
            }

            for (int i = 0; i < _list.Count; i++)
            {
                GUILayout.BeginHorizontal();
                if (GUILayout.Button(">", GUILayout.ExpandWidth(false)))
                {
                    timeOfTheDay = GetDayTimeForLayerNormalizedTime(layerStartTime, layerEndTime, _list[i].percentage / 100);
                }

                GUILayout.Label(percentTxt, GUILayout.ExpandWidth(false));

                RenderPercentagePart(layerStartTime, layerEndTime, ref _list[i].percentage);

                GUILayout.Space(10);

                GUILayout.Label(valueText, GUILayout.ExpandWidth(false));

                // Convert Quaternion to Vector3
                _list[i].value = Quaternion.Euler(EditorGUILayout.Vector3Field("", _list[i].value.eulerAngles, GUILayout.ExpandWidth(false)));

                if (GUILayout.Button("Capture", GUILayout.Width(100), GUILayout.ExpandWidth(false)))
                {
                    selectedConfiguration.directionalLightLayer.lightDirection[i].percentage = GetNormalizedLayerCurrentTime(layerStartTime, layerEndTime) * 100;
                    selectedConfiguration.directionalLightLayer.lightDirection[i].value = GetCurrentRotation();
                    break;
                }

                if (GUILayout.Button("Remove", GUILayout.Width(100), GUILayout.ExpandWidth(false)))
                {
                    _list.RemoveAt(i);
                    break;
                }

                if (i == (_list.Count - 1))
                {
                    GUILayout.Space(20);
                    if (GUILayout.Button("+", GUILayout.Width(20), GUILayout.ExpandWidth(false)))
                    {
                        _list.Add(new TransitioningQuaternion(GetNormalizedLayerCurrentTime(layerStartTime, layerEndTime) * 100, GetCurrentRotation()));
                    }
                }

                GUILayout.EndHorizontal();
            }
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();

            Rect lastRect = GUILayoutUtility.GetLastRect();
            lastRect.y -= 5;
            lastRect.height += 10;
            GUI.Box(lastRect, "", EditorStyles.helpBox);
        }

        void RenderPercentagePart(float layerStartTime, float layerEndTime, ref float percentage)
        {
            GUILayout.Label(layerStartTime + "Hr", GUILayout.Width(35), GUILayout.ExpandWidth(false));

            GUIStyle style = new GUIStyle();
            style.alignment = TextAnchor.MiddleCenter;

            GUILayout.BeginVertical(style, GUILayout.ExpandWidth(false), GUILayout.Width(150));
            float time = GetDayTimeForLayerNormalizedTime(layerStartTime, layerEndTime, percentage / 100);
            GUILayout.Label(time.ToString("f2") + " Hr", GUILayout.ExpandWidth(false));
            percentage = EditorGUILayout.Slider(percentage, 0, 100, GUILayout.Width(150), GUILayout.ExpandWidth(false));
            GUILayout.EndVertical();

            GUILayout.Label(layerEndTime + "Hr", GUILayout.Width(35), GUILayout.ExpandWidth(false));
        }

        #endregion

        Vector4 QuaternionToVector4(Quaternion rot) { return new Vector4(rot.x, rot.y, rot.z, rot.w); }

        private void ApplyOnMaterial()
        {
            EnsureDependencies();

            selectedConfiguration.ApplyOnMaterial(selectedMat, timeOfTheDay, GetNormalizedDayTime(), directionalLight);
        }

        private float GetNormalizedDayTime()
        {
            float tTime = 0;

            tTime = timeOfTheDay / 24;

            tTime = Mathf.Clamp(tTime, 0, 1);

            return tTime;
        }

        private float GetNormalizedLayerCurrentTime(float startTime, float endTime)
        {
            float editedEndTime = endTime;
            float editedDayTime = timeOfTheDay;
            if (endTime < startTime)
            {
                editedEndTime = 24 + endTime;
                if (timeOfTheDay < startTime)
                {
                    editedDayTime = 24 + timeOfTheDay;
                }
            }
            return Mathf.InverseLerp(startTime, editedEndTime, editedDayTime);
        }

        private float GetDayTimeForLayerNormalizedTime(float startTime, float endTime, float normalizeTime)
        {
            float editedEndTime = endTime;
            if (endTime < startTime)
            {
                editedEndTime = 24 + endTime;
            }
            float time = Mathf.Lerp(startTime, editedEndTime, normalizeTime);

            if (time > 24)
            {
                time -= 24;
            }

            return time;
        }

        private Quaternion Vector4ToQuaternion(Vector4 val) { return new Quaternion(val.x, val.y, val.z, val.w); }
    }

}