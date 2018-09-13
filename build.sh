#!/usr/bin/env bash
# Copyright (c) .NET Foundation and contributors. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.

set -e
set -u

usage()
{
    echo "Main interface to running builds on Mac/Linux"
    echo "Usage: build.sh [options]"
    echo ""
    echo "Options"
    echo "  --debug               Build Debug (default)"
    echo "  --release             Build Release"
    echo "  --restore             Restore projects required to build"
    echo "  --build               Build all projects"
    echo "  --pack                Build nuget packages"
    echo "  --test                Run unit tests"
    echo "  --mono                Run unit tests with mono"
    echo "  --build-bootstrap     Build the bootstrap compilers"
    echo "  --use-bootstrap       Use the built bootstrap compilers when running main build"
    echo "  --bootstrap           Implies --build-bootstrap and --use-bootstrap"
}

root_path="$(cd -P "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
binaries_path="${root_path}"/Binaries
bootstrap_path="${binaries_path}"/Bootstrap

args=
build_in_docker=false
build_configuration=Debug
restore=false
build=false
test_=false
pack=false
use_mono=false
build_bootstrap=false
use_bootstrap=false
stop_vbcscompiler=false

# LTTNG is the logging infrastructure used by coreclr.  Need this variable set
# so it doesn't output warnings to the console.
export LTTNG_HOME="$HOME"

if [[ $# = 0 ]]
then
    usage
    echo ""
    echo "To build and test this repo, try: ./build.sh --restore --build --test"
    exit 1
fi

while [[ $# > 0 ]]
do
    opt="$(echo "$1" | awk '{print tolower($0)}')"
    case "$opt" in
        -h|--help)
            usage
            exit 1
            ;;
        --docker)
            build_in_docker=true
            shift
            continue
            ;;
        --debug)
            build_configuration=Debug
            ;;
        --release)
            build_configuration=Release
            ;;
        --restore|-r)
            restore=true
            ;;
        --build|-b)
            build=true
            ;;
        --test|-t)
            test_=true
            ;;
        --mono)
            use_mono=true
            ;;
        --build-bootstrap)
            build_bootstrap=true
            ;;
        --use-bootstrap)
            use_bootstrap=true
            ;;
        --bootstrap)
            build_bootstrap=true
            use_bootstrap=true
            ;;
        --stop-vbcscompiler)
            stop_vbcscompiler=true
            ;;
        --pack)
            pack=true
            ;;
        *)
            echo "$1"
            usage
            exit 1
        ;;
    esac
    args="$args $1"
    shift
done

config_path=${binaries_path}/${build_configuration}
logs_path=${config_path}/Logs
mkdir -p ${binaries_path}
mkdir -p ${config_path}
mkdir -p ${logs_path}

function stop_processes {
    echo "Killing running build processes..."
    pkill -9 "dotnet" || true
    pkill -9 "vbcscompiler" || true
}

if [[ "$build_in_docker" = true ]]
then
    echo "Docker exec: $args"
    BUILD_COMMAND=/opt/code/build.sh "$root_path"/build/scripts/dockerrun.sh $args
    exit
fi

source "${root_path}"/build/scripts/obtain_dotnet.sh

if [[ "$restore" == true ]]
then
    echo "Restoring RoslynToolset.csproj"
    dotnet restore "${root_path}/build/ToolsetPackages/RoslynToolset.csproj" "/bl:${logs_path}/Restore-RoslynToolset.binlog"
    echo "Restoring Compilers.sln"
    dotnet restore "${root_path}/Compilers.sln" "/bl:${logs_path}/Restore-Compilers.binlog"
fi

build_args="--no-restore -c ${build_configuration} /nologo"

if [[ "$build_bootstrap" == true ]]
then
    echo "Building bootstrap compiler"

    rm -rf ${bootstrap_path}
    mkdir -p ${bootstrap_path} 

    project_path=src/NuGet/Microsoft.NETCore.Compilers/Microsoft.NETCore.Compilers.Package.csproj

    dotnet pack -nologo ${project_path} /p:DotNetUseShippingVersions=true /p:InitialDefineConstants=BOOTSTRAP /p:PackageOutputPath=${bootstrap_path}
    unzip ${bootstrap_path}/Microsoft.NETCore.Compilers.*.nupkg -d ${bootstrap_path}
    chmod -R 755 ${bootstrap_path}

    echo "Cleaning Bootstrap compiler artifacts"
    dotnet clean ${project_path}

    stop_processes
fi

if [[ "${use_bootstrap}" == true ]]
then
    build_args+=" /p:BootstrapBuildPath=${bootstrap_path}"
fi

# https://github.com/dotnet/roslyn/issues/23736
UNAME="$(uname)"
if [[ "$UNAME" == "Darwin" ]]
then
    build_args+=" /p:UseRoslynAnalyzers=false"
fi

if [[ "${build}" == true ]]
then
    echo "Building Compilers.sln"

    if [[ "${pack}" == true ]]
    then
        build_args+=" /t:Pack"
    fi

    dotnet build "${root_path}/Compilers.sln" ${build_args} "/bl:${binaries_path}/Build.binlog"
fi

if [[ "${stop_vbcscompiler}" == true ]]
then
    if [[ "${use_bootstrap}" == true ]]
    then
        dotnet build-server shutdown
    else
        echo "--stop-vbcscompiler requires --use-bootstrap. Aborting."
        exit 1
    fi
fi

if [[ "${test_}" == true ]]
then
    if [[ "${use_mono}" == true ]]
    then
        test_runtime=mono
    else
        test_runtime=dotnet
    fi
    "${root_path}"/build/scripts/tests.sh "${build_configuration}" "${test_runtime}"
fi
