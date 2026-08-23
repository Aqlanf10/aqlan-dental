#!/usr/bin/env python3
"""
Which settings the screen offers, and which the server actually reads.

Two defects this session had the same shape: a control the owner can change that
nothing consults — the roles screen's permission switches, and the print language.
Both looked fine until something asked whether the value reached a reader. This asks
that question for the Settings screen as a whole.

A key the UI writes but no backend file reads is a dial connected to nothing.
"""
import os
import re
import sys

FRONTEND = "frontend/src/app/(dashboard)/settings"
BACKEND = "backend/src"

KEY = re.compile(r'["\']([a-z_]+\.[a-z_.]{2,60})["\']')


def keys_in(root: str, exts=(".ts", ".tsx", ".cs")) -> dict[str, set[str]]:
    found: dict[str, set[str]] = {}
    for dirpath, _, filenames in os.walk(root):
        if "__tests__" in dirpath or "/obj/" in dirpath or "/bin/" in dirpath:
            continue
        for filename in filenames:
            if not filename.endswith(exts):
                continue
            path = os.path.join(dirpath, filename)
            with open(path, encoding="utf-8", errors="ignore") as fh:
                for key in KEY.findall(fh.read()):
                    found.setdefault(key, set()).add(path)
    return found


CONST = re.compile(r'public const string (\w+)\s*=\s*"([a-z_]+\.[a-z_.]+)"')


def alias_map() -> dict[str, set[str]]:
    """key literal -> the C# constant names that hold it."""
    out: dict[str, set[str]] = {}
    for dirpath, _, filenames in os.walk(BACKEND):
        for filename in filenames:
            if not filename.endswith(".cs"):
                continue
            path = os.path.join(dirpath, filename)
            with open(path, encoding="utf-8", errors="ignore") as fh:
                for name, literal in CONST.findall(fh.read()):
                    out.setdefault(literal, set()).add(name)
    return out


def constant_uses(constant: str) -> set[str]:
    """Files that mention a constant by name (its own declaration included)."""
    hits: set[str] = set()
    pattern = re.compile(rf"\b{re.escape(constant)}\b")
    for dirpath, _, filenames in os.walk(BACKEND):
        if "/obj/" in dirpath or "/bin/" in dirpath:
            continue
        for filename in filenames:
            if not filename.endswith(".cs"):
                continue
            path = os.path.join(dirpath, filename)
            with open(path, encoding="utf-8", errors="ignore") as fh:
                if pattern.search(fh.read()):
                    hits.add(path)
    return hits


def main() -> int:
    ui = keys_in(FRONTEND)
    server = keys_in(BACKEND)

    # Only settings-shaped namespaces; the regex also catches route and package names.
    prefixes = ("clinic.", "finance.", "website.", "lab.", "ortho.", "ai.", "email.",
                "sms.", "whatsapp.", "notifications.", "permissions.", "appointments.")
    offered = {k for k in ui if k.startswith(prefixes)}

    # Declaring a default and seeding a row are not reading the value. The print-language
    # key was mentioned in exactly these two kinds of place and no PDF generator consulted
    # it, so the switch changed nothing on the documents it named. "Mentioned somewhere in
    # the backend" is the weak check that hid it.
    # Compared as whole file names, not suffixes: "ServicesSettingsController.cs" ends with
    # "SettingsController.cs", so an endswith test silently classified a real reader as a
    # declaration and reported three live keys as dead.
    DECLARATION_ONLY = {"SettingsController.cs", "DbSeeder.cs",
                        "StartupDatabaseMaintenance.cs", "FinanceSettingsKeys.cs"}

    # Most finance keys are referenced through a named constant, not the literal. Searching
    # only for the literal reported nine keys as unread that are read every day — including
    # two the receipt itself uses. Resolve constant -> literal first, then count uses of the
    # constant name as uses of the key.
    aliases = alias_map()

    def consumed(key: str) -> bool:
        places = set(server.get(key, ()))
        for constant in aliases.get(key, ()):
            places |= constant_uses(constant)
        return any(os.path.basename(where) not in DECLARATION_ONLY for where in places)

    unmentioned = sorted(k for k in offered if k not in server)
    declared_only = sorted(k for k in offered if k in server and not consumed(k))

    print(f"settings keys the screen mentions: {len(offered)}")
    print(f"  never mentioned in backend/src:  {len(unmentioned)}")
    print(f"  only declared or seeded, never read: {len(declared_only)}")

    for title, keys in (("not mentioned anywhere in backend/src", unmentioned),
                        ("declared or seeded, but nothing reads the value", declared_only)):
        if not keys:
            continue
        print(f"\n{title}:")
        for key in keys:
            print(f"  {key}")
            for where in sorted(server.get(key, ())):
                print(f"      declared in {where}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
