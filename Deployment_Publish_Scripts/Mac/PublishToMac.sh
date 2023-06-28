#!/bin/bash

# This is a simple script to illustrate how to publish the dependencies of PDFL for .NET.
# You should first publish your .NET application to copy the PDFL .NET Class library and other .NET dependencies, e.g.:
# dotnet publish ./MyApplication.csproj

# Make sure the directory is there, so the following commands work
mkdir -p publish

rsync="rsync -aEq --delete"

echo Copy PDFL libraries
cp -a *.dylib ./publish

echo Copy PDFL Frameworks and Plugins
for dir in *.framework *.ppi
do
    $rsync $dir/ publish/$dir/
done

echo Copy Tesseract Data which is needed for OCR usage, if you aren't using OCR you don't need this.
$rsync ./tessdata4/ ./publish/tessdata4/

echo Copy Resources which contains things needed by PDFL for dealing with Fonts, CMaps, and Color Profiles for example
$rsync ./Resources/ ./publish/Resources/
