#import <Foundation/Foundation.h>

// 빌드 후처리의 명시적인 QA 표식 없이는 네이티브 초기화도 동작하지 않는다.
extern "C" int MJPrereleaseResetBuildNumber()
{
    NSDictionary *info = NSBundle.mainBundle.infoDictionary;
    if (![info[@"MukJumpPrereleaseReset"] boolValue]) return 0;
    return [info[@"CFBundleVersion"] intValue];
}

extern "C" bool MJPrereleaseResetNativePreferences()
{
    if (MJPrereleaseResetBuildNumber() <= 0) return false;
    NSUserDefaults *defaults = NSUserDefaults.standardUserDefaults;
    // Game Center의 계정별 미전송 기록을 포함한다. OS 권한·다른 앱·키체인은 건드리지 않는다.
    for (NSString *key in defaults.dictionaryRepresentation.allKeys)
        if ([key hasPrefix:@"MukJump."]) [defaults removeObjectForKey:key];
    return [defaults synchronize];
}
