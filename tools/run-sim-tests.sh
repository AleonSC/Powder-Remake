#!/usr/bin/env bash
# Compiles the engine-independent simulation code and its tests with Mono and
# runs them, without needing Unity. Inside Unity, use Window > General > Test Runner.
# Pass name fragments to run only matching tests, e.g. tools/run-sim-tests.sh Momentum
set -euo pipefail
cd "$(dirname "$0")/.."
out="$(mktemp -d)"
mcs -optimize+ -out:"$out/simtests.exe" \
  Assets/Scripts/Simulation/*.cs Assets/Tests/EditMode/*.cs tools/NUnitShim.cs
mono "$out/simtests.exe" "$@"
