#!/bin/bash
eval "$(/home/linuxbrew/.linuxbrew/bin/brew shellenv)"
# Allow apps to find .NET Core SDK
export DOTNET_ROOT="/home/linuxbrew/.linuxbrew/opt/dotnet/libexec"
# Add .NET Core SDK tools
export PATH="$PATH:/home/deck/.dotnet/tools"
source ~/.tandoku/python/bin/activate
SCRIPT_DIR=$( cd -- "$( dirname -- "${BASH_SOURCE[0]}" )" &> /dev/null && pwd )
pwsh -NoProfile -NonInteractive -f "$SCRIPT_DIR/InitAndBuildVolume.ps1" "$@"
