#!/bin/bash

# Find all files and replace Aggressor with Evil
find . -type f -exec sed -i 's/Aggressor/Evil/g' {} +
