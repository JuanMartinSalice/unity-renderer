using System;
using System.Collections;
using UnityEngine;

namespace DCL
{
    public interface IAvatarLODController : IDisposable
    {
        void SetAvatarState();
        void SetSimpleAvatar();
        void SetImpostorState();
        void UpdateImpostorTint(float distanceToClosestPosition);
    }

    public class AvatarLODController : IAvatarLODController
    {
        private const float TRANSITION_DURATION = 0.25f;
        internal Player player;

        internal float avatarFade;
        internal float impostorFade;
        internal float targetAvatarFade;
        internal float targetImpostorFade;

        internal bool SSAOEnabled;
        internal bool facialFeaturesEnabled;

        internal Coroutine currentTransition = null;

        public AvatarLODController(Player player)
        {
            this.player = player;
            avatarFade = 1;
            targetAvatarFade = 1;
            impostorFade = 0;
            targetImpostorFade = 0;
            SSAOEnabled = true;
            facialFeaturesEnabled = true;
            if (player?.renderer == null)
                return;
            player.renderer.SetAvatarFade(avatarFade);
            player.renderer.SetImpostorFade(impostorFade);
        }

        public void SetAvatarState()
        {
            if (player?.renderer == null)
                return;

            SetAvatarFeatures(true, true);
            StartTransition(1, 0);
        }

        public void SetSimpleAvatar()
        {
            if (player?.renderer == null)
                return;

            SetAvatarFeatures(false, false);
            StartTransition(1, 0);
        }

        public void SetImpostorState()
        {
            if (player?.renderer == null)
                return;

            SetAvatarFeatures(false, false);
            StartTransition(0, 1);
        }

        private void StartTransition(float newTargetAvatarFade, float newTargetImpostorFade)
        {
            if (Mathf.Approximately(targetAvatarFade, newTargetAvatarFade) && Mathf.Approximately(targetImpostorFade, newTargetImpostorFade))
                return;

            targetAvatarFade = newTargetAvatarFade;
            targetImpostorFade = newTargetImpostorFade;
            CoroutineStarter.Stop(currentTransition);
            currentTransition = CoroutineStarter.Start(Transition(newTargetAvatarFade, newTargetImpostorFade));
        }

        internal IEnumerator Transition(float targetAvatarFade, float targetImpostorFade, float transitionDuration = TRANSITION_DURATION)
        {
            while (!player.renderer.isReady)
            {
                yield return null;
            }

            player.renderer.SetAvatarFade(avatarFade);
            player.renderer.SetImpostorFade(impostorFade);
            player.renderer.SetVisibility(true);
            player.renderer.SetImpostorVisibility(true);

            while (!Mathf.Approximately(avatarFade, targetAvatarFade) || !Mathf.Approximately(impostorFade, targetImpostorFade))
            {
                avatarFade = Mathf.MoveTowards(avatarFade, targetAvatarFade, 1f / transitionDuration * Time.deltaTime);
                impostorFade = Mathf.MoveTowards(impostorFade, targetImpostorFade, 1f / transitionDuration * Time.deltaTime);
                player.renderer.SetAvatarFade(avatarFade);
                player.renderer.SetImpostorFade(impostorFade);
                yield return null;
            }

            avatarFade = targetAvatarFade;
            impostorFade = targetImpostorFade;

            bool avatarVisibility = !Mathf.Approximately(avatarFade, 0);
            player.renderer.SetVisibility(avatarVisibility);
            bool impostorVisibility = !Mathf.Approximately(impostorFade, 0);
            player.renderer.SetImpostorVisibility(impostorVisibility);
            currentTransition = null;
        }

        private void SetAvatarFeatures(bool newSSAOEnabled, bool newFacialFeaturesEnabled)
        {
            if (SSAOEnabled != newSSAOEnabled)
            {
                player.renderer.SetSSAOEnabled(newSSAOEnabled);
                SSAOEnabled = newSSAOEnabled;
            }

            if (facialFeaturesEnabled != newFacialFeaturesEnabled)
            {
                player.renderer.SetFacialFeaturesVisible(newFacialFeaturesEnabled);
                facialFeaturesEnabled = newFacialFeaturesEnabled;
            }
        }

        public void UpdateImpostorTint(float distanceToClosestPosition)
        {
            float tintStep = Mathf.InverseLerp(0, 32, distanceToClosestPosition);
            float tintValue = Mathf.Lerp(0.2f, 0.9f, tintStep);
            Color newColor = Color.Lerp(Color.white, Color.black, tintValue);
            newColor.a = Mathf.Lerp(1f, 0.75f, tintStep);

            player.renderer.SetImpostorTextureTint(newColor);
        }

        public void Dispose() { CoroutineStarter.Stop(currentTransition); }
    }
}