// #28 short Taptic Engine feedback for C6.Prototype.Presentation.Haptics (iOS only).
// Generators are kept and re-prepared after each use so the next tap starts without delay.
#import <UIKit/UIKit.h>

static UIImpactFeedbackGenerator *c6Impacts[3];
static UINotificationFeedbackGenerator *c6Notification;

extern "C" void C6Haptics_Impact(int style)
{
    int index = style < 0 ? 0 : style > 2 ? 2 : style;
    if (c6Impacts[index] == nil)
    {
        UIImpactFeedbackStyle styles[3] = { UIImpactFeedbackStyleLight, UIImpactFeedbackStyleMedium, UIImpactFeedbackStyleHeavy };
        c6Impacts[index] = [[UIImpactFeedbackGenerator alloc] initWithStyle:styles[index]];
    }
    [c6Impacts[index] impactOccurred];
    [c6Impacts[index] prepare];
}

extern "C" void C6Haptics_Success(void)
{
    if (c6Notification == nil) c6Notification = [[UINotificationFeedbackGenerator alloc] init];
    [c6Notification notificationOccurred:UINotificationFeedbackTypeSuccess];
    [c6Notification prepare];
}
