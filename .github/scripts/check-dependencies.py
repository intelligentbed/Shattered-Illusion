#!/usr/bin/env python3
"""Checks that the mod's dependency declarations agree with each other.

Compares three things that have to be kept in step by hand:
  - build.txt       (modReferences / weakReferences: what tModLoader requires at load)
  - the .csproj     (dll references: what the code compiles against)
  - the Lib folder  (the dll files themselves)

Run from the mod's root folder. Exits 1 if anything disagrees.
"""

import glob
import os
import re
import sys
import xml.etree.ElementTree as ET

SKIP_DIRS = {"bin", "obj", ".git", ".vs", ".github"}

errors = []


def error(path, message):
    errors.append((path, message))


def read_build_txt(path):
    """Returns (strong, weak) mod names from build.txt, without @version suffixes."""
    refs = {"modReferences": [], "weakReferences": []}
    with open(path, encoding="utf-8-sig") as f:
        for line in f:
            key, sep, value = line.partition("=")
            key = key.strip()
            if sep and key in refs:
                refs[key] = [m.split("@")[0].strip() for m in value.split(",") if m.strip()]
    return refs["modReferences"], refs["weakReferences"]


def read_csproj_references(path):
    """Returns {mod name: hint path} for every <Reference> that points at a dll file."""
    found = {}
    for element in ET.parse(path).getroot().iter():
        if element.tag.split("}")[-1] != "Reference":
            continue
        hint = next((c.text for c in element if c.tag.split("}")[-1] == "HintPath"), None)
        if hint:
            name = element.get("Include", "").split(",")[0].strip()
            found[name] = hint.strip().replace("\\", "/")
    return found


def exists_with_exact_case(relative_path):
    """True only if every part of the path matches the name on disk exactly.

    macOS and Windows ignore case, the Linux build machine does not.
    """
    current = "."
    for part in relative_path.split("/"):
        if part in ("", "."):
            continue
        if not os.path.isdir(current) or part not in os.listdir(current):
            return False
        current = os.path.join(current, part)
    return os.path.isfile(current)


def source_files():
    for root, dirs, files in os.walk("."):
        dirs[:] = [d for d in dirs if d not in SKIP_DIRS]
        for name in files:
            if name.endswith(".cs"):
                yield os.path.join(root, name)


def code_uses_namespace(name):
    """True if any .cs file has `using Name` or refers to `Name.Something`.

    A mod's name inside a string, as in TryGetMod("Name"), does not count:
    that is a runtime lookup and needs no dll.
    """
    pattern = re.compile(r"\busing\s+(?:static\s+)?%s\b|(?<![\w\"])%s\s*\." % (re.escape(name), re.escape(name)))
    for path in source_files():
        with open(path, encoding="utf-8-sig", errors="replace") as f:
            if pattern.search(f.read()):
                return True
    return False


def main():
    csprojs = glob.glob("*.csproj")
    if not os.path.isfile("build.txt") or len(csprojs) != 1:
        print("Run this from the mod's root folder (needs build.txt and one .csproj).")
        return 2
    csproj = csprojs[0]

    strong, weak = read_build_txt("build.txt")
    declared = set(strong) | set(weak)
    referenced = read_csproj_references(csproj)

    for mod in strong:
        if mod not in referenced:
            error("build.txt", f"'{mod}' is in modReferences but {csproj} has no reference to its dll.")

    for mod, hint in referenced.items():
        if mod not in declared:
            error(csproj, f"'{mod}' is referenced but is not listed in build.txt. "
                          "Add it to modReferences (required) or weakReferences (optional), or remove the reference.")
        if not exists_with_exact_case(hint):
            error(csproj, f"'{mod}' points at '{hint}', which does not exist with that exact spelling and capitalisation.")
        if not code_uses_namespace(mod):
            error(csproj, f"'{mod}' is referenced but no .cs file uses anything from it. Remove the reference and its dll.")

    # Folder names come from os.listdir so they carry their real capitalisation.
    referenced_files = {hint.lower() for hint in referenced.values()}
    for folder in sorted(d for d in os.listdir(".") if d.lower() == "lib" and os.path.isdir(d)):
        for name in sorted(os.listdir(folder)):
            dll = f"{folder}/{name}"
            if name.lower().endswith(".dll") and dll.lower() not in referenced_files:
                error(dll, f"'{dll}' is not referenced by {csproj}. Delete it or add a reference.")

    print(f"build.txt  modReferences: {', '.join(strong) or '(none)'}")
    print(f"build.txt  weakReferences: {', '.join(weak) or '(none)'}")
    print(f"{csproj}  dll references: {', '.join(sorted(referenced)) or '(none)'}")

    if not errors:
        print("Dependency declarations are consistent.")
        return 0

    print(f"\n{len(errors)} problem(s):")
    for path, message in errors:
        print(f"  {path}: {message}")
        if os.environ.get("GITHUB_ACTIONS"):
            print(f"::error file={path}::{message}")
    return 1


if __name__ == "__main__":
    sys.exit(main())
