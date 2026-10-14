// 합이오 iOS 햅틱. Unity C# (LabHitHaptics) 에서 DllImport("__Internal") 로 부른다.
// style: 0 Light, 1 Medium, 2 Heavy, 3 Soft, 4 Rigid / intensity: 0~1 (iOS 13+)
#import <UIKit/UIKit.h>

extern "C" void C6Lab_PlayImpactHaptic(int style, float intensity)
{
    UIImpactFeedbackStyle feedbackStyle = UIImpactFeedbackStyleMedium;
    switch (style)
    {
        case 0: feedbackStyle = UIImpactFeedbackStyleLight; break;
        case 1: feedbackStyle = UIImpactFeedbackStyleMedium; break;
        case 2: feedbackStyle = UIImpactFeedbackStyleHeavy; break;
        case 3: if (@available(iOS 13.0, *)) feedbackStyle = UIImpactFeedbackStyleSoft; break;
        case 4: if (@available(iOS 13.0, *)) feedbackStyle = UIImpactFeedbackStyleRigid; break;
        default: break;
    }

    dispatch_async(dispatch_get_main_queue(), ^{
        UIImpactFeedbackGenerator *generator = [[UIImpactFeedbackGenerator alloc] initWithStyle:feedbackStyle];
        [generator prepare];
        if (@available(iOS 13.0, *))
        {
            CGFloat clamped = intensity < 0.f ? 0.f : (intensity > 1.f ? 1.f : intensity);
            [generator impactOccurredWithIntensity:clamped];
        }
        else
        {
            [generator impactOccurred];
        }
    });
}
