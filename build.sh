#!/usr/bin/env bash

set -eo pipefail

script_name="${0##*/}"

tool=gt
project=Kokoabim.GitTasks
deploy_dir=/opt/kokoabim/bin

action="Build $project"
deploy=0
yes=0
while getopts "dhy" opt; do
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
        echo " -y  Confirm yes"
        exit 0
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
dotnet publish -c Release -r osx-arm64 -p:PublishSingleFile=true --self-contained false -o ./build src/$project/$project.csproj

if [[ $deploy -eq 1 ]]; then
    echo "Deploying..."
    cp ./build/$tool $deploy_dir/$tool
fi