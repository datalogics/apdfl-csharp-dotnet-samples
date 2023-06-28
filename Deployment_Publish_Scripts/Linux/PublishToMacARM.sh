#!/bin/bash

# This is a simple script to illustrate how to publish the dependencies of PDFL for .NET.
# You should first publish your .NET application to copy the PDFL .NET Class library and other .NET dependencies, e.g.:
# dotnet publish ./MyApplication.csproj

echo Copy Tesseract Data which is needed for OCR usage, if you aren't using OCR you don't need this.
cp -rp ./tessdata4 ./publish

echo Copy Mac ARM dependencies, upon deployment you need to decompress this archive
cp ./MacARM_Dependencies.zip ./publish

echo Copy Resources which contains things needed by PDFL for dealing with Fonts, CMaps, and Color Profiles for example
cp -rp ./Resources ./publish
