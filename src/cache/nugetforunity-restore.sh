#!/bin/bash
set -e
cd "${GITHUB_WORKSPACE}"
dotnet nugetforunity restore "${UNITY_PROJECT_PATH}"
