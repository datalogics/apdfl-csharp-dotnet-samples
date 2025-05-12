from dl_conan_build_tools.tasks import conan
from invoke import Collection, task
from invoke.tasks import Task
import platform
import os
import pathlib
import xml.etree.ElementTree as ET

samples_list = [
              'Annotations/Annotations/',
              'Annotations/InkAnnotations/',
              'Annotations/LinkAnnotation/',
              'Annotations/PolygonAnnotations/',
              'Annotations/PolyLineAnnotations/',
              'ContentCreation/AddElements/',
              'ContentCreation/AddHeaderFooter/',
              'ContentCreation/Clips/',
              'ContentCreation/CreateBookmarks/',
              'ContentCreation/GradientShade/',
              'ContentCreation/MakeDocWithCalGrayColorSpace/',
              'ContentCreation/MakeDocWithCalRGBColorSpace/',
              'ContentCreation/MakeDocWithDeviceNColorSpace/',
              'ContentCreation/MakeDocWithICCBasedColorSpace/',
              'ContentCreation/MakeDocWithIndexedColorSpace/',
              'ContentCreation/MakeDocWithLabColorSpace/',
              'ContentCreation/MakeDocWithSeparationColorSpace/',
              'ContentCreation/NameTrees/',
              'ContentCreation/NumberTrees/',
              'ContentCreation/RemoteGoToActions/',
              'ContentCreation/WriteNChannelTiff/',
              'ContentModification/Action/',
              'ContentModification/AddCollection/',
              'ContentModification/AddQRCode/',
              'ContentModification/ChangeLayerConfiguration/',
              'ContentModification/ChangeLinkColors/',
              'ContentModification/CreateLayer/',
              'ContentModification/ExtendedGraphicStates/',
              'ContentModification/FlattenTransparency/',
              'ContentModification/LaunchActions/',
              'ContentModification/MergePDF/',
              'ContentModification/PageLabels/',
              'ContentModification/PDFObject/',
              'ContentModification/UnderlinesAndHighlights/',
              'ContentModification/Watermark/',
              'DocumentConversion/ColorConvertDocument/',
              'DocumentConversion/ConvertToOffice/',
              'DocumentConversion/CreateDocFromXPS/',
              'DocumentConversion/Factur-XConverter/',
              'DocumentConversion/PDFAConverter/',
              'DocumentConversion/PDFXConverter/',
              'DocumentConversion/ZUGFeRDConverter/',
              'DocumentOptimization/PDFOptimize/',
              'Images/DocToImages/',
              'Images/DrawSeparations/',
              'Images/DrawToBitmap/',
              'Images/EPSSeparations/',
              'Images/GetSeparatedImages/',
              'Images/ImageEmbedICCProfile/',
              'Images/ImageExport/',
              'Images/ImageExtraction/',
              'Images/ImageFromStream/',
              'Images/ImageImport/',
              'Images/ImageResampling/',
              'Images/ImageSoftMask/',
              'Images/OutputPreview/',
              'Images/RasterizePage/',
              'InformationExtraction/ListBookmarks/',
              'InformationExtraction/ListInfo/',
              'InformationExtraction/ListLayers/',
              'InformationExtraction/ListPaths/',
              'InformationExtraction/Metadata/',
              'OpticalCharacterRecognition/AddTextToDocument/',
              'OpticalCharacterRecognition/AddTextToImage/',
              'OpticalCharacterRecognition/OCRDocument/',
              'Other/MemoryFileSystem/',
              'Other/StreamIO/',
              'Security/AddDigitalSignature/',
              'Security/AddRegexRedaction/',
              'Security/Redactions/',
              'Text/AddGlyphs/',
              'Text/AddUnicodeText/',
              'Text/AddVerticalText/',
              'Text/ExtractAcroFormFieldData/',
              'Text/ExtractCJKTextByPatternMatch/',
              'Text/ExtractTextByPatternMatch/',
              'Text/ExtractTextByRegion/',
              'Text/ExtractTextFromAnnotations/',
              'Text/ExtractTextFromMultiRegions/',
              'Text/ExtractTextPreservingStyleAndPositionInfo/',
              'Text/ListWords/',
              'Text/RegexExtractText/',
              'Text/RegexTextSearch/',
              'Text/TextExtract/'
              ]


# Given an absolute sample path, modify the csproj to pull 
# a different version of the nuget package for testing
def set_nuget_pkg_version(sample=None, package=None):
    if package is None:
        return
    else:
        tree = ET.parse(sample.absolute().as_posix())
        elem = tree.findall(".//PackageReference")
        for entry in elem:
            if 'Adobe.PDF.Library' in entry.attrib.get('Include'):
                entry.set('Include', package)
                break
        
        # Add the SampleInput entry. We already have an object 
        # in entry_copy just gotta change the values to SampleInput
        new_entry = ET.Element('PackageReference')
        new_entry.set('Include', 'Adobe.PDF.Library.SampleInput')
        new_entry.set('Version', '1.*')
        insert_elem = tree.findall(".//ItemGroup")
        insert_elem[0].append(new_entry)
        tree.write(sample)

@task()
def clean_samples(ctx):
    for sample in samples_list:
        full_path = os.path.join(os.getcwd(), sample)
        with ctx.cd(full_path):
            ctx.run('git clean -fdx')
            ctx.run('git checkout .')

@task()
def clean_nuget_cache(ctx):
    ctx.run('dotnet nuget locals --clear all')

@task()
def build_samples(ctx, pkg_name='Adobe.PDF.Library.NET', config='Debug'):
    """Builds the .NET samples"""
    ctx.run('invoke clean-samples')
    for sample in samples_list:
        full_path = os.path.join(os.getcwd(), sample)
        if 'DrawSeparations' in sample or 'DocToImages' in sample:
            continue
        if platform.system() == 'Darwin' and ('ConvertToOffice' in sample or 'CreateDocFromXPS' in sample):
            print(f'{sample} not available on this OS')
            continue
        else:
            with ctx.cd(full_path):
                last_directory = os.path.basename(os.path.dirname(full_path))
                full_name = full_path + last_directory + '.csproj'
                set_nuget_pkg_version(pathlib.Path(full_name), package=pkg_name)
                if config == 'Release':
                    ctx.run(live_source_build())
                elif config == 'Debug':
                    ctx.run(nightly_source_build())


@task()
def run_samples(ctx):
    """Runs the .NET samples
    """
    for sample in samples_list:
        full_path = os.path.join(os.getcwd(), sample)
        if 'DrawSeparations' in sample or 'DocToImages' in sample:
            continue
        if platform.system() == 'Darwin' and ('ConvertToOffice' in sample or 'CreateDocFromXPS' in sample):
            print(f'{sample} not available on this OS')
            continue
        elif platform.system() == 'Linux' and 'ConvertToOffice' in sample:
            continue
        else:
            with ctx.cd(full_path):
                sample_name = os.path.basename(os.path.dirname(full_path))
                if 'DrawSeparations' in sample_name:
                    continue
                ctx.run(f'dotnet run --no-build')


def live_source_build():
    """Locations of packages that are live"""
    if platform.system() == 'Darwin':
        return (f'dotnet build '
                '--source https://api.nuget.org/v3/index.json '
                '--source /Volumes/raid/products/released/APDFL/nuget/DotNET/for_apdfl_18.0.5Plus/approved/current '
                '--source /Volumes/raid/products/released/APDFL/nuget/SampleInputFile/for_apdfl_18.0.4Plus/approved/current ')
    elif platform.system() == 'Windows':
        return (f'dotnet build '
                '--source https://api.nuget.org/v3/index.json '
                '--source \\\\ivy\\raid\\products\\released\\APDFL\\nuget\\DotNET\\for_apdfl_18.0.5Plus\\approved\\current '

                '--source \\\\ivy\\raid\\products\\released\\APDFL\\nuget\\SampleInputFile\\for_apdfl_18.0.4Plus\\approved\\current ')
    else:
        return (f'dotnet build '
                '--source https://api.nuget.org/v3/index.json '
                '--source /raid/products/released/APDFL/nuget/DotNET/for_apdfl_18.0.5Plus/approved/current '
                '--source /raid/products/released/APDFL/nuget/SampleInputFile/for_apdfl_18.0.4Plus/approved/current ')


def nightly_source_build():
    """Locations of nightly packages. Note: These paths will only work on the nuget-builder build machine"""
    if platform.system() == 'Darwin':
        return (f'dotnet build '
                '--source https://api.nuget.org/v3/index.json '
                '--source /Volumes/raid/nuget-builder-samples-test ')
    elif platform.system() == 'Windows':
        return (f'dotnet build '
                '--source https://api.nuget.org/v3/index.json '
                '--source \\\\ivy\\raid\\nuget-builder-samples-test ')
    else:
        return (f'dotnet build '
                '--source https://api.nuget.org/v3/index.json '
                '--source /raid/nuget-builder-samples-test ')



tasks = []
tasks.extend([v for v in locals().values() if isinstance(v, Task)])

conan_tasks = Collection()
conan_tasks.add_task(conan.install_config)
conan_tasks.add_task(conan.login)
conan_tasks.add_task(conan.upload_dependencies)

ns = Collection(*tasks)
ns.add_collection(conan_tasks, 'conan')

ns.configure({'run': {'echo': 'true'}})
