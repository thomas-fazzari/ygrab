# SPDX-FileCopyrightText: 2026 Thomas Fazzari
# SPDX-License-Identifier: AGPL-3.0-or-later

"""Versions of the Python runtime and of the installed packages."""

import importlib.metadata
import platform


def python_version() -> str:
    """Return the version of the running interpreter, such as 3.13.9."""
    return platform.python_version()


def package_version(name: str) -> str | None:
    """Return the version of an installed package, or None when it is missing."""
    try:
        return importlib.metadata.version(name)
    except importlib.metadata.PackageNotFoundError:
        return None
