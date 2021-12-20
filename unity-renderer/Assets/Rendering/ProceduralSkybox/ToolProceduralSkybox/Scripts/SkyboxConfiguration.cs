using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.Skybox
{
    //[CreateAssetMenu(fileName = "Skybox Configuration", menuName = "ScriptableObjects/SkyboxConfiguration", order = 1)]
    public class SkyboxConfiguration : ScriptableObject
    {
        public delegate void TimelineEvents(string tag, bool enable, bool trigger);
        public event TimelineEvents OnTimelineEvent;

        // ID
        public string skyboxID;

        // Background Color
        public Gradient skyColor = new Gradient();
        public Gradient horizonColor = new Gradient();
        public Gradient groundColor = new Gradient();

        // Horizon Layer
        public List<TransitioningFloat> horizonWidth = new List<TransitioningFloat>();
        public List<TransitioningFloat> horizonHeight = new List<TransitioningFloat>();
        public Texture2D horizonMask;
        public Vector3 horizonMaskValues = new Vector3(0, 0, 0);
        public Gradient horizonPlaneColor = new Gradient();
        public List<TransitioningFloat> horizonPlaneHeight = new List<TransitioningFloat>();

        // Ambient Color
        public bool ambientTrilight = true;
        public Gradient ambientSkyColor = new Gradient();
        public Gradient ambientEquatorColor = new Gradient();
        public Gradient ambientGroundColor = new Gradient();

        // Avatar Color
        public bool useAvatarGradient = true;
        public Gradient avatarTintGradient = new Gradient();
        public Color avatarTintColor = Color.white;
        public bool useAvatarRealtimeDLDirection = true;
        public Vector3 avatarLightConstantDir = new Vector3(-18f, 144f, -72f);
        public bool useAvatarRealtimeLightColor = true;
        public Gradient avatarLightColorGradient = new Gradient();

        // Fog Properties
        public bool useFog = false;
        public FogMode fogMode = FogMode.ExponentialSquared;
        public Gradient fogColor = new Gradient();
        public float fogDensity = 0.05f;
        public float fogStartDistance = 0;
        public float fogEndDistance = 300;

        // DirectionalLight Properties
        public bool useDirectionalLight = true;
        public DirectionalLightAttributes directionalLightLayer = new DirectionalLightAttributes();

        // Slots
        public List<SkyboxSlots> slots = new List<SkyboxSlots>();

        // Layers
        public List<TextureLayer> layers = new List<TextureLayer>();

        // Timeline Tags
        public List<TimelineTagsDuration> timelineTags = new List<TimelineTagsDuration>();

        // Environment Reflection
        public float timeToCreateCubemap;
        public bool useCustomReflection;

        private float cycleTime = 24;

        [ContextMenu("Fill layers array")]
        public void FillTextureLayersWithSlotValues()
        {
            for (int i = 0; i < slots.Count; i++)
            {
                for (int j = 0; j < slots[i].layers.Count; j++)
                {
                    TextureLayer temp = slots[i].layers[j];
                    layers.Add(temp);
                }
            }
        }

        public void AddSlots(int slotID) { slots.Add(new SkyboxSlots(slotID)); }

        public void ApplyOnMaterial(Material selectedMat, float dayTime, float normalizedDayTime, int slotCount = 5, Light directionalLightGO = null, float cycleTime = 24)
        {
            float percentage = normalizedDayTime * 100;

            // General Values
            selectedMat.SetColor("_lightTint", directionalLightLayer.tintColor.Evaluate(normalizedDayTime));
            selectedMat.SetVector("_lightDirection", directionalLightGO.transform.rotation.eulerAngles);

            // Apply Base Values
            selectedMat.SetColor("_skyColor", skyColor.Evaluate(normalizedDayTime));
            selectedMat.SetColor("_groundColor", groundColor.Evaluate(normalizedDayTime));

            // Apply Horizon Values
            selectedMat.SetColor("_horizonColor", horizonColor.Evaluate(normalizedDayTime));
            selectedMat.SetFloat("_horizonHeight", GetTransitionValue(horizonHeight, percentage, 0f));
            selectedMat.SetFloat("_horizonWidth", GetTransitionValue(horizonWidth, percentage, 0f));
            selectedMat.SetTexture("_horizonMask", horizonMask);
            selectedMat.SetVector("_horizonMaskValues", horizonMaskValues);
            selectedMat.SetColor("_HorizonPlaneColor", horizonPlaneColor.Evaluate(normalizedDayTime));
            selectedMat.SetFloat("_horizonPlaneHeight", GetTransitionValue(horizonPlaneHeight, percentage, -1f));


            // Apply Ambient colors to the rendering settings
            if (ambientTrilight)
            {
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
                RenderSettings.ambientSkyColor = ambientSkyColor.Evaluate(normalizedDayTime);
                RenderSettings.ambientEquatorColor = ambientEquatorColor.Evaluate(normalizedDayTime);
                RenderSettings.ambientGroundColor = ambientGroundColor.Evaluate(normalizedDayTime);
            }
            else
            {
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Skybox;
            }

            // Apply Avatar Color 
            if (useAvatarGradient)
            {
                Shader.SetGlobalColor("_TintColor", avatarTintGradient.Evaluate(normalizedDayTime));
            }
            else
            {
                Shader.SetGlobalColor("_TintColor", avatarTintColor);
            }

            // Apply Avatar Light Direction
            if (!useAvatarRealtimeDLDirection || directionalLightGO == null || !useDirectionalLight)
            {
                Shader.SetGlobalVector("_LightDir", avatarLightConstantDir / 180);
            }
            else
            {
                Vector3 tempDir = directionalLightGO.transform.rotation.eulerAngles;
                Shader.SetGlobalVector("_LightDir", tempDir / 180);
            }

            // Apply Avatar Light Color
            if (!useAvatarRealtimeLightColor || directionalLightGO == null || !useDirectionalLight)
            {
                Shader.SetGlobalColor("_LightColor", avatarLightColorGradient.Evaluate(normalizedDayTime));
            }
            else
            {
                Shader.SetGlobalColor("_LightColor", directionalLightLayer.lightColor.Evaluate(normalizedDayTime));
            }


            //Fog Values
            RenderSettings.fog = useFog;
            if (useFog)
            {
                RenderSettings.fogColor = fogColor.Evaluate(normalizedDayTime);
                RenderSettings.fogMode = fogMode;
                switch (fogMode)
                {
                    case FogMode.Linear:
                        RenderSettings.fogStartDistance = fogStartDistance;
                        RenderSettings.fogEndDistance = fogEndDistance;
                        break;
                    default:
                        RenderSettings.fogDensity = fogDensity;
                        break;
                }
            }

            //Directional List
            if (useDirectionalLight)
            {
                // Apply Direction light color
                directionalLightGO.color = directionalLightLayer.lightColor.Evaluate(normalizedDayTime);

                // Apply direction
                ApplyDLDirection(normalizedDayTime, directionalLightGO);

                // Apply Intensity
                ApplyDLIntensity(normalizedDayTime, directionalLightGO);
            }
            else
            {
                directionalLightGO.gameObject.SetActive(false);
            }

            // Check and Fire timeline events
            CheckAndFireTimelineEvents(dayTime);

            ApplyAllSlots(selectedMat, dayTime, normalizedDayTime, slotCount, cycleTime);
        }

        void ApplyAllSlots(Material selectedMat, float dayTime, float normalizedDayTime, int slotCount = 5, float cycleTime = 24)
        {
            for (int i = 0; i < slotCount; i++)
            {
                TextureLayer layer = GetActiveLayer(dayTime, i);

                if (layer == null || !layer.enabled)
                {
                    ResetSlot(selectedMat, i);
                    continue;
                }

                ApplyTextureLayer(selectedMat, dayTime, normalizedDayTime, i, layer, cycleTime);
            }
        }

        public TextureLayer GetActiveLayer(float currentTime, int slotID)
        {
            TextureLayer temp = null;

            for (int i = 0; i < layers.Count; i++)
            {
                if (layers[i].slotID != slotID)
                {
                    continue;
                }

                float endTimeEdited = layers[i].timeSpan_End;
                float dayTimeEdited = currentTime;

                if (layers[i].timeSpan_End < layers[i].timeSpan_start)
                {
                    endTimeEdited = cycleTime + layers[i].timeSpan_End;
                    if (currentTime < layers[i].timeSpan_start)
                    {
                        dayTimeEdited = cycleTime + currentTime;
                    }
                }

                if (dayTimeEdited >= layers[i].timeSpan_start && dayTimeEdited <= endTimeEdited)
                {
                    if (layers[i].enabled)
                    {
                        if (temp == null)
                        {
                            layers[i].renderType = LayerRenderType.Rendering;
                            temp = layers[i];
                        }
                        else
                        {
                            temp.renderType = LayerRenderType.Conflict_Playing;
                            layers[i].renderType = LayerRenderType.Conflict_NotPlaying;
                        }
                    }
                    else
                    {
                        layers[i].renderType = LayerRenderType.NotRendering;
                    }
                }
                else
                {
                    layers[i].renderType = LayerRenderType.NotRendering;
                }


            }
            return temp;
        }

        #region Timeline Events

        /// <summary>
        /// Check if any event has to be fired
        /// TODO:: Can be optimised.
        /// </summary>
        /// <param name="dayTime">Current day time</param>
        private void CheckAndFireTimelineEvents(float dayTime)
        {
            float endTimeEdited = 0;
            float dayTimeEdited = dayTime;
            for (int i = 0; i < timelineTags.Count; i++)
            {
                // If current event is a trigger event, fire trigger event. It's boolean will be disable when cycle resets
                if (timelineTags[i].isTrigger)
                {
                    if (dayTime > timelineTags[i].startTime && !timelineTags[i].startEventExecuted)
                    {
                        OnTimelineEvent?.Invoke(timelineTags[i].tag, true, true);
                        timelineTags[i].startEventExecuted = true;
                    }
                    continue;
                }


                endTimeEdited = timelineTags[i].endTime;
                dayTimeEdited = dayTime;

                // Change time if this is over a day scenario
                if (timelineTags[i].endTime < timelineTags[i].startTime)
                {
                    endTimeEdited = cycleTime + timelineTags[i].endTime;

                    if (dayTime < timelineTags[i].startTime)
                    {
                        dayTimeEdited = cycleTime + dayTime;
                    }
                }

                // If current time is between start and end time and start event is not executed, Fire start event
                if (dayTimeEdited >= timelineTags[i].startTime && dayTimeEdited < endTimeEdited && !timelineTags[i].startEventExecuted)
                {
                    OnTimelineEvent?.Invoke(timelineTags[i].tag, true, false);
                    timelineTags[i].startEventExecuted = true;
                }

                // If current time is greater than end time and start event is executed, fire end event
                if (dayTimeEdited >= endTimeEdited && timelineTags[i].startEventExecuted)
                {
                    OnTimelineEvent?.Invoke(timelineTags[i].tag, false, false);
                    timelineTags[i].startEventExecuted = false;
                }

            }
        }

        public void CycleResets()
        {
            // Resets all timeline triggers and normal flow events
            for (int i = 0; i < timelineTags.Count; i++)
            {
                if (timelineTags[i].isTrigger)
                {
                    timelineTags[i].startEventExecuted = false;
                    continue;
                }

                if (timelineTags[i].endTime > timelineTags[i].startTime)
                {
                    timelineTags[i].startEventExecuted = false;
                }
            }
        }

        #endregion

        #region Directional Light

        void ApplyDLIntensity(float normalizedDayTime, Light lightGO)
        {
            if (lightGO == null)
            {
                Debug.LogError("Directional Light option is on, with light reference");
                return;
            }

            if (directionalLightLayer.intensity.Count == 0)
            {
                Debug.Log("Directional Light option is on with no intensity data");
                return;
            }

            if (directionalLightLayer.intensity.Count == 1)
            {
                lightGO.intensity = directionalLightLayer.intensity[0].value;
                return;
            }


            lightGO.intensity = GetTransitionValue(directionalLightLayer.intensity, normalizedDayTime * 100);
        }

        void ApplyDLDirection(float normalizedDayTime, Light lightGO)
        {
            if (lightGO == null)
            {
                Debug.LogError("Directional Light option is on, with light reference");
                return;
            }

            if (directionalLightLayer.lightDirection.Count == 0)
            {
                Debug.Log("Directional Light option is on with no rotation data");
                return;
            }

            if (directionalLightLayer.lightDirection.Count == 1)
            {
                lightGO.transform.rotation = directionalLightLayer.lightDirection[0].value;
                return;
            }

            float percentage = normalizedDayTime * 100;
            TransitioningQuaternion min = directionalLightLayer.lightDirection[0], max = directionalLightLayer.lightDirection[0];

            // Apply Direction
            for (int i = 0; i < directionalLightLayer.lightDirection.Count; i++)
            {
                if (percentage <= directionalLightLayer.lightDirection[i].percentage)
                {
                    max = directionalLightLayer.lightDirection[i];

                    if ((i - 1) > 0)
                    {
                        min = directionalLightLayer.lightDirection[i - 1];
                    }

                    break;
                }
            }

            float t = Mathf.InverseLerp(min.percentage, max.percentage, percentage);
            lightGO.transform.rotation = Quaternion.Lerp(min.value, max.value, t);
        }

        #endregion

        #region Apply texture layers

        void ApplyTextureLayer(Material selectedMat, float dayTime, float normalizedDayTime, int slotNum, TextureLayer layer, float cycleTime = 24, bool changeAlllValues = true)
        {
            float endTimeEdited = layer.timeSpan_End;
            float dayTimeEdited = dayTime;
            if (layer.timeSpan_End < layer.timeSpan_start)
            {
                endTimeEdited = cycleTime + layer.timeSpan_End;
                if (dayTime < layer.timeSpan_start)
                {
                    dayTimeEdited = cycleTime + dayTime;
                }
            }
            float normalizedLayerTime = Mathf.InverseLerp(layer.timeSpan_start, endTimeEdited, dayTimeEdited);

            selectedMat.SetFloat("_layerType_" + slotNum, (int)layer.layerType);

            bool fadeInChange = CheckFadingIn(selectedMat, dayTime, normalizedDayTime, slotNum, layer, cycleTime);

            bool fadeOutChange = CheckFadingOut(selectedMat, dayTime, normalizedDayTime, slotNum, layer, cycleTime);

            if (!fadeInChange && !fadeOutChange)
            {
                selectedMat.SetFloat("_fadeTime_" + slotNum, 1);
            }

            switch (layer.layerType)
            {
                case LayerType.Planar:
                case LayerType.Radial:
                    ApplyPlanarTextureLayer(selectedMat, dayTime, normalizedLayerTime, slotNum, layer, true);
                    break;
                case LayerType.Satellite:
                    ApplySatelliteTextureLayer(selectedMat, dayTime, normalizedLayerTime, slotNum, layer, true);
                    break;
                case LayerType.Cubemap:
                    ApplyCubemapTextureLayer(selectedMat, dayTime, normalizedLayerTime, slotNum, layer, true);
                    break;
                case LayerType.Particles:
                    ApplyParticleTextureLayer(selectedMat, dayTime, normalizedLayerTime, slotNum, layer, true);
                    break;
                default:
                    break;
            }
        }

        private bool CheckFadingIn(Material selectedMat, float dayTime, float normalizedDayTime, int slotNum, TextureLayer layer, float cycleTime = 24, bool changeAlllValues = true)
        {
            bool fadeChanged = false;
            float fadeInCompletionTime = layer.timeSpan_start + layer.fadingInTime;
            float dayTimeEdited = dayTime;
            if (dayTime < layer.timeSpan_start)
            {
                dayTimeEdited = 24 + dayTime;
            }

            if (dayTimeEdited < fadeInCompletionTime)
            {
                float percentage = Mathf.InverseLerp(layer.timeSpan_start, fadeInCompletionTime, dayTimeEdited);
                fadeChanged = true;
                selectedMat.SetFloat("_fadeTime_" + slotNum, percentage);
            }

            return fadeChanged;
        }

        private bool CheckFadingOut(Material selectedMat, float dayTime, float normalizedDayTime, int slotNum, TextureLayer layer, float cycleTime = 24, bool changeAlllValues = true)
        {
            bool fadeChanged = false;
            float endTimeEdited = layer.timeSpan_End;
            float dayTimeEdited = dayTime;

            if (layer.timeSpan_End < layer.timeSpan_start)
            {
                endTimeEdited = cycleTime + layer.timeSpan_End;
            }

            if (dayTime < layer.timeSpan_start)
            {
                dayTimeEdited = cycleTime + dayTime;
            }


            float fadeOutStartTime = endTimeEdited - layer.fadingOutTime;

            if (dayTimeEdited > fadeOutStartTime)
            {
                float percentage = Mathf.InverseLerp(endTimeEdited, fadeOutStartTime, dayTimeEdited);
                fadeChanged = true;
                selectedMat.SetFloat("_fadeTime_" + slotNum, percentage);
            }

            return fadeChanged;
        }

        void ApplyCubemapTextureLayer(Material selectedMat, float dayTime, float normalizedLayerTime, int layerNum, TextureLayer layer, bool changeAlllValues = true)
        {
            if (changeAlllValues)
            {
                selectedMat.SetFloat("_RenderDistance_" + layerNum, 0);
                selectedMat.SetTexture("_tex_" + layerNum, null);
                selectedMat.SetTexture("_cubemap_" + layerNum, layer.cubemap);
                selectedMat.SetTexture("_normals_" + layerNum, null);
                selectedMat.SetVector("_timeFrame_" + layerNum, new Vector4(layer.timeSpan_start, layer.timeSpan_End));
                //selectedMat.SetFloat("_fadeTime_" + layerNum, 1);
                selectedMat.SetFloat("_lightIntensity_" + layerNum, layer.tintercentage / 100);
                selectedMat.SetFloat("_normalIntensity_" + layerNum, 0);
                selectedMat.SetVector("_distortIntAndSize_" + layerNum, Vector2.zero);
                selectedMat.SetVector("_distortSpeedAndSharp_" + layerNum, Vector4.zero);
                // Particles
                selectedMat.SetVector("_rowAndCollumns_" + layerNum, Vector2.zero);
                selectedMat.SetVector("_particlesMainParameters_" + layerNum, Vector4.zero);
                selectedMat.SetVector("_particlesSecondaryParameters_" + layerNum, Vector4.zero);
            }


            selectedMat.SetColor("_color_" + layerNum, layer.color.Evaluate(normalizedLayerTime));

            // Set cubemap rotation. (Shader variable reused)   
            if (layer.movementTypeCubemap == MovementType.PointBased)
            {
                Vector3 currentRotation = GetTransitionValue(layer.cubemapRotations, normalizedLayerTime * 100);
                selectedMat.SetVector("_tilingAndOffset_" + layerNum, new Vector4(currentRotation.x, currentRotation.y, currentRotation.z, 0));
                selectedMat.SetVector("_speedAndRotation_" + layerNum, new Vector4(0, 0, 0, 0));
            }
            else
            {
                selectedMat.SetVector("_tilingAndOffset_" + layerNum, new Vector4(0, 0, 0, 0));
                selectedMat.SetVector("_speedAndRotation_" + layerNum, new Vector4(layer.speed_Vector3.x, layer.speed_Vector3.y, layer.speed_Vector3.z, 0));
            }

        }

        void ApplyPlanarTextureLayer(Material selectedMat, float dayTime, float normalizedLayerTime, int layerNum, TextureLayer layer, bool changeAlllValues = true)
        {
            selectedMat.SetFloat("_RenderDistance_" + layerNum, GetTransitionValue(layer.renderDistance, normalizedLayerTime * 100, 3.4f));
            selectedMat.SetTexture("_tex_" + layerNum, layer.texture);
            selectedMat.SetTexture("_normals_" + layerNum, layer.textureNormal);
            selectedMat.SetTexture("_cubemap_" + layerNum, null);

            selectedMat.SetColor("_color_" + layerNum, layer.color.Evaluate(normalizedLayerTime));


            if (layer.movementTypePlanar_Radial == MovementType.Speed)
            {
                // speed and Rotation
                float rot = 0;
                if (layer.layerType == LayerType.Planar)
                {
                    rot = GetTransitionValue(layer.rotation_float, normalizedLayerTime * 100);
                }

                selectedMat.SetVector("_speedAndRotation_" + layerNum, new Vector4(layer.speed_Vec2.x, layer.speed_Vec2.y, rot));

                // Tiling and Offset
                Vector4 t = new Vector4(layer.tiling.x, layer.tiling.y, 0, 0);
                selectedMat.SetVector("_tilingAndOffset_" + layerNum, t);
            }
            else
            {
                // speed and Rotation
                float rot = 0;
                if (layer.layerType == LayerType.Planar)
                {
                    rot = GetTransitionValue(layer.rotation_float, normalizedLayerTime * 100);
                }

                selectedMat.SetVector("_speedAndRotation_" + layerNum, new Vector4(0, 0, rot));

                // Tiling and Offset
                Vector2 currentOffset = GetTransitionValue(layer.offset, normalizedLayerTime * 100);
                Vector4 t = new Vector4(layer.tiling.x, layer.tiling.y, currentOffset.x, currentOffset.y);
                selectedMat.SetVector("_tilingAndOffset_" + layerNum, t);
            }


            // Time frame
            selectedMat.SetVector("_timeFrame_" + layerNum, new Vector4(layer.timeSpan_start, layer.timeSpan_End));
            //Fade time
            //selectedMat.SetFloat("_fadeTime_" + layerNum, 1);
            // normal intensity
            selectedMat.SetFloat("_normalIntensity_" + layerNum, layer.normalIntensity);
            // Tint
            selectedMat.SetFloat("_lightIntensity_" + layerNum, layer.tintercentage / 100);

            // Reset Particle related Params
            selectedMat.SetVector("_rowAndCollumns_" + layerNum, layer.flipBookRowsAndColumns);
            selectedMat.SetVector("_particlesMainParameters_" + layerNum, new Vector4(layer.flipbookAnimSpeed, 0, 0, 0));
            selectedMat.SetVector("_particlesSecondaryParameters_" + layerNum, Vector4.zero);

            // Apply Distortion Values
            Vector2 distortIntAndSize = new Vector2(GetTransitionValue(layer.distortIntensity, normalizedLayerTime * 100), GetTransitionValue(layer.distortSize, normalizedLayerTime * 100));
            selectedMat.SetVector("_distortIntAndSize_" + layerNum, distortIntAndSize);

            Vector2 distortSpeed = GetTransitionValue(layer.distortSpeed, normalizedLayerTime * 100);
            Vector2 distortSharpness = GetTransitionValue(layer.distortSharpness, normalizedLayerTime * 100);
            selectedMat.SetVector("_distortSpeedAndSharp_" + layerNum, new Vector4(distortSpeed.x, distortSpeed.y, distortSharpness.x, distortSharpness.y));
        }

        void ApplySatelliteTextureLayer(Material selectedMat, float dayTime, float normalizedLayerTime, int layerNum, TextureLayer layer, bool changeAlllValues = true)
        {
            //selectedMat.SetFloat("_RenderDistance_" + layerNum, GetTransitionValue(layer.renderDistance, normalizedLayerTime * 100, 0.0f));

            selectedMat.SetTexture("_tex_" + layerNum, layer.texture);
            selectedMat.SetTexture("_normals_" + layerNum, layer.textureNormal);
            selectedMat.SetTexture("_cubemap_" + layerNum, null);

            selectedMat.SetColor("_color_" + layerNum, layer.color.Evaluate(normalizedLayerTime));

            if (layer.movementTypeSatellite == MovementType.Speed)
            {
                // Tiling and Offset
                Vector2 currentWidthHeight = GetTransitionValue(layer.satelliteWidthHeight, normalizedLayerTime * 100, new Vector2(1, 1));
                Vector4 t = new Vector4(currentWidthHeight.x, currentWidthHeight.y, 0, 0);
                selectedMat.SetVector("_tilingAndOffset_" + layerNum, t);


                // speed and Rotation
                float rot = GetTransitionValue(layer.rotation_float, normalizedLayerTime * 100);
                selectedMat.SetVector("_speedAndRotation_" + layerNum, new Vector4(layer.speed_Vec2.x, layer.speed_Vec2.y, rot));
            }
            else
            {
                // Tiling and Offset
                Vector2 currentOffset = GetTransitionValue(layer.offset, normalizedLayerTime * 100);
                Vector2 currentWidthHeight = GetTransitionValue(layer.satelliteWidthHeight, normalizedLayerTime * 100, new Vector2(1, 1));
                Vector4 t = new Vector4(currentWidthHeight.x, currentWidthHeight.y, currentOffset.x, currentOffset.y);
                selectedMat.SetVector("_tilingAndOffset_" + layerNum, t);

                // speed and Rotation
                float rot = GetTransitionValue(layer.rotation_float, normalizedLayerTime * 100);
                selectedMat.SetVector("_speedAndRotation_" + layerNum, new Vector4(0, 0, rot));
            }

            // Time frame
            selectedMat.SetVector("_timeFrame_" + layerNum, new Vector4(layer.timeSpan_start, layer.timeSpan_End));
            //Fade time
            //selectedMat.SetFloat("_fadeTime_" + layerNum, 1);
            // normal intensity
            selectedMat.SetFloat("_normalIntensity_" + layerNum, layer.normalIntensity);
            // Tint
            selectedMat.SetFloat("_lightIntensity_" + layerNum, layer.tintercentage / 100);

            // Reset Particle related Params
            selectedMat.SetVector("_rowAndCollumns_" + layerNum, layer.flipBookRowsAndColumns);
            selectedMat.SetVector("_particlesMainParameters_" + layerNum, new Vector4(layer.flipbookAnimSpeed, 0, 0, 0));
            selectedMat.SetVector("_particlesSecondaryParameters_" + layerNum, Vector4.zero);

            // Reset Distortion values
            selectedMat.SetVector("_distortIntAndSize_" + layerNum, Vector2.zero);
            selectedMat.SetVector("_distortSpeedAndSharp_" + layerNum, Vector4.zero);

        }

        void ApplyParticleTextureLayer(Material selectedMat, float dayTime, float normalizedLayerTime, int layerNum, TextureLayer layer, bool changeAlllValues = true)
        {
            // Reset Unused params
            selectedMat.SetFloat("_RenderDistance_" + layerNum, 0);
            selectedMat.SetTexture("_cubemap_" + layerNum, null);
            selectedMat.SetVector("_distortIntAndSize_" + layerNum, Vector2.zero);
            selectedMat.SetVector("_distortSpeedAndSharp_" + layerNum, Vector4.zero);


            // Time frame
            selectedMat.SetVector("_timeFrame_" + layerNum, new Vector4(layer.timeSpan_start, layer.timeSpan_End));
            //Fade time
            //selectedMat.SetFloat("_fadeTime_" + layerNum, 1);
            // Tint
            selectedMat.SetFloat("_lightIntensity_" + layerNum, layer.tintercentage / 100);

            // Particles
            selectedMat.SetTexture("_tex_" + layerNum, layer.texture);
            selectedMat.SetTexture("_normals_" + layerNum, layer.textureNormal);
            selectedMat.SetFloat("_normalIntensity_" + layerNum, layer.normalIntensity);
            selectedMat.SetVector("_rowAndCollumns_" + layerNum, layer.flipBookRowsAndColumns);
            selectedMat.SetColor("_color_" + layerNum, layer.color.Evaluate(normalizedLayerTime));
            selectedMat.SetVector("_tilingAndOffset_" + layerNum, new Vector4(layer.particleTiling.x, layer.particleTiling.y, layer.particlesOffset.x, layer.particlesOffset.y));
            selectedMat.SetVector("_speedAndRotation_" + layerNum, GetTransitionValue(layer.particleRotation, normalizedLayerTime * 100));
            selectedMat.SetVector("_particlesMainParameters_" + layerNum, new Vector4(layer.flipbookAnimSpeed, layer.particlesAmount, layer.particleMinSize, layer.particleMaxSize));
            selectedMat.SetVector("_particlesSecondaryParameters_" + layerNum, new Vector4(layer.particlesHorizontalSpread, layer.particlesVerticalSpread, layer.particleMinFade, layer.particleMaxFade));


        }

        #endregion

        #region Transition Values Utility Methods

        float GetTransitionValue(List<TransitioningFloat> _list, float percentage, float defaultVal = 0)
        {
            if (_list == null || _list.Count < 1)
            {
                return defaultVal;
            }

            if (_list.Count == 1)
            {
                return _list[0].value;
            }

            TransitioningFloat min = _list[0], max = _list[0];


            for (int i = 0; i < _list.Count; i++)
            {
                if (percentage <= _list[i].percentage)
                {
                    max = _list[i];

                    if ((i - 1) > 0)
                    {
                        min = _list[i - 1];
                    }

                    break;
                }
            }

            float t = Mathf.InverseLerp(min.percentage, max.percentage, percentage);
            return Mathf.Lerp(min.value, max.value, t);
        }

        Vector2 GetTransitionValue(List<TransitioningVector2> _list, float percentage, Vector2 defaultVal = default(Vector2))
        {
            Vector2 offset = defaultVal;

            if (_list == null || _list.Count == 0)
            {
                return offset;
            }

            if (_list.Count == 1)
            {
                offset = _list[0].value;
                return offset;
            }


            TransitioningVector2 min = _list[0], max = _list[0];

            for (int i = 0; i < _list.Count; i++)
            {
                if (percentage <= _list[i].percentage)
                {
                    max = _list[i];

                    if ((i - 1) > 0)
                    {
                        min = _list[i - 1];
                    }

                    break;
                }
            }
            float t = Mathf.InverseLerp(min.percentage, max.percentage, percentage);
            offset = Vector2.Lerp(min.value, max.value, t);

            return offset;
        }

        Vector3 GetTransitionValue(List<TransitioningVector3> _list, float percentage)
        {
            Vector3 offset = new Vector3(0, 0, 0);

            if (_list == null || _list.Count == 0)
            {
                return offset;
            }

            if (_list.Count == 1)
            {
                offset = _list[0].value;
                return offset;
            }


            TransitioningVector3 min = _list[0], max = _list[0];

            for (int i = 0; i < _list.Count; i++)
            {
                if (percentage <= _list[i].percentage)
                {
                    max = _list[i];

                    if ((i - 1) > 0)
                    {
                        min = _list[i - 1];
                    }

                    break;
                }
            }

            float t = Mathf.InverseLerp(min.percentage, max.percentage, percentage);
            offset = Vector3.Lerp(min.value, max.value, t);

            return offset;
        }

        #endregion

        public void ResetMaterial(Material selectedMat, int slotCount)
        {
            for (int i = 0; i < slotCount; i++)
            {
                ResetSlot(selectedMat, i);
            }
        }

        public void ResetSlot(Material selectedMat, int slotCount)
        {
            selectedMat.SetFloat("_layerType_" + slotCount, 0);
            selectedMat.SetTexture("_tex_" + slotCount, null);
            selectedMat.SetTexture("_cubemap_" + slotCount, null);
            selectedMat.SetTexture("_normals_" + slotCount, null);
            selectedMat.SetColor("_color_" + slotCount, new Color(1, 1, 1, 0));
            selectedMat.SetVector("_tilingAndOffset_" + slotCount, new Vector4(1, 1, 0, 0));
            selectedMat.SetVector("_speedAndRotation_" + slotCount, new Vector4(0, 0, 0));
            selectedMat.SetVector("_timeFrame_" + slotCount, new Vector4(0, 0));
            selectedMat.SetFloat("_fadeTime_" + slotCount, 1);
            selectedMat.SetFloat("_normalIntensity_" + slotCount, 0);
            selectedMat.SetFloat("_lightIntensity_" + slotCount, 0);
            selectedMat.SetFloat("_RenderDistance_" + slotCount, 3.4f);
            selectedMat.SetVector("_distortIntAndSize_" + slotCount, new Vector2(0, 0));
            selectedMat.SetVector("_distortSpeedAndSharp_" + slotCount, new Vector4(0, 0, 0, 0));
            selectedMat.SetVector("_rowAndCollumns_" + slotCount, new Vector4(1, 1));
            selectedMat.SetVector("_particlesMainParameters_" + slotCount, new Vector4(0, 0, 0, 0));
            selectedMat.SetVector("_particlesSecondaryParameters_" + slotCount, new Vector4(0, 0, 0, 0));
        }

        Quaternion Vector4ToQuaternion(Vector4 val) { return new Quaternion(val.x, val.y, val.z, val.w); }
    }
}