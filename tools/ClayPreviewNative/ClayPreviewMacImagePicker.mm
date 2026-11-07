#import <Cocoa/Cocoa.h>
#include <cstring>

static void HapioWriteUtf8(NSString *text, char *destination, int capacity)
{
    if (destination == nullptr || capacity < 1) return;
    destination[0] = 0;
    const char *value = text.UTF8String;
    if (value == nullptr) return;
    size_t count = std::strlen(value);
    if (count >= static_cast<size_t>(capacity)) count = static_cast<size_t>(capacity - 1);
    std::memcpy(destination, value, count);
    destination[count] = 0;
}

// 1 selected, 0 cancelled, -1 error. Buffers belong to the caller; no native memory escapes.
extern "C" __attribute__((visibility("default")))
int HapioClayChooseImage(char *path, int pathCapacity, char *error, int errorCapacity)
{
    @autoreleasepool
    {
        HapioWriteUtf8(@"", path, pathCapacity); HapioWriteUtf8(@"", error, errorCapacity);
        if (![NSThread isMainThread])
        { HapioWriteUtf8(@"The image picker must run on the main thread.", error, errorCapacity); return -1; }
        if (path == nullptr || pathCapacity < 2)
        { HapioWriteUtf8(@"The file path buffer is unavailable.", error, errorCapacity); return -1; }
        @try
        {
            NSOpenPanel *panel = [NSOpenPanel openPanel];
            panel.title = @"Choose a PNG or JPEG image";
            panel.prompt = @"Choose";
            panel.canChooseFiles = YES; panel.canChooseDirectories = NO;
            panel.allowsMultipleSelection = NO; panel.canCreateDirectories = NO;
            panel.resolvesAliases = YES;
#pragma clang diagnostic push
#pragma clang diagnostic ignored "-Wdeprecated-declarations"
            panel.allowedFileTypes = @[@"png", @"jpg", @"jpeg"];
#pragma clang diagnostic pop
            if ([panel runModal] != NSModalResponseOK) return 0;
            NSString *selected = panel.URL.path;
            if (selected == nil || !panel.URL.isFileURL)
            { HapioWriteUtf8(@"No local image file was selected.", error, errorCapacity); return -1; }
            if ([selected lengthOfBytesUsingEncoding:NSUTF8StringEncoding] >= static_cast<NSUInteger>(pathCapacity))
            { HapioWriteUtf8(@"The selected file path is too long.", error, errorCapacity); return -1; }
            HapioWriteUtf8(selected, path, pathCapacity); return 1;
        }
        @catch (NSException *)
        { HapioWriteUtf8(@"The native image panel could not be opened.", error, errorCapacity); return -1; }
    }
}
