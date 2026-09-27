#!/usr/bin/env python3
"""빌드 43의 고정 소스를 내보내고 승인된 설정창 수정만 적용한다."""
import argparse
import difflib
import hashlib
import json
from pathlib import Path
import subprocess
import tarfile
import re
import shutil

BASE = "26c627f86167373baabb9e0dcce392814b85d42a"
REPO = Path(__file__).resolve().parents[2]
RELEASE = REPO / "output/release-appstore/2026-09-27-build-44"
SOURCE = RELEASE / "source"
TARGET = "Assets/Scripts/Core/LobbyOptionsView.cs"


def sha(data):
    return hashlib.sha256(data).hexdigest()


def original(path):
    return subprocess.check_output(["git", "show", f"{BASE}:{path}"], cwd=REPO)


def fixed_source():
    text = original(TARGET).decode()
    before = "            float height = UsesTossSettings ? SettingsDesignHeight :"
    after = ("            // iOS 구매 행이 있는 경우 약관과 상태 문구가 들어갈 높이를 유지한다.\n"
             "            bool hasPremiumRow = optionsGroup.transform.Find(\"PremiumButton\") != null;\n"
             "            float height = UsesTossSettings ? SettingsDesignHeight :\n"
             "                (hasPremiumRow ? SettingsDesignHeight : SettingsPanelHeight) +\n"
             "                (hasStatus ? SettingsStatusHeight : 0f);")
    assert text.count(before) == 1
    text = text.replace(before + "\n                SettingsPanelHeight + (hasStatus ? SettingsStatusHeight : 0f);", after)
    before = "                new Vector2(0, -527), new Vector2(700, 70),"
    after = "                new Vector2(0, -527), new Vector2(700, InkPremiumAppearance.IsSupported ? SettingsStatusHeight : 70),"
    assert text.count(before) == 1
    text = text.replace(before, after)
    before = "                    UsesTossSettings ? -527f : -381f - footerLift);"
    after = "                    UsesTossSettings || hasPremiumRow ? -527f : -381f - footerLift);"
    assert text.count(before) == 1
    return text.replace(before, after).encode()


def export():
    if SOURCE.exists():
        raise SystemExit(f"기존 입력은 덮어쓰지 않습니다: {SOURCE}")
    RELEASE.mkdir(parents=True, exist_ok=True)
    archive = RELEASE / "source-43.tar"
    subprocess.run(["git", "archive", "--format=tar", f"--output={archive}", BASE], cwd=REPO, check=True)
    SOURCE.mkdir()
    with tarfile.open(archive) as contents:
        contents.extractall(SOURCE, filter="data")
    files = {str(p.relative_to(SOURCE)): sha(p.read_bytes())
             for p in SOURCE.rglob("*") if p.is_file()}
    manifest = {"baseCommit": BASE, "mainCommitAtExport": subprocess.check_output(
        ["git", "rev-parse", "HEAD"], cwd=REPO, text=True).strip(),
        "source": str(SOURCE), "archiveSha256": sha(archive.read_bytes()),
        "files": files, "allowedRuntimeChange": TARGET,
        "version": "1.0.3", "newBuild": "44"}
    (RELEASE / "source-baseline.json").write_text(json.dumps(manifest, indent=2))
    print(json.dumps({"source": str(SOURCE), "base": BASE, "files": len(files)}))


def complete_export():
    """초기 Unity 입력 외에 사전 검사에서 요구하는 문서·루트 빌드 입력도 같은 커밋에서 보충한다."""
    path = RELEASE / "source-baseline.json"
    baseline = json.loads(path.read_text())
    archive = RELEASE / "source-43-complete.tar"
    assert not archive.exists()
    subprocess.run(["git", "archive", "--format=tar", f"--output={archive}", BASE], cwd=REPO, check=True)
    with tarfile.open(archive) as contents:
        for member in contents:
            name = member.name.rstrip("/")
            if name in baseline["files"] or member.isdir():
                continue
            if member.issym():
                contents.extract(member, SOURCE, filter="data")
                continue
            assert member.isfile(), name
            data = contents.extractfile(member).read()
            dest = SOURCE / name
            if dest.exists():
                assert dest.read_bytes() == data, f"보충 입력과 자동 생성 파일이 다릅니다: {name}"
            else:
                dest.parent.mkdir(parents=True, exist_ok=True)
                dest.write_bytes(data)
            baseline["files"][name] = sha(data)
    baseline["completeArchiveSha256"] = sha(archive.read_bytes())
    path.write_text(json.dumps(baseline, indent=2))
    print("complete tracked source files", len(baseline["files"]))


def patch():
    dest = SOURCE / TARGET
    assert dest.read_bytes() == original(TARGET), "원본 43에서만 적용할 수 있습니다."
    data = fixed_source()
    dest.write_bytes(data)
    diff = "".join(difflib.unified_diff(original(TARGET).decode().splitlines(True),
                    data.decode().splitlines(True), fromfile="a/" + TARGET, tofile="b/" + TARGET))
    (RELEASE / "settings-layout.patch").write_text(diff)
    print(diff)


def provision_local():
    """Git 제외 연결 설정은 값 노출 없이 기존 Archive의 앱과 일치하는지 확인한다."""
    name = "Assets/TheBackend/Resources/TheBackendSettings.asset"
    data = (REPO / name).read_bytes()
    archive = Path.home() / "Library/Developer/Xcode/Archives/2026-09-25/MukJump-1.0.3-43.xcarchive/Products/Applications/ProductName.app/Data"
    values = {key: re.search(rb"^  " + key.encode() + rb":\s*(.*)$", data, re.M).group(1).strip().strip(b'"')
              for key in ["clientAppID", "signatureKey"]}
    matched = {key: False for key in values}
    for path in archive.rglob("*.assets"):
        asset = path.read_bytes()
        for key, value in values.items():
            matched[key] |= bool(value) and value in asset
    assert all(matched.values()), "연결 설정이 기존 Archive 43과 일치하지 않습니다."
    entries = {}
    for relative in [name, name + ".meta"]:
        source, dest = REPO / relative, SOURCE / relative
        assert not dest.exists(), "기존 입력을 덮어쓰지 않습니다."
        dest.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(source, dest)
        entries[relative] = sha(source.read_bytes())
    (RELEASE / "local-build-inputs.json").write_text(json.dumps({"files": entries,
        "connectionValuesMatchArchive43": matched, "valuesDisclosed": False}, indent=2))
    print("기존 Archive 43과 일치하는 로컬 연결 설정을 복사했습니다.")


def verify():
    baseline = json.loads((RELEASE / "source-baseline.json").read_text())
    changed, missing = {}, []
    for name, digest in baseline["files"].items():
        path = SOURCE / name
        if not path.exists():
            missing.append(name)
        elif sha(path.read_bytes()) != digest:
            changed[name] = {"before": digest, "after": sha(path.read_bytes())}
    added = sorted(str(p.relative_to(SOURCE)) for directory in ["Assets", "Packages", "ProjectSettings"]
                   for p in (SOURCE / directory).rglob("*")
                   if p.is_file() and str(p.relative_to(SOURCE)) not in baseline["files"])
    result = {"baseCommit": BASE, "changed": changed, "missing": missing, "added": added,
              "runtimePatchMatches": (SOURCE / TARGET).read_bytes() == fixed_source()}
    generated = {"Assets/Scenes/Main.unity", "Assets/Scenes/Splash.unity", "Assets/Scenes/SkinUnlock.unity",
                 "ProjectSettings/MukJumpSceneAttestation.json", "Assets/UniversalRenderPipelineGlobalSettings.asset"}
    allowed_added = {"Assets/Editor/HotfixEditorPipeline.asset", "Assets/Editor/HotfixEditorPipeline.asset.meta",
                     "Assets/StreamingAssets/google-services-desktop.json", "Assets/StreamingAssets/google-services-desktop.json.meta"}
    for name in ["SettingsPremiumLayoutTests.cs", "SettingsPremiumPlayTests.cs"]:
        allowed_added.update({"Assets/Editor/Tests/" + name, "Assets/Editor/Tests/" + name + ".meta"})
    private_manifest = RELEASE / "local-build-inputs.json"
    if private_manifest.exists():
        local = json.loads(private_manifest.read_text())
        for name, digest in local["files"].items():
            assert sha((SOURCE / name).read_bytes()) == digest
            allowed_added.add(name)
    unexpected = sorted(set(changed) - generated - {TARGET, "ProjectSettings/ProjectSettings.asset"})
    unexpected += sorted(set(added) - allowed_added)
    result["unexpectedPaths"] = unexpected
    settings = "ProjectSettings/ProjectSettings.asset"
    result["onlyBuildNumberChangedInPlayerSettings"] = (SOURCE / settings).read_bytes() == original(settings).replace(b"    iPhone: 43\n", b"    iPhone: 44\n")
    result["generatedFirebaseMatches43"] = (SOURCE / "Assets/StreamingAssets/google-services-desktop.json").read_bytes() == (REPO / "output/ios/MukJump/Data/Raw/google-services-desktop.json").read_bytes()
    urp = "Assets/UniversalRenderPipelineGlobalSettings.asset"
    def without_generated_runtime_list(data):
        return re.sub(rb"(    m_RuntimeSettings:\n)      m_List:(?: \[\])?\n(?:      - rid: [^\n]*\n)*",
                      rb"\1      m_List: []\n", data)
    result["onlyGeneratedUrpRuntimeListChanged"] = without_generated_runtime_list((SOURCE / urp).read_bytes()) == without_generated_runtime_list(original(urp))
    result["allOtherTrackedFilesMatch43"] = not unexpected and not missing
    (RELEASE / "source-diff.json").write_text(json.dumps(result, indent=2))
    print(json.dumps(result, indent=2))
    assert result["runtimePatchMatches"] and result["allOtherTrackedFilesMatch43"]
    assert result["onlyBuildNumberChangedInPlayerSettings"] and result["generatedFirebaseMatches43"]
    assert result["onlyGeneratedUrpRuntimeListChanged"]


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("action", choices=["export", "complete-export", "patch", "provision-local", "verify"])
    args = parser.parse_args()
    {"export": export, "complete-export": complete_export, "patch": patch,
     "provision-local": provision_local, "verify": verify}[args.action]()
