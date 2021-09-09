namespace GPUSkinning
{
    public class GPUSkinningThrottler
    {
        internal static int startingFrame = 0;

        internal readonly SimpleGPUSkinning gpuSkinning;
        internal int framesBetweenUpdates;
        internal int currentFrame;

        public GPUSkinningThrottler(SimpleGPUSkinning gpuSkinning)
        {
            this.gpuSkinning = gpuSkinning;
            framesBetweenUpdates = 1;
            currentFrame = startingFrame++;
        }

        public void SetThrottling(int newFramesBetweenUpdates) { framesBetweenUpdates = newFramesBetweenUpdates; }

        public void TryUpdate()
        {
            currentFrame++;
            if (currentFrame % framesBetweenUpdates == 0)
                gpuSkinning.Update();
        }

    }
}