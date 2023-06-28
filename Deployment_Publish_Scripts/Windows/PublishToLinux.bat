REM This is a simple script to illustrate how to publish the dependencies of PDFL for .NET.
REM You should first publish your .NET application to copy the PDFL .NET Class library and other .NET dependencies, e.g.:
REM dotnet publish ./MyApplication.csproj

echo Copy Tesseract Data which is needed for OCR usage, if you aren't using OCR you don't need this.
xcopy "./tessdata4" "./publish/tessdata4" /i /y

echo Copy Linux dependencies, upon deployment you need to decompress this archive
xcopy "Linux_Dependencies.tar.gz" "./publish" /y

echo Copy Resources which contains things needed by PDFL for dealing with Fonts, CMaps, and Color Profiles for example
xcopy ".\Resources\*" ".\publish\Resources" /i /s /y
