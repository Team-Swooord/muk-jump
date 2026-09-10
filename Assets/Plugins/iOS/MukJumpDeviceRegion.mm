#import <Foundation/Foundation.h>
#include <string.h>

// 언어·GPS·통신사 대신 설정 > 일반 > 언어 및 지역의 지역을 읽는다.
extern "C" void MukJumpCopyDeviceRegion(char *buffer, int capacity)
{
    if (!buffer || capacity < 3) return;
    buffer[0] = '\0';
    NSString *region = NSLocale.currentLocale.countryCode;
    if (region.length == 2)
        strlcpy(buffer, region.uppercaseString.UTF8String, (size_t)capacity);
}
