#!/bin/bash

# Store the script name
SCRIPT_NAME=$(basename "$0")

# Find .cs files, explicitly exclude this script, and replace Aggressor with Evil
find . -name "*.cs" -type f ! -name "$SCRIPT_NAME" -exec sed -i 's/Aggressor/Evil/g' {} +
