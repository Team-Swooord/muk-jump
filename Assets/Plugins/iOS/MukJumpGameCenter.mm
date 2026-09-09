#import <GameKit/GameKit.h>
#import <UIKit/UIKit.h>

extern "C" void UnitySendMessage(const char *, const char *, const char *);
extern "C" UIWindow *UnityGetMainWindow(void);

// 모든 GameKit 상태와 Unity 콜백은 메인 큐에서 다룬다.
static NSString *MJRunOwner;
static NSString *MJAuthenticatedOwner;
static NSString *MJLeaderboard;
static int MJRequest;
static bool MJAuthInstalled;
static bool MJWantLoad;
static bool MJSubmitting;

static void MJRetryScore(NSString *identifier)
{
    if (MJSubmitting || !GKLocalPlayer.localPlayer.isAuthenticated || !identifier.length) return;
    NSString *owner = [GKLocalPlayer.localPlayer.gamePlayerID copy];
    NSString *key = [NSString stringWithFormat:@"MukJump.GameCenter.Pending.%@.%@", identifier, owner];
    NSNumber *pending = [NSUserDefaults.standardUserDefaults objectForKey:key];
    if (![pending isKindOfClass:NSNumber.class] || pending.integerValue < 0) return;
    MJSubmitting = true;
    [GKLeaderboard submitScore:pending.integerValue context:0 player:GKLocalPlayer.localPlayer
        leaderboardIDs:@[identifier] completionHandler:^(NSError *error) {
            dispatch_async(dispatch_get_main_queue(), ^{
                MJSubmitting = false;
                NSNumber *current = [NSUserDefaults.standardUserDefaults objectForKey:key];
                if (!error && [current isKindOfClass:NSNumber.class] && current.integerValue <= pending.integerValue)
                    [NSUserDefaults.standardUserDefaults removeObjectForKey:key];
                // 더 높은 기록/다른 계정은 해당 소유자로 다음 열기·플레이 때 다시 시도한다.
            });
        }];
}

static void MJReply(int request, NSString *error, NSArray *rows)
{
    NSDictionary *payload = @{@"request": @(request), @"error": error ?: @"", @"rows": rows ?: @[]};
    NSData *data = [NSJSONSerialization dataWithJSONObject:payload options:0 error:nil];
    NSString *json = [[NSString alloc] initWithData:data encoding:NSUTF8StringEncoding];
    if (json) UnitySendMessage("MukJumpAppleGameCenterRuntime", "ReceiveLeaderboard", json.UTF8String);
}

static bool MJSameOwner(NSString *owner)
{
    return owner.length > 0 && GKLocalPlayer.localPlayer.isAuthenticated &&
        [owner isEqualToString:GKLocalPlayer.localPlayer.gamePlayerID];
}

static void MJLoadEntries(void)
{
    const int request = MJRequest;
    NSString *owner = [GKLocalPlayer.localPlayer.gamePlayerID copy];
    NSString *identifier = [MJLeaderboard copy];
    MJWantLoad = false;
    MJRetryScore(identifier);
    [GKLeaderboard loadLeaderboardsWithIDs:@[identifier] completionHandler:^(NSArray<GKLeaderboard *> *boards, NSError *error) {
        dispatch_async(dispatch_get_main_queue(), ^{
            if (request != MJRequest || !MJSameOwner(owner)) return;
            if (error || boards.count == 0) { MJReply(request, @"load", nil); return; }
            [boards.firstObject loadEntriesForPlayerScope:GKLeaderboardPlayerScopeGlobal
                timeScope:GKLeaderboardTimeScopeAllTime range:NSMakeRange(1, 10)
                completionHandler:^(GKLeaderboardEntry *local, NSArray<GKLeaderboardEntry *> *items, NSInteger total, NSError *loadError) {
                    dispatch_async(dispatch_get_main_queue(), ^{
                        if (request != MJRequest || !MJSameOwner(owner)) return;
                        if (loadError) { MJReply(request, @"load", nil); return; }
                        NSMutableArray *rows = [NSMutableArray array];
                        for (GKLeaderboardEntry *entry in items) {
                            [rows addObject:@{@"rank": @(MIN(INT_MAX, MAX(1, entry.rank))),
                                @"height": @(MIN(INT_MAX, MAX(0, entry.score))),
                                @"name": entry.player.displayName ?: @""}];
                        }
                        MJReply(request, nil, rows);
                    });
                }];
        });
    }];
}

extern "C" void MukJumpGameCenterLoad(const char *leaderboard, int request)
{
    NSString *identifier = leaderboard ? [NSString stringWithUTF8String:leaderboard] : @"";
    dispatch_async(dispatch_get_main_queue(), ^{
        MJLeaderboard = identifier;
        MJRequest = request;
        MJWantLoad = true;
        if (identifier.length == 0) { MJWantLoad = false; MJReply(request, @"configuration", nil); return; }
        if (MJAuthInstalled) {
            if (GKLocalPlayer.localPlayer.isAuthenticated) MJLoadEntries();
            else { MJWantLoad = false; MJReply(request, @"auth", nil); }
            return;
        }
        MJAuthInstalled = true;
        GKLocalPlayer.localPlayer.authenticateHandler = ^(UIViewController *controller, NSError *error) {
            dispatch_async(dispatch_get_main_queue(), ^{
                if (controller) {
                    UIViewController *root = UnityGetMainWindow().rootViewController;
                    if (root && !root.presentedViewController)
                        [root presentViewController:controller animated:YES completion:nil];
                    else { MJWantLoad = false; MJReply(MJRequest, @"auth", nil); }
                    return;
                }
                NSString *owner = GKLocalPlayer.localPlayer.isAuthenticated ? GKLocalPlayer.localPlayer.gamePlayerID : @"";
                if (MJAuthenticatedOwner.length > 0 && ![MJAuthenticatedOwner isEqualToString:owner]) {
                    MJRunOwner = nil;
                    MJWantLoad = false;
                    UnitySendMessage("MukJumpAppleGameCenterRuntime", "AccountChanged", "");
                }
                MJAuthenticatedOwner = [owner copy];
                if (!MJWantLoad) return;
                if (!owner.length || error) { MJWantLoad = false; MJReply(MJRequest, @"auth", nil); return; }
                MJLoadEntries();
            });
        };
    });
}

extern "C" void MukJumpGameCenterBeginRun(void)
{
    dispatch_async(dispatch_get_main_queue(), ^{
        MJRunOwner = GKLocalPlayer.localPlayer.isAuthenticated ? [GKLocalPlayer.localPlayer.gamePlayerID copy] : nil;
    });
}

extern "C" void MukJumpGameCenterSubmit(const char *leaderboard, int height)
{
    NSString *identifier = leaderboard ? [NSString stringWithUTF8String:leaderboard] : @"";
    dispatch_async(dispatch_get_main_queue(), ^{
        if (!identifier.length || height < 0 || !MJSameOwner(MJRunOwner)) return;
        NSString *key = [NSString stringWithFormat:@"MukJump.GameCenter.Pending.%@.%@", identifier, MJRunOwner];
        NSInteger pending = [NSUserDefaults.standardUserDefaults integerForKey:key];
        [NSUserDefaults.standardUserDefaults setInteger:MAX(pending, height) forKey:key];
        MJRetryScore(identifier);
    });
}
