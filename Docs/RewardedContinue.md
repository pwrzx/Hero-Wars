# Reserved rewarded-continue placement

The defeat card offers **Watch ad & continue** and **Retry**. The ad option preserves the hero's power and every previously cleared room; Retry starts the keep from the beginning. Cleared stages still use their existing Next stage / Play again action.

No ad provider or account is configured yet. The placement stays visible with an unavailable message and a disabled ad button. Retry works immediately. There is no timer-based simulated reward, automatic ad, ad SDK, advertising identifier, network call or additional permission in this change.

## Connect an ad provider later

Implement `TowerTrials.IRewardedContinueProvider` and call `RewardedContinueAds.Register(adapter)` on Unity's main thread after the SDK is ready. Its `IsReady` property must reflect a loaded rewarded ad and be safe to read every frame. Keep the same adapter registered while inventory loads; the defeat button enables automatically when it becomes ready.

`Show(Action<RewardedContinueResult> finished)` presents the rewarded placement. Invoke its completion callback only when the ad has finished closing:

- `Rewarded`: the SDK explicitly confirmed the earned reward **and** the ad has closed.
- `Skipped`: the player closed the ad without earning the reward.
- `Failed`: the SDK failed to load/show or otherwise could not finish the placement.

If an SDK reports closure and reward separately or out of order, the adapter must join those events before completing. Do not infer a reward from elapsed time, an impression, a click or closure alone. Always finish failed/cancelled SDK shows too, so subsequent ad requests can proceed. SDK callbacks may arrive on worker threads; the bridge records their result atomically, and the controller applies gameplay and saves on Unity's main thread.

Only one show can be pending. Duplicate callbacks cannot issue a second revive. Retry, scene exit and provider replacement invalidate the pending gameplay request, so a late reward cannot affect a different run. Reviving clears only the failed flag and immediately saves that same checkpoint; it does not grant power or currency.

When adding an actual SDK, also update privacy/consent handling, SDK-specific platform settings, store disclosures and native build validation. The current offline Android postprocessor removes Internet permission from release builds; revise that behavior deliberately when enabling real ads. Use the provider's official test IDs for device testing and the publisher's IDs for production. The current reserved placement is not a live advertising integration.

## Verification

- `Tower Trials > Verify Game Rules`: revive/save round trips, retained power/room state and rejection of repeated/live/completed-run revival.
- `Hero Wars > Release > Verify rewarded continue hook`: absent inventory, single-show lock, skipped/duplicate/worker-thread callbacks, cancellation and provider replacement.
- `Hero Wars > Release > Verify rewarded continue (Play Mode)`: real attacks and UI pointers through defeat, unavailable placement, Retry, skipped/failed shows, stale rewards, successful revival and post-revive combat. Its provider exists only in Editor test code; it is not included in mobile players.

Checks restore the player's primary/backup saves. Reports and defeat-screen captures are in `Artifacts`.
