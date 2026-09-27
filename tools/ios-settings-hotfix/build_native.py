#!/usr/bin/env python3
"""승인된 설정창 핫픽스의 네이티브 빌드·서명 검증·업로드 단계."""
import argparse
import hashlib
import json
from pathlib import Path
import plistlib
import shutil
import subprocess
import datetime
import re

REPO = Path(__file__).resolve().parents[2]
ROOT = REPO / "output/release-appstore/2026-09-27-build-44"
PREVIOUS = REPO / "output/release-appstore/2026-09-25-build-43"
ARCHIVE = Path.home() / "Library/Developer/Xcode/Archives/2026-09-27/MukJump-1.0.3-44.xcarchive"


def run(name, args):
    log = ROOT / (name + ".log")
    if log.exists():
        raise RuntimeError(f"기존 실행 로그가 있습니다. 결과를 먼저 확인하세요: {log}")
    started = datetime.datetime.now(datetime.timezone.utc).isoformat()
    print(f"{name}: started", flush=True)
    with log.open("w") as stream:
        result = subprocess.run(args, cwd=REPO, stdout=stream, stderr=subprocess.STDOUT)
    (ROOT / (name + "-result.json")).write_text(json.dumps({"started": started,
        "finished": datetime.datetime.now(datetime.timezone.utc).isoformat(),
        "exitCode": result.returncode, "log": str(log)}, indent=2))
    if result.returncode:
        raise RuntimeError(f"{name} 실패: {log}")
    print(f"{name}: succeeded", flush=True)


def verify(app, distribution=False):
    info = plistlib.loads((app / "Info.plist").read_bytes())
    ent = plistlib.loads(subprocess.check_output(["codesign", "-d", "--entitlements", ":-", str(app)], stderr=subprocess.DEVNULL))
    profile = plistlib.loads(subprocess.check_output(["security", "cms", "-D", "-i", str(app / "embedded.mobileprovision")], stderr=subprocess.DEVNULL))
    assert info["CFBundleShortVersionString"] == "1.0.3"
    assert info["CFBundleVersion"] == "44"
    assert info["CFBundleIdentifier"] == "com.CYSB.MukJump"
    assert info["ITSAppUsesNonExemptEncryption"] is False
    assert info["UIDeviceFamily"] == [1]
    assert {"ko", "en", "ja"} <= set(info["CFBundleLocalizations"])
    assert not info.get("MukJumpPrereleaseReset", False)
    assert ent["com.apple.developer.team-identifier"] == "8AU359WZZ2"
    assert ent["application-identifier"] == "8AU359WZZ2.com.CYSB.MukJump"
    assert ent["com.apple.developer.applesignin"] == ["Default"]
    assert ent["com.apple.developer.game-center"] is True
    assert profile["TeamIdentifier"] == ["8AU359WZZ2"]
    if distribution:
        assert ent.get("get-task-allow") is False
        assert profile["Name"] == "MukJump App Store Connect"
    subprocess.run(["codesign", "--verify", "--deep", "--strict", str(app)], check=True)
    return {"app": str(app), "version": "1.0.3", "build": "44", "bundle": info["CFBundleIdentifier"],
        "encryption": False, "deviceFamily": info["UIDeviceFamily"], "team": ent["com.apple.developer.team-identifier"],
        "appleSignIn": ent["com.apple.developer.applesignin"], "gameCenter": True,
        "getTaskAllow": ent.get("get-task-allow"), "profile": profile["Name"],
        "signature": "deep + strict passed", "distribution": distribution}


def pin_dependencies(xcode, phase):
    # ~> 범위가 광고 SDK를 새 버전으로 올리지 않도록 실제 43의 잠금 파일을 재사용한다.
    lock = REPO / "output/ios/MukJump/Podfile.lock"
    assert lock.is_file()
    first = ROOT / (phase + "-first-resolution.lock")
    if not first.exists(): shutil.copy2(xcode / "Podfile.lock", first)
    shutil.copy2(lock, xcode / "Podfile.lock")
    run(phase + "-pod-install", ["pod", "install", "--project-directory=" + str(xcode)])
    # 같은 podspec이 Specs Git 저장소 대신 CDN으로 조회되는 위치 메타데이터만 허용한다.
    def package_pins(text):
        return re.sub(r"(?ms)^SPEC REPOS:\n.*?(?=^[A-Z][A-Z _-]+:\n)", "", text)
    actual = xcode / "Podfile.lock"
    assert package_pins(actual.read_text()) == package_pins(lock.read_text()), "43과 pod 버전/체크섬이 다릅니다."
    (ROOT / (phase + "-dependency-verification.json")).write_text(json.dumps({
        "allVersionsAndSpecChecksumsMatch43": True,
        "onlySpecRepositoryMetadataMayDiffer": actual.read_bytes() != lock.read_bytes(),
        "originalLockSha256": hashlib.sha256(lock.read_bytes()).hexdigest(),
        "buildLockSha256": hashlib.sha256(actual.read_bytes()).hexdigest()}, indent=2))
    resolved = REPO / "output/ios/MukJump/Unity-iPhone.xcworkspace/xcshareddata/swiftpm/Package.resolved"
    dest = xcode / "Unity-iPhone.xcworkspace/xcshareddata/swiftpm/Package.resolved"
    dest.parent.mkdir(parents=True, exist_ok=True); shutil.copy2(resolved, dest)


def simulator():
    state = json.loads((ROOT / "simulator-export.json").read_text())
    assert state["status"] == "succeeded"
    xcode = ROOT / "Xcode-Simulator-44"
    pin_dependencies(xcode, "simulator")
    run("simulator-native-build", ["xcodebuild", "-workspace", str(xcode / "Unity-iPhone.xcworkspace"),
        "-scheme", "Unity-iPhone", "-configuration", "Release", "-sdk", "iphonesimulator",
        "-destination", "generic/platform=iOS Simulator", "-derivedDataPath", str(ROOT / "DerivedData-Simulator-44"),
        "-disableAutomaticPackageResolution", "-onlyUsePackageVersionsFromResolvedFile",
        "ARCHS=x86_64", "ONLY_ACTIVE_ARCH=YES", "EXCLUDED_ARCHS=arm64",
        "CODE_SIGNING_ALLOWED=NO", "CODE_SIGNING_REQUIRED=NO", "build"])


def device():
    state = json.loads((ROOT / "device-export.json").read_text())
    assert state["status"] == "succeeded"
    assert not ARCHIVE.exists(), "기존 Archive는 덮어쓰지 않습니다."
    xcode = ROOT / "Xcode-AppStore-44"
    pin_dependencies(xcode, "device")
    args = ["xcodebuild", "-workspace", str(xcode / "Unity-iPhone.xcworkspace"), "-scheme", "Unity-iPhone",
        "-configuration", "Release", "-destination", "generic/platform=iOS",
        "-derivedDataPath", str(ROOT / "DerivedData-AppStore-44"), "-allowProvisioningUpdates",
        "-disableAutomaticPackageResolution", "-onlyUsePackageVersionsFromResolvedFile", "DEVELOPMENT_TEAM=8AU359WZZ2"]
    run("device-native-build", args + ["build"])
    run("archive", args + ["-archivePath", str(ARCHIVE), "archive"])
    result = verify(ARCHIVE / "Products/Applications/ProductName.app")
    (ROOT / "archive-verification.json").write_text(json.dumps(result, indent=2))
    shutil.copy2(PREVIOUS / "ExportOptions.plist", ROOT / "ExportOptions.plist")
    run("export", ["xcodebuild", "-exportArchive", "-archivePath", str(ARCHIVE), "-exportPath", str(ROOT / "Export-AppStore-44"),
        "-exportOptionsPlist", str(ROOT / "ExportOptions.plist"), "-allowProvisioningUpdates"])
    ipa = ROOT / "Export-AppStore-44/ProductName.ipa"
    subprocess.run(["ditto", "-x", "-k", str(ipa), str(ROOT / "ipa-verification")], check=True)
    result = verify(ROOT / "ipa-verification/Payload/ProductName.app", True)
    result["ipaSha256"] = hashlib.sha256(ipa.read_bytes()).hexdigest()
    (ROOT / "ipa-verification.json").write_text(json.dumps(result, indent=2))


def upload():
    verified = json.loads((ROOT / "ipa-verification.json").read_text())
    ipa = ROOT / "Export-AppStore-44/ProductName.ipa"
    assert hashlib.sha256(ipa.read_bytes()).hexdigest() == verified["ipaSha256"]
    shutil.copy2(PREVIOUS / "UploadOptions.plist", ROOT / "UploadOptions.plist")
    run("upload", ["xcodebuild", "-exportArchive", "-archivePath", str(ARCHIVE), "-exportPath", str(ROOT / "Upload-AppStore-44"),
        "-exportOptionsPlist", str(ROOT / "UploadOptions.plist"), "-allowProvisioningUpdates"])


if __name__ == "__main__":
    parser = argparse.ArgumentParser(); parser.add_argument("phase", choices=["simulator", "device", "upload"])
    {"simulator": simulator, "device": device, "upload": upload}[parser.parse_args().phase]()
