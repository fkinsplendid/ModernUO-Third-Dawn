#!/bin/bash

# Find all files and replace Evil with Evil
find . -type f -exec sed -i 's/Evil/Evil/g' {} +
