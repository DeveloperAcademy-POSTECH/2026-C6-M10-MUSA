// 합이오 iOS 햅틱. Unity C# (LabHitHaptics) 에서 DllImport("__Internal") 로 부른다.
// style: 0 Light, 1 Medium, 2 Heavy, 3 Soft, 4 Rigid / intensity: 0~1 (iOS 13+)
#import <UIKit/UIKit.h>
#import <CoreHaptics/CoreHaptics.h>

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

// 박 색상 콤보 실패(#222): seconds 동안 이어지는 진동. Core Haptics(iOS 13+, 지원 기기)로 한 번에
// 이어서 울리고, 지원하지 않으면 Heavy 탭을 촘촘히 반복해 비슷하게 만든다.
static id c6HapticEngine = nil;

static void C6Lab_PlayImpactFallback(float seconds)
{
    const double step = 0.06;
    int count = (int)(seconds / step);
    for (int i = 0; i < count; i++)
    {
        dispatch_after(dispatch_time(DISPATCH_TIME_NOW, (int64_t)(i * step * NSEC_PER_SEC)), dispatch_get_main_queue(), ^{
            UIImpactFeedbackGenerator *generator = [[UIImpactFeedbackGenerator alloc] initWithStyle:UIImpactFeedbackStyleHeavy];
            [generator impactOccurred];
        });
    }
}

extern "C" void C6Lab_PlayContinuousHaptic(float seconds, float intensity, float sharpness)
{
    float duration = seconds < 0.05f ? 0.05f : (seconds > 30.f ? 30.f : seconds);
    float strength = intensity < 0.f ? 0.f : (intensity > 1.f ? 1.f : intensity);
    float sharp = sharpness < 0.f ? 0.f : (sharpness > 1.f ? 1.f : sharpness);
    dispatch_async(dispatch_get_main_queue(), ^{
        BOOL played = NO;
        if (@available(iOS 13.0, *))
        {
            if (CHHapticEngine.capabilitiesForHardware.supportsHaptics)
            {
                NSError *error = nil;
                if (c6HapticEngine == nil)
                {
                    CHHapticEngine *engine = [[CHHapticEngine alloc] initAndReturnError:&error];
                    if (engine != nil)
                    {
                        // 한 번 만든 엔진을 앱이 끝날 때까지 쓰므로 그대로 붙잡아 둔다.
                        engine.resetHandler = ^{ [engine startAndReturnError:nil]; };
                        c6HapticEngine = engine;
                    }
                }
                CHHapticEngine *engine = (CHHapticEngine *)c6HapticEngine;
                if (engine != nil && [engine startAndReturnError:&error])
                {
                    CHHapticEventParameter *power = [[CHHapticEventParameter alloc]
                        initWithParameterID:CHHapticEventParameterIDHapticIntensity value:strength];
                    CHHapticEventParameter *edge = [[CHHapticEventParameter alloc]
                        initWithParameterID:CHHapticEventParameterIDHapticSharpness value:sharp];
                    CHHapticEvent *event = [[CHHapticEvent alloc] initWithEventType:CHHapticEventTypeHapticContinuous
                        parameters:@[power, edge] relativeTime:0 duration:duration];
                    CHHapticPattern *pattern = [[CHHapticPattern alloc] initWithEvents:@[event] parameters:@[] error:&error];
                    id<CHHapticPatternPlayer> player = pattern != nil ? [engine createPlayerWithPattern:pattern error:&error] : nil;
                    played = player != nil && [player startAtTime:CHHapticTimeImmediate error:&error];
                }
            }
        }
        if (!played) C6Lab_PlayImpactFallback(duration);
    });
}
