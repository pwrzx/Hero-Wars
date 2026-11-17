"""Inspect an exported Unity Xcode project. Does not compile or sign an iOS app."""
import argparse
import json
import plistlib
import struct
import sys
from pathlib import Path


def inspect(root):
    errors = []

    def check(condition, message):
        if not condition:
            errors.append(message)

    info = plistlib.loads((root / "Info.plist").read_bytes())
    project = (root / "Unity-iPhone.xcodeproj/project.pbxproj").read_text(encoding="utf-8")
    check(info.get("CFBundleDisplayName") == "Hero Wars", "Incorrect display name")
    check(info.get("CFBundleShortVersionString") == "1.0.0", "Incorrect release version")
    check(info.get("UISupportedInterfaceOrientations") == ["UIInterfaceOrientationPortrait"], "Portrait orientation is not configured")
    check("metal" in info.get("UIRequiredDeviceCapabilities", []), "Metal capability is missing")
    check("PRODUCT_BUNDLE_IDENTIFIER = com.herowars.towertrials;" in project, "App bundle identifier is missing")
    check("IPHONEOS_DEPLOYMENT_TARGET = 15.0;" in project, "iOS deployment target is missing")
    check('TARGETED_DEVICE_FAMILY = "1,2";' in project, "iPhone/iPad support is missing")
    check((root / "Libraries/Plugins/iOS/HeroWarsHaptics.mm").is_file(), "Native haptics source is missing")
    check("HeroWarsHaptics.mm in Sources" in project, "Native haptics is not in the Xcode sources phase")
    generated = root / "Il2CppOutputProject/Source/il2cppOutput/TowerTrials.Runtime.cpp"
    check(generated.is_file() and "PlayImpact" in generated.read_text(encoding="utf-8"), "Haptics P/Invoke was not generated")

    privacy = {}
    manifests = sorted(root.rglob("PrivacyInfo.xcprivacy"))
    for path in manifests:
        document = plistlib.loads(path.read_bytes())
        for entry in document.get("NSPrivacyAccessedAPITypes", []):
            privacy.setdefault(entry["NSPrivacyAccessedAPIType"], set()).update(entry["NSPrivacyAccessedAPITypeReasons"])
    check("CA92.1" in privacy.get("NSPrivacyAccessedAPICategoryUserDefaults", set()), "Local PlayerPrefs privacy reason is missing")

    icons = root / "Unity-iPhone/Images.xcassets/AppIcon.appiconset"
    entries = json.loads((icons / "Contents.json").read_text(encoding="utf-8"))["images"]
    check(all((icons / item.get("filename", "missing")).is_file() for item in entries), "An app icon size is missing")
    marketing = icons / "Icon-Store-1024.png"
    png = marketing.read_bytes()
    check(png[:8] == b"\x89PNG\r\n\x1a\n" and struct.unpack_from(">II", png, 16) == (1024, 1024), "Store icon is not a 1024px PNG")
    check(png[25] in (0, 2), "Store icon has an alpha channel")
    check((root / "Data/level0").is_file() and (root / "Data/level1").is_file() and not (root / "Data/level2").exists(), "Expected exactly two shipping scenes")
    metadata = list((root / "Data").rglob("global-metadata.dat"))
    check(bool(metadata), "IL2CPP metadata is missing")
    check(not any(b"RuntimePipelineDriver" in path.read_bytes() for path in metadata), "Development runtime server is present")
    return {"export": str(root), "display_name": info.get("CFBundleDisplayName"),
            "version": info.get("CFBundleShortVersionString"), "icon_sizes": len(entries),
            "privacy_manifests": [str(path.relative_to(root)) for path in manifests],
            "privacy_api_reasons": {key: sorted(value) for key, value in privacy.items()},
            "xcode_compilation_performed": False, "device_installation_performed": False,
            "errors": errors, "passed": not errors}


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("export", type=Path)
    parser.add_argument("--report", type=Path)
    args = parser.parse_args()
    result = inspect(args.export.resolve())
    output = json.dumps(result, indent=2)
    if args.report:
        args.report.write_text(output + "\n", encoding="utf-8")
    print(output)
    sys.exit(0 if result["passed"] else 1)
