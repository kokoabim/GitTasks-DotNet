#!/usr/bin/env bash

set -eo pipefail

script_name="${0##*/}"

tool=gt
project=Kokoabim.GitTasks
deploy_dir=/opt/kokoabim/bin

default_runtime="osx-arm64"
runtimes=("$default_runtime")

action="Build $project"
deploy=0
yes=0
while getopts "dhmr:y" opt; do
    case $opt in
    d)
        deploy=1
        action="Build and deploy $project to $deploy_dir"
        ;;
    h)
        echo "Build & Deploy $project"
        echo "Usage: $script_name [-dhy]"
        echo " -d  Deploy to $deploy_dir"
        echo " -h  Show this help message"
        echo " -m  Build for multiple runtime identifiers (RIDs)"
        echo " -r  Runtime identifier (RID) to build for (default: $default_runtime)" 
        echo " -y  Confirm yes"
        exit 0
        ;;
    m)
        action="Build $project for multiple runtime identifiers (RIDs)"
        runtimes=("osx-arm64" "osx-x64" "linux-x64" "linux-arm64" "win-x64" "win-arm64") 
        ;;
    r)
        runtimes=("$OPTARG")
        ;;
    y) yes=1 ;;
    \?) exit 1 ;;
    esac
done
shift $((OPTIND - 1))

function confirm_run() {
    if [[ ${yes:-0} == 1 ]]; then
        return
    fi

    read -r -p "${action}? [y/N] " -n 1
    [[ $REPLY == "" ]] && echo -en "\033[1A" >&2
    echo >&2
    [[ $REPLY =~ ^[Yy]$ ]]
}

if ! confirm_run; then
    echo "Canceled"
    exit 1
fi

echo "Building..."
rm -rf ./build

for runtime in "${runtimes[@]}"; do
    dotnet publish -c Release -r "$runtime" -p:PublishSingleFile=true --self-contained false -o "./build/$runtime" src/$project/$project.csproj

    tool_file="$tool"
    if [[ $runtime == win* ]]; then
        tool_file="$tool.exe"
    fi
    zip -j "./build/$tool-$runtime.zip" "./build/$runtime/$tool_file"
done

if [[ $deploy -eq 1 ]]; then
    echo "Deploying..."
    cp ./build/$default_runtime/$tool $deploy_dir/$tool
fi