using System;
using System.Threading;
using UnityEngine;

namespace TowerTrials
{
    public enum RewardedContinueResult { Pending, Rewarded, Skipped, Failed, Cancelled }

    /// <summary>
    /// Future ad SDK adapter. Register on Unity's main thread; IsReady must be inexpensive.
    /// Finish only after the ad closes. Return Rewarded only after the SDK confirms its reward;
    /// a close, impression, timer, or failed show is not a reward. Always report a terminal result.
    /// The completion callback may arrive on any thread.
    /// </summary>
    public interface IRewardedContinueProvider
    {
        bool IsReady { get; }
        void Show(Action<RewardedContinueResult> finished);
    }

    public sealed class RewardedContinueTicket
    {
        private int result;
        public RewardedContinueResult Result => (RewardedContinueResult)Volatile.Read(ref result);
        public void Cancel() => Complete(RewardedContinueResult.Cancelled);
        internal void Complete(RewardedContinueResult value)
        { Interlocked.CompareExchange(ref result, (int)value, (int)RewardedContinueResult.Pending); }
    }

    // No SDK, test ad, network request or reward is supplied by this reserved integration point.
    public static class RewardedContinueAds
    {
        private static IRewardedContinueProvider provider;
        private static RewardedContinueTicket active;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() => Register(null);

        public static void Register(IRewardedContinueProvider value)
        {
            Interlocked.Exchange(ref active, null)?.Cancel();
            provider = value;
        }

        public static bool IsReady
        {
            get
            {
                if (Volatile.Read(ref active) != null || provider == null) return false;
                try { return provider.IsReady; }
                catch (Exception error)
                {
                    Register(null);
                    Debug.LogWarning("Rewarded continue provider is unavailable: " + error.Message);
                    return false;
                }
            }
        }

        public static bool TryShow(out RewardedContinueTicket ticket)
        {
            ticket = null;
            if (!IsReady) return false;
            IRewardedContinueProvider current = provider;
            var request = new RewardedContinueTicket();
            if (Interlocked.CompareExchange(ref active, request, null) != null) return false;
            ticket = request;
            void Finish(RewardedContinueResult result)
            {
                if (result != RewardedContinueResult.Rewarded && result != RewardedContinueResult.Skipped)
                    result = RewardedContinueResult.Failed;
                request.Complete(result);
                Interlocked.CompareExchange(ref active, null, request);
            }
            try { current.Show(Finish); }
            catch (Exception error)
            {
                Finish(RewardedContinueResult.Failed);
                Debug.LogWarning("Rewarded continue could not show: " + error.Message);
            }
            return true;
        }
    }
}
