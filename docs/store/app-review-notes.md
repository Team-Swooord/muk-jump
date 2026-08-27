# 먹점프 스토어 심사 메모

## App Review Notes (English)

MukJump is a portrait casual drawing-platform game. The character jumps automatically, and the
player draws temporary one-way platforms with one finger. Platforms can be passed from below and
are solid only while the character is falling onto their top side.

No reviewer account is required. On first launch, the game starts with a guest account and remains
fully playable offline. Google and Sign in with Apple are optional in Main Screen > Settings >
Account and are used only for cloud save and the all-time best-height leaderboard.

Account deletion is available in Main Screen > Settings > Account > Delete Account. The action
requires a second confirmation. It immediately logs the user out, deletes linked local gameplay
data, and requests deletion of the BACKND account and game data. Server-side final deletion can
take up to one hour. For Sign in with Apple accounts, the app reauthenticates and revokes the Apple
authorization before requesting account deletion.

The Android 1.0 build offers guest and Google sign-in. Apple sign-in is available in the iOS build,
where the authorization-revocation deletion flow can be completed.

The lobby may show one top adaptive banner. At game over, the player may voluntarily watch one
rewarded ad per run to revive once, or return to the lobby without watching an ad. There are no
startup ads, forced interstitial ads, purchases, subscriptions, loot boxes, chat, or user-generated
content.

Version 1.0 requests non-personalized ads only and does not request App Tracking Transparency
permission. Where required, Google UMP consent is presented before any ad request and can be
reopened from Settings > Play & Ads > Ad Privacy.

Support: cysbandcs@gmail.com

## 심사 재현 순서

1. 앱 실행 후 게스트 상태에서 `시작`을 누른다.
2. 손가락으로 선을 그려 자동 점프하는 먹방울을 받친다.
3. 모든 먹방울이 추락하면 결과 화면에서 `광고 보고 부활` 또는 `로비로`를 확인한다.
4. 로비 `설정 → 계정`에서 로그인·로그아웃·2단계 탈퇴를 확인한다.
5. 로비 `설정 → 플레이·광고 → 광고 개인정보`에서 UMP 선택 화면 재진입을 확인한다.
