#!/usr/bin/env bash

set -eo pipefail

script_name="${0##*/}"

action="Build ConsoleApp"
deploy=0
yes=0
while getopts "dhy" opt; do
    case $opt in
    d)
        deploy=1
        action="Build and deploy ConsoleApp"
        ;;
    h)
        echo "Build & Deploy ConsoleApp"
        echo "Usage: $script_name [-dhy]" >&2
        echo " -d  Deploy to /opt/kokoabim/bin/" >&2
        echo " -h  Show this help message" >&2
        echo " -y  Confirm yes" >&2
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
dotnet publish -c Release -r osx-arm64 -p:PublishSingleFile=true --self-contained false -o ./build src/Kokoabim.GitTasks/Kokoabim.GitTasks.csproj
chown -R spencer:staff ./build

if [[ $deploy -eq 1 ]]; then
    echo "Deploying..."
    cp ./build/gt /opt/kokoabim/bin/gt
    chown -R spencer:staff /opt/kokoabim/bin/gt
fi