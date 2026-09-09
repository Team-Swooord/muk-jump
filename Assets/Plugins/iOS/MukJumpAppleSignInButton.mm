#import <AuthenticationServices/AuthenticationServices.h>
#import <UIKit/UIKit.h>

extern "C" void UnitySendMessage(
    const char *objectName,
    const char *methodName,
    const char *message);
extern "C" UIWindow *UnityGetMainWindow(void);

@interface MukJumpAppleSignInButtonTarget : NSObject
- (void)pressed;
@end

@implementation MukJumpAppleSignInButtonTarget
- (void)pressed
{
    UnitySendMessage(
        "MukJumpAccountRuntime",
        "HandleNativeAppleSignInButton",
        "");
}
@end

static ASAuthorizationAppleIDButton *MukJumpAppleButton = nil;
static MukJumpAppleSignInButtonTarget *MukJumpAppleButtonTarget = nil;

static UIView *MukJumpRootView(void)
{
    UIWindow *window = UnityGetMainWindow();
    if (@available(iOS 13.0, *))
    {
        if (window == nil)
        {
            for (UIScene *scene in UIApplication.sharedApplication.connectedScenes)
            {
                if (scene.activationState != UISceneActivationStateForegroundActive ||
                    ![scene isKindOfClass:UIWindowScene.class])
                    continue;
                for (UIWindow *candidate in ((UIWindowScene *)scene).windows)
                {
                    if (candidate.isKeyWindow)
                    {
                        window = candidate;
                        break;
                    }
                }
                if (window != nil)
                    break;
            }
        }
    }
    return window.rootViewController.view ?: window;
}

extern "C" int MukJumpAppleSignInButtonShow(
    float x,
    float y,
    float width,
    float height,
    int enabled)
{
    __block int didShow = 0;
    void (^showBlock)(void) = ^{
        if (@available(iOS 13.0, *))
        {
            UIView *root = MukJumpRootView();
            if (root == nil)
                return;
            if (MukJumpAppleButton == nil)
            {
                MukJumpAppleButton = [ASAuthorizationAppleIDButton
                    buttonWithType:ASAuthorizationAppleIDButtonTypeContinue
                    style:ASAuthorizationAppleIDButtonStyleBlack];
                MukJumpAppleButtonTarget =
                    [[MukJumpAppleSignInButtonTarget alloc] init];
                [MukJumpAppleButton addTarget:MukJumpAppleButtonTarget
                    action:@selector(pressed)
                    forControlEvents:UIControlEventTouchUpInside];
                MukJumpAppleButton.cornerRadius = 8.0;
                MukJumpAppleButton.accessibilityIdentifier =
                    @"MukJump.SignInWithApple";
            }
            if (MukJumpAppleButton.superview != root)
            {
                [MukJumpAppleButton removeFromSuperview];
                [root addSubview:MukJumpAppleButton];
            }

            CGRect bounds = root.bounds;
            MukJumpAppleButton.frame = CGRectMake(
                bounds.size.width * x,
                bounds.size.height * (1.0 - y - height),
                bounds.size.width * width,
                bounds.size.height * height);
            MukJumpAppleButton.enabled = enabled != 0;
            MukJumpAppleButton.alpha = 1.0;
            MukJumpAppleButton.hidden = NO;
            didShow = 1;
        }
    };
    if (NSThread.isMainThread)
        showBlock();
    else
        dispatch_sync(dispatch_get_main_queue(), showBlock);
    return didShow;
}

extern "C" void MukJumpAppleSignInButtonHide(void)
{
    void (^hideBlock)(void) = ^{
        MukJumpAppleButton.hidden = YES;
    };
    if (NSThread.isMainThread)
        hideBlock();
    else
        dispatch_async(dispatch_get_main_queue(), hideBlock);
}
