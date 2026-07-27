"""Tiny stdlib build backend.

The project is normally run directly with ``python -m minejammer``.  This
module exists so pyproject metadata never causes an implicit package download.
"""


def _unsupported(*_args, **_kwargs):
    raise RuntimeError("MineJammer is a source-run tool; use python -m minejammer")


build_wheel = _unsupported
build_sdist = _unsupported
