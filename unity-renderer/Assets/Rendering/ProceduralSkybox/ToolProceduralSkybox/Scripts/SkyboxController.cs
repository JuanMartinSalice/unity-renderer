using System;
using System.Linq;
using UnityEngine;
using DCL.ServerTime;

namespace DCL.Skybox
{
    /// <summary>
    /// This class will handle runtime execution of skybox cycle. 
    /// Load and assign material to the Skybox.
    /// This will mostly increment the time cycle and apply values from configuration to the material.
    /// </summary>
    public class SkyboxController : IPlugin
    {
        public event SkyboxConfiguration.TimelineEvents OnTimelineEvent;

        public static SkyboxController i { get; private set; }

        public const string DEFAULT_SKYBOX_ID = "Generic_Skybox";

        public string loadedConfig;
        //Time for one complete circle. In Hours. default 24
        public float cycleTime = 24;
        public float lifecycleDuration = 2;

        private float timeOfTheDay;                            // (Nishant.K) Time will be provided from outside, So remove this variable
        private Light directionalLight;
        private SkyboxConfiguration configuration;
        private Material selectedMat;
        private bool overrideDefaultSkybox;
        private string overrideSkyboxID;
        private bool isPaused;
        private float timeNormalizationFactor;
        private int slotCount;
        private bool overrideByEditor = false;

        // Reflection probe
        private ReflectionProbe skyboxProbe;
        bool probeParented = false;
        private float reflectionUpdateTime = 1;                                 // In Mins
        private ReflectionProbeRuntime runtimeReflectionObj;

        public SkyboxController()
        {
            i = this;

            // Find and delete test directional light obj if any
            Light[] testDirectionalLight = GameObject.FindObjectsOfType<Light>().Where(s => s.name == "The Sun_Temp").ToArray();
            for (int i = 0; i < testDirectionalLight.Length; i++)
            {
                GameObject.DestroyImmediate(testDirectionalLight[i].gameObject);
            }

            // Get or Create new Directional Light Object
            directionalLight = GameObject.FindObjectsOfType<Light>().Where(s => s.type == LightType.Directional).FirstOrDefault();

            if (directionalLight == null)
            {
                GameObject temp = new GameObject("The Sun");
                // Add the light component
                directionalLight = temp.AddComponent<Light>();
                directionalLight.type = LightType.Directional;
            }

            GetOrCreateEnvironmentProbe();

            // Get current time from the server
            GetTimeFromTheServer(WorldTimer.i.GetCurrentTime());
            WorldTimer.i.OnTimeChanged += GetTimeFromTheServer;

            // Update config whenever skybox config changed in data store. Can be used for both testing and runtime
            DataStore.i.skyboxConfig.objectUpdated.OnChange += UpdateConfig;

            // Change as Kernel config is initialized or updated
            KernelConfig.i.EnsureConfigInitialized()
                        .Then(config =>
                        {
                            KernelConfig_OnChange(config, null);
                        });

            KernelConfig.i.OnChange += KernelConfig_OnChange;

            DCL.Environment.i.platform.updateEventHandler.AddListener(IUpdateEventHandler.EventType.Update, Update);
        }

        private void GetOrCreateEnvironmentProbe()
        {
            // Get Reflection Probe Object
            skyboxProbe = GameObject.FindObjectsOfType<ReflectionProbe>().Where(s => s.name == "SkyboxProbe").FirstOrDefault();

            if (DataStore.i.skyboxConfig.disableReflection.Get())
            {
                if (skyboxProbe != null)
                {
                    skyboxProbe.gameObject.SetActive(false);
                }

                RenderSettings.defaultReflectionMode = UnityEngine.Rendering.DefaultReflectionMode.Skybox;
                RenderSettings.customReflection = null;
                Debug.Log("Procedural Skybox :: Reflection disabled from server: " + DataStore.i.skyboxConfig.disableReflection.Get());
                return;
            }

            if (skyboxProbe == null)
            {
                // Instantiate new probe from the resources
                GameObject temp = Resources.Load<GameObject>("SkyboxReflectionProbe/SkyboxProbe");
                GameObject probe = GameObject.Instantiate<GameObject>(temp);
                probe.name = "SkyboxProbe";
                skyboxProbe = probe.GetComponent<ReflectionProbe>();

                // make probe a child of main camera
                ParentProbeWithCamera();
            }

            // Update time in Reflection Probe
            runtimeReflectionObj = skyboxProbe.GetComponent<ReflectionProbeRuntime>();
            if (runtimeReflectionObj == null)
            {
                runtimeReflectionObj = skyboxProbe.gameObject.AddComponent<ReflectionProbeRuntime>();
            }
            // Assign as seconds
            runtimeReflectionObj.updateAfter = reflectionUpdateTime * 60;

            RenderSettings.defaultReflectionMode = UnityEngine.Rendering.DefaultReflectionMode.Custom;
            RenderSettings.customReflection = null;
            Debug.Log("Procedural Skybox :: Reflection object instantiated:");
        }

        private void ParentProbeWithCamera()
        {
            if (skyboxProbe == null)
            {
#if UNITY_EDITOR
                Debug.LogError("Cannot parent the probe as probe is not instantiated");
#endif
                Debug.Log("Procedural Skybox :: skyboxProbe is null ");
                return;
            }

            // make probe a child of main camera
            if (Camera.main != null)
            {
                GameObject mainCam = Camera.main.gameObject;
                skyboxProbe.transform.parent = mainCam.transform;
                Debug.Log("Procedural Skybox :: Parenting done");
                probeParented = true;
            }
        }

        private void KernelConfig_OnChange(KernelConfigModel current, KernelConfigModel previous)
        {
            if (overrideByEditor)
            {
                return;
            }
            // set skyboxConfig to true
            DataStore.i.skyboxConfig.useProceduralSkybox.Set(true);
            DataStore.i.skyboxConfig.configToLoad.Set(current.proceduralSkyboxConfig.configToLoad);
            DataStore.i.skyboxConfig.lifecycleDuration.Set(current.proceduralSkyboxConfig.lifecycleDuration);
            DataStore.i.skyboxConfig.jumpToTime.Set(current.proceduralSkyboxConfig.fixedTime);
            DataStore.i.skyboxConfig.updateReflectionTime.Set(current.proceduralSkyboxConfig.updateReflectionTime);
            DataStore.i.skyboxConfig.disableReflection.Set(current.proceduralSkyboxConfig.disableReflection);

            Debug.Log("Procedural Skybox :: Disable Reflection value from kernel: " + current.proceduralSkyboxConfig.disableReflection);
            Debug.Log("Procedural Skybox :: updateReflectionTime value from kernel: " + current.proceduralSkyboxConfig.updateReflectionTime);

            // Call update on skybox config which will call Update config in this class.
            DataStore.i.skyboxConfig.objectUpdated.Set(true, true);
        }

        /// <summary>
        /// Called whenever any change in skyboxConfig is observed
        /// </summary>
        /// <param name="current"></param>
        /// <param name="previous"></param>
        public void UpdateConfig(bool current = true, bool previous = false)
        {
            if (overrideByEditor)
            {
                return;
            }

            if (loadedConfig != DataStore.i.skyboxConfig.configToLoad.Get())
            {
                // Apply configuration
                overrideDefaultSkybox = true;
                overrideSkyboxID = DataStore.i.skyboxConfig.configToLoad.Get();
            }

            // Apply time
            lifecycleDuration = DataStore.i.skyboxConfig.lifecycleDuration.Get();

            if (DataStore.i.skyboxConfig.useProceduralSkybox.Get())
            {
                if (!ApplyConfig())
                {
                    RenderProfileManifest.i.currentProfile.Apply();
                }
            }
            else
            {
                RenderProfileManifest.i.currentProfile.Apply();
            }

            // Reset Object Update value without notifying
            DataStore.i.skyboxConfig.objectUpdated.Set(false, false);

            // if Paused
            if (DataStore.i.skyboxConfig.jumpToTime.Get() >= 0)
            {
                PauseTime(true, DataStore.i.skyboxConfig.jumpToTime.Get());
            }
            else
            {
                ResumeTime();
            }

            // Update reflection time
            if (DataStore.i.skyboxConfig.disableReflection.Get())
            {
                if (skyboxProbe != null)
                {
                    skyboxProbe.gameObject.SetActive(false);
                }

                RenderSettings.defaultReflectionMode = UnityEngine.Rendering.DefaultReflectionMode.Skybox;
                RenderSettings.customReflection = null;
                Debug.Log("Procedural Skybox :: disable by kernel rendering default skybox");
            }
            else if (runtimeReflectionObj != null)
            {
                // If reflection update time is -1 then calculate time based on the cycle time, else assign same
                if (DataStore.i.skyboxConfig.updateReflectionTime.Get() >= 0)
                {
                    reflectionUpdateTime = DataStore.i.skyboxConfig.updateReflectionTime.Get();
                }
                else
                {
                    // Evaluate with the cycle time
                    reflectionUpdateTime = 1;               // Default for an hour is 1 min
                    // get cycle time in hours
                    reflectionUpdateTime = (DataStore.i.skyboxConfig.lifecycleDuration.Get() / 60);
                }
                runtimeReflectionObj.updateAfter = Mathf.Clamp(reflectionUpdateTime * 60, 5, 86400);
                Debug.Log("Procedural Skybox :: probe enabled and update after: " + runtimeReflectionObj.updateAfter);
            }
        }

        /// <summary>
        /// Apply changed configuration
        /// </summary>
        bool ApplyConfig()
        {
            if (overrideByEditor)
            {
                return false;
            }

            if (!SelectSkyboxConfiguration())
            {
                return false;
            }

            if (!configuration.useDirectionalLight)
            {
                directionalLight.gameObject.SetActive(false);
            }

            // Calculate time factor
            if (lifecycleDuration <= 0)
            {
                lifecycleDuration = 0.01f;
            }

            // Convert minutes in seconds and then normalize with cycle time
            timeNormalizationFactor = lifecycleDuration * 60 / cycleTime;

            GetTimeFromTheServer(WorldTimer.i.GetCurrentTime());
            Debug.Log("Procedural Skybox :: Returning time: " + WorldTimer.i.GetCurrentTime() + " : timeOfTheDay: " + timeOfTheDay);
            return true;
        }

        void GetTimeFromTheServer(DateTime serverTime)
        {
            // Convert miliseconds to seconds
            double seconds = serverTime.Second + (serverTime.Millisecond / 1000);
            //seconds = (float)(Mathf.Round(seconds * 100)) / 100f;
            // Convert seconds to minutes
            double minutes = serverTime.Minute + (seconds / 60);
            //minutes = (float)(Mathf.Round(minutes * 100)) / 100f;
            // Convert minutes to hour (in float format)
            double totalTimeInMins = serverTime.Hour * 60 + minutes;
            //totalTimeInMins = (float)(Mathf.Round(totalTimeInMins * 100)) / 100f;

            // Get the cycle min and max
            //double cycleMin = ((int)(totalTimeInMins / lifecycleDuration)) * lifecycleDuration;
            //double cycleMax = cycleMin + (int)lifecycleDuration;

            //double percentage = Mathf.InverseLerp(cycleMin, cycleMax, totalTimeInMins);
            //percentage = (float)(Mathf.Round(percentage * 1000)) / 1000f;

            double timeInCycle = (totalTimeInMins / lifecycleDuration) + 1;
            double percentageSkyboxtime = timeInCycle - (int)timeInCycle;


            timeOfTheDay = (float)percentageSkyboxtime * cycleTime;

            //Debug.Log("Procedural Skybox :: Current UTC time: " + WorldTimer.i.GetCurrentTime().ToString());
            //Debug.Log("Procedural Skybox :: Cycle Min: " + cycleMin + ", Cycle max: " + cycleMax);
            //Debug.Log("Procedural Skybox :: Calculated time of the day: " + timeOfTheDay);

            //// divide by lifecycleDuration.... + 1 as time is from 0
            //float timeInCycle = (totalTimeInMins / lifecycleDuration) + 1;
            //// get percentage part for converting to skybox time
            //float percentageSkyboxtime = timeInCycle - (int)timeInCycle;

            //timeOfTheDay = percentageSkyboxtime * cycleTime;
        }

        /// <summary>
        /// Select Configuration to load.
        /// </summary>
        private bool SelectSkyboxConfiguration()
        {
            bool tempConfigLoaded = true;

            string configToLoad = loadedConfig;
            if (string.IsNullOrEmpty(loadedConfig))
            {
                configToLoad = DEFAULT_SKYBOX_ID;
            }


            if (overrideDefaultSkybox)
            {
                configToLoad = overrideSkyboxID;
                overrideDefaultSkybox = false;
            }

            // config already loaded, return
            if (configToLoad.Equals(loadedConfig))
            {
                return tempConfigLoaded;
            }

            SkyboxConfiguration newConfiguration = Resources.Load<SkyboxConfiguration>("Skybox Configurations/" + configToLoad);

            if (newConfiguration == null)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogError(configToLoad + " configuration not found in Resources. Trying to load Default config: " + DEFAULT_SKYBOX_ID + "(Default path through tool is Assets/Scripts/Resources/Skybox Configurations)");
#endif
                // Try to load default config
                configToLoad = DEFAULT_SKYBOX_ID;
                newConfiguration = Resources.Load<SkyboxConfiguration>("Skybox Configurations/" + configToLoad);

                if (newConfiguration == null)
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Debug.LogError("Default configuration not found in Resources. Shifting to old skybox. (Default path through tool is Assets/Scripts/Resources/Skybox Configurations)");
#endif
                    tempConfigLoaded = false;
                    return tempConfigLoaded;
                }
            }

            // Register to timelineEvents
            if (configuration != null)
            {
                configuration.OnTimelineEvent -= Configuration_OnTimelineEvent;
            }
            newConfiguration.OnTimelineEvent += Configuration_OnTimelineEvent;
            configuration = newConfiguration;

            // Apply material as per number of Slots.
            MaterialReferenceContainer.Mat_Layer matLayer = MaterialReferenceContainer.i.GetMat_LayerForLayers(configuration.slots.Count);
            if (matLayer == null)
            {
                matLayer = MaterialReferenceContainer.i.materials[0];
            }

            configuration.ResetMaterial(matLayer.material, matLayer.numberOfSlots);
            selectedMat = matLayer.material;
            slotCount = matLayer.numberOfSlots;

            if (DataStore.i.skyboxConfig.useProceduralSkybox.Get())
            {
                RenderSettings.skybox = selectedMat;
            }

            // Update loaded config
            loadedConfig = configToLoad;

            return tempConfigLoaded;
        }

        private void Configuration_OnTimelineEvent(string tag, bool enable, bool trigger) { OnTimelineEvent?.Invoke(tag, enable, trigger); }

        // Update is called once per frame
        public void Update()
        {
            if (!DataStore.i.skyboxConfig.disableReflection.Get() && skyboxProbe != null && !probeParented)
            {
                ParentProbeWithCamera();
            }

            if (configuration == null || isPaused || !DataStore.i.skyboxConfig.useProceduralSkybox.Get())
            {
                return;
            }

            // Control is in editor tool
            if (overrideByEditor)
            {
                return;
            }

            timeOfTheDay += Time.deltaTime / timeNormalizationFactor;
            //GetTimeFromTheServer(WorldTimer.i.GetCurrentTime());
            timeOfTheDay = Mathf.Clamp(timeOfTheDay, 0.01f, cycleTime);
            DataStore.i.skyboxConfig.currentVirtualTime.Set(timeOfTheDay);

            configuration.ApplyOnMaterial(selectedMat, timeOfTheDay, GetNormalizedDayTime(), slotCount, directionalLight, cycleTime);

            // Cycle resets
            if (timeOfTheDay >= cycleTime)
            {
                timeOfTheDay = 0.01f;
                configuration.CycleResets();
            }
        }

        public void Dispose()
        {
            // set skyboxConfig to false
            DataStore.i.skyboxConfig.useProceduralSkybox.Set(false);
            DataStore.i.skyboxConfig.objectUpdated.OnChange -= UpdateConfig;

            WorldTimer.i.OnTimeChanged -= GetTimeFromTheServer;
            configuration.OnTimelineEvent -= Configuration_OnTimelineEvent;
            KernelConfig.i.OnChange -= KernelConfig_OnChange;
            DCL.Environment.i.platform.updateEventHandler.RemoveListener(IUpdateEventHandler.EventType.Update, Update);
        }

        public void PauseTime(bool overrideTime = false, float newTime = 0)
        {
            isPaused = true;
            if (overrideTime)
            {
                timeOfTheDay = Mathf.Clamp(newTime, 0, 24);
                configuration.ApplyOnMaterial(selectedMat, (float)timeOfTheDay, GetNormalizedDayTime(), slotCount, directionalLight, cycleTime);
            }
        }

        public void ResumeTime(bool overrideTime = false, float newTime = 0)
        {
            isPaused = false;
            if (overrideTime)
            {
                timeOfTheDay = newTime;
            }
        }

        public bool IsPaused() { return isPaused; }

        private float GetNormalizedDayTime()
        {
            double tTime = 0;

            tTime = timeOfTheDay / cycleTime;

            tTime = ClampDouble(tTime, 0, 1);

            return (float)tTime;
        }

        public SkyboxConfiguration GetCurrentConfiguration() { return configuration; }

        public float GetCurrentTimeOfTheDay() { return (float)timeOfTheDay; }

        public bool SetOverrideController(bool editorOveride)
        {
            overrideByEditor = editorOveride;
            return overrideByEditor;
        }

        // Whenever Skybox editor closed at runtime control returns back to controller with the values in the editor
        public bool GetControlBackFromEditor(string currentConfig, float timeOfTheday, float lifecycleDuration, bool isPaused)
        {
            overrideByEditor = false;

            DataStore.i.skyboxConfig.configToLoad.Set(currentConfig);
            DataStore.i.skyboxConfig.lifecycleDuration.Set(lifecycleDuration);

            if (isPaused)
            {
                PauseTime(true, timeOfTheday);
            }
            else
            {
                ResumeTime(true, timeOfTheday);
            }

            // Call update on skybox config which will call Update config in this class.
            DataStore.i.skyboxConfig.objectUpdated.Set(true, true);

            return overrideByEditor;
        }

        public void UpdateConfigurationTimelineEvent(SkyboxConfiguration newConfig)
        {
            configuration.OnTimelineEvent -= Configuration_OnTimelineEvent;
            newConfig.OnTimelineEvent += Configuration_OnTimelineEvent;
        }

        private double ClampDouble(double timeOfTheDay, double min, float max)
        {
            double result = timeOfTheDay;
            if (timeOfTheDay < min)
            {
                result = min;
            }

            if (timeOfTheDay > max)
            {
                result = max;
            }

            return result;
        }
    }
}