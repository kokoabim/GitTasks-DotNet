#!/usr/bin/env bash

set -eo pipefail

script_name="${0##*/}"

tool=gt
project=Kokoabim.GitTasks
deploy_dir=/opt/kokoabim/bin

default_arch="osx-arm64"
archs=("$default_arch")

action="Build $project"
deploy=0
multiple_archs=0
yes=0
while getopts "dhmy" opt; do
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
        echo " -m  Build for multiple architectures"
        echo " -y  Confirm yes"
        exit 0
        ;;
    m)
        multiple_archs=1
        action="Build $project for multiple architectures"
        archs=("osx-arm64" "osx-x64" "linux-x64" "linux-arm64" "win-x64" "win-arm64") 
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

for arch in "${archs[@]}"; do
    dotnet publish -c Release -r "$arch" -p:PublishSingleFile=true --self-contained false -o "./build/$arch" src/$project/$project.csproj

    tool_file="$tool"
    if [[ $arch == win* ]]; then
        tool_file="$tool.exe"
    fi
    zip -j "./build/$tool-$arch.zip" "./build/$arch/$tool_file"
done

if [[ $deploy -eq 1 ]]; then
    echo "Deploying..."
    cp ./build/$default_arch/$tool $deploy_dir/$tool
fi