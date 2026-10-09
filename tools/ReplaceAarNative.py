"""Replace only the Android arm64 Cardboard native library in an AAR."""

import os
import pathlib
import sys
import zipfile


def main() -> None:
    source, library, output = map(pathlib.Path, sys.argv[1:4])
    payload = library.read_bytes()
    if not payload.startswith(b"\x7fELF"):
        raise ValueError(f"Not an ELF library: {library}")
    member = "jni/arm64-v8a/libGfxPluginCardboard.so"
    output.parent.mkdir(parents=True, exist_ok=True)
    temporary = output.with_suffix(output.suffix + ".tmp")
    with zipfile.ZipFile(source) as original, zipfile.ZipFile(temporary, "w") as patched:
        if member not in original.namelist():
            raise ValueError(f"AAR does not contain {member}")
        for info in original.infolist():
            patched.writestr(info, payload if info.filename == member else original.read(info))
    with zipfile.ZipFile(temporary) as check:
        if check.read(member) != payload or check.testzip() is not None:
            raise ValueError("AAR verification failed")
    if output.exists() and output.read_bytes() == temporary.read_bytes():
        temporary.unlink()
    else:
        os.replace(temporary, output)
    print(output)


if __name__ == "__main__":
    main()
