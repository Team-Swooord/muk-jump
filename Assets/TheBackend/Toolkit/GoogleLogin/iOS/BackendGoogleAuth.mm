#import <UIKit/UIKit.h>
#import <GoogleSignIn/GoogleSignIn.h>
typedef void (*backendgoogleDelegate)(bool, const char*, const char*);
typedef void (*backendgoogleSignOutDelegate)(bool, const char*);


@interface BackendFederation: UIResponder <UIApplicationDelegate>
{
}

@property (assign) backendgoogleDelegate backendGoogleLoginHandler;
@property (assign) backendgoogleSignOutDelegate backendGoogleSignOutHandler;

@end

@implementation BackendFederation

static BackendFederation *instance;

+(BackendFederation*) instance
{
    static dispatch_once_t onceToken;
    dispatch_once(&onceToken, ^{
        instance = [[BackendFederation alloc] init];
    });
    return instance;
}

-(id)init
{
    self = [super init];
    if (self)
        NSLog(@"BackendGoogleAuth was created");
    return self;
}

- (BOOL)application:(UIApplication *)app
            openURL:(NSURL *)url
            options:(NSDictionary<UIApplicationOpenURLOptionsKey,id> *)options {
  BOOL handled;

  handled = [GIDSignIn.sharedInstance handleURL:url];
  if (handled) {
    return YES;
  }

  // Handle other custom URL types.

  // If not handled by this app, return NO.
  return NO;
}

-(void)startGoogleLogin
{
    [GIDSignIn.sharedInstance signInWithPresentingViewController:UnityGetGLViewController() completion:^(GIDSignInResult * _Nullable signInResult, NSError * _Nullable error) {
            if (error) {
                self.backendGoogleLoginHandler(false, [self charToString:error.localizedDescription], "");
                return;
            }

            if (signInResult == nil) {
                self.backendGoogleLoginHandler(false, "signInResult is nil", "");
                return;
            }

            [signInResult.user refreshTokensIfNeededWithCompletion:^(GIDGoogleUser * _Nullable user, NSError * _Nullable error) {
                if (error) {
                    self.backendGoogleLoginHandler(false, [self charToString:error.localizedDescription], "");
                    return;
                }
                if (user == nil) {
                    self.backendGoogleLoginHandler(false, "user is nil", "");
                    return;
                }

                NSString *idToken = user.idToken.tokenString;
                self.backendGoogleLoginHandler(true, "", [self charToString:idToken]);
            }];
        }
    ];
}


-(char *) charToString:(NSString *)string
{
    const char* stringUTF8 = [string UTF8String];
    char* stringChar = (char*)malloc(strlen(stringUTF8) + 1);
    strcpy(stringChar, stringUTF8);

    return stringChar;
}

-(void)logoutGoogle {
    try {
        [[GIDSignIn sharedInstance] signOut];
        self.backendGoogleSignOutHandler(true, "");
    }
     catch (NSException *exception) {
        NSLog(@"An error occurred while signing out: %@", exception.reason);
        self.backendGoogleSignOutHandler(false, [self charToString:exception.reason]);
    }
}

extern "C"
{
    void StartGoogleLogin(backendgoogleDelegate handler)
    {
        [BackendFederation instance].backendGoogleLoginHandler = handler;
        [[BackendFederation instance] startGoogleLogin];
    }

    void LogoutGoogle(backendgoogleSignOutDelegate handler)
    {
        [BackendFederation instance].backendGoogleSignOutHandler = handler;
        [[BackendFederation instance] logoutGoogle];
    }
}
@end
