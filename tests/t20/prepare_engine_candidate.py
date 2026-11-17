"""Prepare and byte-verify a local engine-only ZIP; never publish it.

The paired application/source candidate must already have passed T09 verification.
An approved release tag, URLs and releaseSequence are deliberately NOT invented.
"""
import argparse
import hashlib
import json
from pathlib import Path
import re
import zipfile


def digest(path):
    h = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            h.update(block)
    return h.hexdigest()


def require(condition, message):
    if not condition:
        raise ValueError(message)


parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument("--runtime", type=Path, required=True)
parser.add_argument("--sidecar", type=Path, required=True)
parser.add_argument("--package", type=Path, required=True)
parser.add_argument("--output", type=Path, required=True)
args = parser.parse_args()

runtime = args.runtime.resolve(strict=True)
manifest_path = runtime / "manifest.json"
manifest = json.loads(manifest_path.read_text(encoding="utf-8-sig"))
require(manifest.get("schemaVersion") == 1 and manifest.get("reportProfile") == "phpcs4-json-v1",
        "Unexpected verified pack contract")
require((runtime / "verification/results.json").is_file(), "Verified pack results are missing")
pair = json.loads((args.package / "evidence/paired-candidate.json").read_text(encoding="utf-8"))
require(pair.get("scopePassed") is True and pair.get("allSourceMembersVerified") is True
        and pair.get("candidateForUserTesting") is True and pair.get("publicReleaseApproved") is False,
        "The app and source companion are not an approved local test pair")
binary = args.package / pair["binaryArchive"]
source = args.sidecar / pair["sourceArchive"]
require(binary.is_file() and digest(binary) == pair["binaryArchiveSha256"], "Paired app ZIP checksum mismatch")
require(source.is_file() and digest(source) == pair["sourceArchiveSha256"], "Paired source ZIP checksum mismatch")

target_versions = [row["version"] for row in manifest["targets"]]
require(target_versions == ["7.4", "8.0", "8.1", "8.2", "8.3", "8.4", "8.5"],
        "T20 target list differs from the Windows-verified PHP 7.4–8.5 pack")
member_names = []
for file in sorted(runtime.rglob("*")):
    if file.is_dir():
        continue
    require(file.is_file() and not file.is_symlink(), "Unsupported engine pack entry: " + str(file))
    relative = file.relative_to(runtime).as_posix()
    require(all(re.fullmatch(r"[A-Za-z0-9][A-Za-z0-9._-]*", part)
                and part.upper().split(".", 1)[0] not in
                {"CON", "PRN", "AUX", "NUL", *("COM" + str(i) for i in range(10)),
                 *("LPT" + str(i) for i in range(10))}
                for part in relative.split("/")), "Unsupported ZIP entry: " + relative)
    member_names.append((file, "runtime/" + relative))
require(any(name == "runtime/manifest.json" for _, name in member_names),
        "Engine pack manifest not included")
require(not args.output.exists(), "Preserve previous engine candidate; use a new output directory")
args.output.mkdir(parents=True)

archive = args.output / "DK-PHP-engine-unpublished.zip"
with zipfile.ZipFile(archive, "w", compression=zipfile.ZIP_DEFLATED, compresslevel=6) as target:
    for path, name in member_names:
        entry = zipfile.ZipInfo(name, (1980, 1, 1, 0, 0, 0))
        entry.create_system = 0
        entry.external_attr = 0x20
        entry.compress_type = zipfile.ZIP_DEFLATED
        target.writestr(entry, path.read_bytes(), compress_type=zipfile.ZIP_DEFLATED, compresslevel=6)
with zipfile.ZipFile(archive) as check:
    require(check.namelist() == [name for _, name in member_names], "Engine ZIP entry mismatch")
    for path, name in member_names:
        require(check.read(name) == path.read_bytes(), "Engine ZIP member mismatch: " + name)

metadata = {
    "scope": "T20 local engine-only candidate; not a release or source-clearance decision",
    "scopePassed": True,
    "publicReleaseApproved": False,
    "packId": manifest["packId"],
    "packSchemaVersion": 1,
    "reportProfile": manifest["reportProfile"],
    "targets": target_versions,
    "engineVersions": manifest["engine"]["versions"],
    "phpRuntimeVersion": manifest["runtime"]["version"],
    "archive": archive.name,
    "archiveBytes": archive.stat().st_size,
    "archiveSha256": digest(archive),
    "manifestSha256": digest(manifest_path),
    "fileCount": len(member_names),
    "pairedAppSha256": pair["binaryArchiveSha256"],
    "sourceArchive": source.name,
    "sourceArchiveSha256": pair["sourceArchiveSha256"],
    "catalogReady": False,
    "pending": [
        "Separate authorization for a GitHub Release/tag and persistent matching-source publication",
        "An approved releaseSequence and exact immutable Release asset URLs",
        "Actual anonymous HTTPS download, install, restart and restore on Windows 11",
        "Final redistribution compliance and unchanged-EXE pack-only update verification"
    ]
}
(args.output / "engine-candidate.json").write_text(
    json.dumps(metadata, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
print("T20_ENGINE_CANDIDATE_JSON=" + json.dumps(
    {k: metadata[k] for k in ("scopePassed", "packId", "archiveBytes", "archiveSha256",
                              "manifestSha256", "fileCount", "sourceArchiveSha256")}))
