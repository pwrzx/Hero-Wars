"""Read-only APK/AAB checks for ARM64 libraries and Android 16KB page support."""
import argparse
import hashlib
import json
import struct
import sys
import zipfile
from pathlib import Path


def inspect(path):
    libraries = []
    errors = []
    with zipfile.ZipFile(path) as archive, path.open("rb") as raw:
        for entry in archive.infolist():
            if not entry.filename.endswith(".so"):
                continue
            data = archive.read(entry)
            if data[:6] != b"\x7fELF\x02\x01":
                errors.append(f"Not a little-endian 64-bit ELF: {entry.filename}")
                continue
            machine = struct.unpack_from("<H", data, 18)[0]
            offset = struct.unpack_from("<Q", data, 32)[0]
            stride, count = struct.unpack_from("<HH", data, 54)
            loads = []
            for i in range(count):
                header = offset + i * stride
                if struct.unpack_from("<I", data, header)[0] != 1:
                    continue
                file_offset, address = struct.unpack_from("<QQ", data, header + 8)
                alignment = struct.unpack_from("<Q", data, header + 48)[0]
                loads.append(alignment)
                if alignment < 16384 or (file_offset - address) % 16384:
                    errors.append(f"LOAD segment is not 16KB compatible: {entry.filename}")
            if not loads:
                errors.append(f"Native library has no LOAD segments: {entry.filename}")
            raw.seek(entry.header_offset)
            header = raw.read(30)
            name_size, extra_size = struct.unpack_from("<HH", header, 26)
            zip_data_offset = entry.header_offset + 30 + name_size + extra_size
            aligned_zip = zip_data_offset % 16384 == 0
            if path.suffix == ".apk" and entry.compress_type == zipfile.ZIP_STORED and not aligned_zip:
                errors.append(f"Uncompressed native library is not 16KB ZIP aligned: {entry.filename}")
            if machine != 183 or "arm64-v8a/" not in entry.filename:
                errors.append(f"Unexpected native architecture: {entry.filename}")
            libraries.append({"name": entry.filename, "machine": machine,
                              "minimum_load_alignment": min(loads) if loads else None,
                              "compressed": entry.compress_type != zipfile.ZIP_STORED,
                              "zip_16kb_aligned": aligned_zip})
        metadata = [entry for entry in archive.namelist() if entry.endswith("global-metadata.dat")]
        if not metadata:
            errors.append("Cannot verify player types: IL2CPP metadata is missing")
        server_present = any(b"RuntimePipelineDriver" in archive.read(entry) for entry in metadata)
        if server_present:
            errors.append("Pipeline runtime server type is present in player metadata")
    if not libraries:
        errors.append("Package contains no native libraries")
    with path.open("rb") as package_data:
        digest = hashlib.file_digest(package_data, "sha256").hexdigest()
    return {"package": path.name, "bytes": path.stat().st_size, "sha256": digest,
            "libraries": libraries, "pipeline_runtime_server_present": server_present,
            "errors": errors, "passed": not errors}


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("package", type=Path)
    parser.add_argument("--report", type=Path)
    args = parser.parse_args()
    result = inspect(args.package.resolve())
    text = json.dumps(result, indent=2)
    if args.report:
        args.report.write_text(text + "\n", encoding="utf-8")
    print(text)
    sys.exit(0 if result["passed"] else 1)
