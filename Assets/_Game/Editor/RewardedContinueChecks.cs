using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using TowerTrials;
using UnityEditor;

public static class RewardedContinueChecks
{
    public sealed class Provider : IRewardedContinueProvider
    {
        public bool ready = true;
        public readonly List<Action<RewardedContinueResult>> callbacks = new List<Action<RewardedContinueResult>>();
        public bool IsReady => ready;
        public void Show(Action<RewardedContinueResult> finished) => callbacks.Add(finished);
    }

    [MenuItem("Hero Wars/Release/Verify rewarded continue hook")]
    public static string Verify()
    {
        int assertions = 0;
        void Check(bool value, string message) { assertions++; if (!value) throw new Exception(message); }
        try
        {
            RewardedContinueAds.Register(null);
            Check(!RewardedContinueAds.IsReady && !RewardedContinueAds.TryShow(out _), "No SDK cannot show or reward an ad");
            var provider = new Provider { ready = false };
            RewardedContinueAds.Register(provider);
            Check(!RewardedContinueAds.TryShow(out _), "Unavailable inventory cannot show");
            provider.ready = true;
            Check(RewardedContinueAds.TryShow(out var first) && first.Result == RewardedContinueResult.Pending, "Ready provider creates a pending request");
            Check(!RewardedContinueAds.TryShow(out _) && provider.callbacks.Count == 1, "Only one ad can show at a time");
            provider.callbacks[0](RewardedContinueResult.Skipped);
            Check(first.Result == RewardedContinueResult.Skipped && RewardedContinueAds.IsReady, "Closing without reward leaves a skipped result");
            Check(RewardedContinueAds.TryShow(out var second), "Retry can request another ad");
            provider.callbacks[0](RewardedContinueResult.Rewarded);
            Check(first.Result == RewardedContinueResult.Skipped && second.Result == RewardedContinueResult.Pending && !RewardedContinueAds.IsReady,
                "Duplicate callbacks cannot change old outcomes or release the current request");
            Task.Run(() => provider.callbacks[1](RewardedContinueResult.Rewarded)).GetAwaiter().GetResult();
            Check(second.Result == RewardedContinueResult.Rewarded, "SDK result can safely arrive on a worker thread");
            provider.callbacks[1](RewardedContinueResult.Failed);
            Check(second.Result == RewardedContinueResult.Rewarded, "Reward terminal result is delivered exactly once");
            Check(RewardedContinueAds.TryShow(out var cancelled), "Start cancellation fixture");
            cancelled.Cancel(); provider.callbacks[2](RewardedContinueResult.Rewarded);
            Check(cancelled.Result == RewardedContinueResult.Cancelled && RewardedContinueAds.IsReady, "Leaving a request prevents a late reward");
            Check(RewardedContinueAds.TryShow(out var old), "Start provider replacement fixture");
            var replacement = new Provider(); RewardedContinueAds.Register(replacement);
            Check(old.Result == RewardedContinueResult.Cancelled, "Replacing a provider cancels its old request");
            Check(RewardedContinueAds.TryShow(out var current), "New provider can create a request");
            provider.callbacks[3](RewardedContinueResult.Rewarded);
            Check(current.Result == RewardedContinueResult.Pending && !RewardedContinueAds.IsReady, "Old provider cannot release or reward the new request");
            replacement.callbacks[0](RewardedContinueResult.Pending);
            Check(current.Result == RewardedContinueResult.Failed && RewardedContinueAds.IsReady, "Invalid adapter outcome fails without granting a reward");
        }
        finally { RewardedContinueAds.Register(null); }
        string report = "PASS: " + assertions + " rewarded-continue hook assertions. No-provider/unavailable states, single-show lock, skipped ads, worker-thread callbacks, duplicate results, cancellation and replaced-provider isolation. No ad SDK or network requests.\n";
        File.WriteAllText("Artifacts/RewardedContinueValidation.txt", report);
        return report;
    }
}
