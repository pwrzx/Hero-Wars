#import <UIKit/UIKit.h>

extern "C" void PlayImpact()
{
    dispatch_async(dispatch_get_main_queue(), ^{
        UIImpactFeedbackGenerator *feedback = [[UIImpactFeedbackGenerator alloc] initWithStyle:UIImpactFeedbackStyleLight];
        [feedback prepare];
        [feedback impactOccurred];
#if !__has_feature(objc_arc)
        [feedback release];
#endif
    });
}
