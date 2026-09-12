#import <GameKit/GameKit.h>
#import <UIKit/UIKit.h>
extern "C" void UnitySendMessage(const char *, const char *, const char *);
extern "C" UIWindow *UnityGetMainWindow(void);
static NSString *receiver;
static UIViewController *pendingController;
static NSString *lastPlayer;
static NSString *Player() { return GKLocalPlayer.localPlayer.authenticated ? GKLocalPlayer.localPlayer.gamePlayerID : @""; }
static void Reply(NSDictionary *value)
{
    NSData *json = [NSJSONSerialization dataWithJSONObject:value options:0 error:nil];
    if (receiver && json) UnitySendMessage(receiver.UTF8String, "OnAchievementReply", [[NSString alloc] initWithData:json encoding:NSUTF8StringEncoding].UTF8String);
}
static void AuthState()
{
    NSString *player = Player();
    if ([lastPlayer isEqualToString:player]) return;
    lastPlayer = [player copy];
    Reply(@{@"kind":@"auth", @"player":player, @"success":@([player length] > 0)});
}
extern "C" void MJ_AchInit(const char *target)
{
    receiver = [NSString stringWithUTF8String:target];
    GKLocalPlayer.localPlayer.authenticateHandler = ^(UIViewController *controller, NSError *error) {
        dispatch_async(dispatch_get_main_queue(), ^{ pendingController = controller; AuthState(); });
    };
}
extern "C" void MJ_AchTick(bool allowUi)
{
    AuthState();
    if (!allowUi || !pendingController || UIApplication.sharedApplication.applicationState != UIApplicationStateActive) return;
    UIViewController *root = UnityGetMainWindow().rootViewController;
    if (!root || root.presentedViewController) return;
    UIViewController *controller = pendingController; pendingController = nil;
    [root presentViewController:controller animated:YES completion:nil];
}
static void Complete(NSString *kind, NSString *player, int request, BOOL success, NSArray *items)
{
    dispatch_async(dispatch_get_main_queue(), ^{
        AuthState();
        Reply(@{@"kind":kind, @"player":player, @"request":@(request),
            @"success":@(success && [Player() isEqualToString:player]), @"items":items ?: @[]});
    });
}
extern "C" void MJ_AchLoad(const char *identity, int request)
{
    NSString *player = [NSString stringWithUTF8String:identity];
    if (![Player() isEqualToString:player] || !player.length) { Complete(@"load", player, request, NO, nil); return; }
    [GKAchievement loadAchievementsWithCompletionHandler:^(NSArray<GKAchievement *> *achievements, NSError *error) {
        NSMutableArray *items = [NSMutableArray array];
        for (GKAchievement *a in achievements) [items addObject:@{@"id":a.identifier, @"percent":@(a.percentComplete)}];
        Complete(@"load", player, request, error == nil, items);
    }];
}
extern "C" void MJ_AchReport(const char *identity, int request, const char *payload)
{
    NSString *player = [NSString stringWithUTF8String:identity];
    if (![Player() isEqualToString:player] || !player.length) { Complete(@"report", player, request, NO, nil); return; }
    NSDictionary *data = [NSJSONSerialization JSONObjectWithData:[[NSString stringWithUTF8String:payload] dataUsingEncoding:NSUTF8StringEncoding] options:0 error:nil];
    NSMutableArray *batch = [NSMutableArray array];
    for (NSDictionary *item in data[@"items"]) {
        if (![item[@"id"] isKindOfClass:NSString.class] || ![item[@"id"] hasPrefix:@"mukjump.achievement."]) continue;
        GKAchievement *a = [[GKAchievement alloc] initWithIdentifier:item[@"id"]];
        a.percentComplete = MIN(100, MAX(0, [item[@"percent"] doubleValue]));
        a.showsCompletionBanner = NO;
        [batch addObject:a];
    }
    if (!batch.count) { Complete(@"report", player, request, NO, nil); return; }
    [GKAchievement reportAchievements:batch withCompletionHandler:^(NSError *error) { Complete(@"report", player, request, error == nil, nil); }];
}
