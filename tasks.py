from invoke import Collection, Exit, task
from invoke.tasks import Task
import platform
import os
import pathlib
import xml.etree.ElementTree as ET
import shutil

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
              'DocumentConversion/CreateDocFromWebPage/',
              'DocumentConversion/CreateDocFromXPS/',
              'DocumentConversion/CreateInvoiceFromStructuredData/',
              'DocumentConversion/Factur-XConverter/',
              'DocumentConversion/MarkdownToPDF/',
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
              'Security/AddBasicPAdESElectronicSignature/',
              'Security/AddPAdESPolicySignature/',
              'Security/AddDigitalSignatureCMS/',
              'Security/AddDigitalSignatureRFC3161/',
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

# The package sources build-samples can test against:
#   Nightly - .nupkg files copied off the raid share into a local feed, with the
#             samples repointed at Adobe.PDF.Library.NET, the non-license-managed
#             package that exists only in that feed.
#   Public  - resolved from nuget.org, keeping the license-managed package ids
#             the samples already reference because those are the only ones
#             published. Nothing is copied locally, so this is the pass that
#             fails when a release is approved but not actually usable: a wrong
#             or partial upload, an unlisted version, or a dependency package
#             that never made it up (APDFL.SharedLibs, APDFL.SharedOfficeLibs).
package_sources = ('Nightly', 'Public')

nugetOrgFeed = 'https://api.nuget.org/v3/index.json'

# Emptied before it is populated: the nightly and public packages share version
# numbers and the samples float their PackageReference (Version="21.*"), so a
# leftover .nupkg from an earlier run would outrank the one under test.
nightlyFeedDir = 'packages_nightly'


# Given an absolute sample path, add the SampleInput package the samples read
# their input files from, and optionally repoint the Adobe.PDF.Library reference
# at a different package. `package` is left None for the public pass: the
# license-managed ids already in the csproj are the ones published on nuget.org.
def set_nuget_pkg_version(sample=None, package=None):
    tree = ET.parse(sample.absolute().as_posix())

    if package is not None:
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
def clean_nuget_packages(ctx):
    """Clears extracted packages, keeping the http cache

    Restore reuses an already-extracted package without consulting any source,
    so the nightly and public passes have to run against an empty
    global-packages folder to be sure of which bits they built against. This
    matters most for the dependencies the two passes share by id and version --
    APDFL.SharedLibs, Adobe.PDF.Library.Resources, SampleInput -- where a copy
    cached from the nightly feed would hide a version that was never published.
    The http cache is left alone so the nuget.org packages do not download twice.
    """
    ctx.run('dotnet nuget locals global-packages --clear')

# Defaulted rather than required so a bare `invoke build-samples` keeps working
# the way it always has, on the nightly packages. Invoke never treats an
# argument that has a default as positional, so the value has to come in as a
# flag: `invoke build-samples --pkg-source Public`.
@task(help={'pkg_source': f'Packages to build against: {" or ".join(package_sources)}'})
def build_samples(ctx, pkg_source='Nightly'):
    """Builds the .NET samples against the Nightly or Public packages"""
    # Checked before anything else: an unrecognized value used to fall through
    # both branches and build against whatever packages were left in the tree,
    # which passes without testing anything.
    if pkg_source not in package_sources:
        raise Exit(f'unknown package source {pkg_source!r}, '
                   f'expected one of {list(package_sources)}')

    ctx.run('invoke clean-samples')

    # nuget.org is in both passes: the samples also pull SampleInput, SkiaSharp
    # and the transitive dependencies of the Adobe packages from it. Passing
    # --source explicitly means the public pass cannot fall back to a local or
    # raid feed configured in NuGet.config.
    sources = [nugetOrgFeed]
    rewritePackage = None

    if pkg_source == 'Nightly':
        rewritePackage = 'Adobe.PDF.Library.NET'
        nightlyFeed = make_package_dir(nightlyFeedDir)
        get_nightly_packages(nightlyFeed)
        sources.append(nightlyFeed)

        # Checked here rather than left to the restore: an unmounted or empty
        # raid share otherwise shows up as NU1101 on all 94 samples, naming a
        # package id instead of the share that failed to provide it.
        if not any(pkg.startswith(f'{rewritePackage}.') for pkg in os.listdir(nightlyFeed)):
            raise Exit(f'{rewritePackage} is not in the nightly feed at {nightlyFeed}. '
                       f'The nightly share had packages but not that one -- '
                       f'check what nuget-builder last published to it.')

    sourceArgs = ' '.join(f'--source {source}' for source in sources)

    for sample in samples_list:
        full_path = os.path.join(os.getcwd(), sample)
        if 'DrawSeparations' in sample or 'DocToImages' in sample:
            continue
        if platform.system() == 'Darwin' and ('ConvertToOffice' in sample or 'CreateDocFromXPS' in sample):
            print(f'{sample} not available on this OS')
            continue
        # WebToPDF ships a macOS runtime package for arm64 only
        elif platform.system() == 'Darwin' and platform.machine() != 'arm64' and 'CreateDocFromWebPage' in sample:
            print(f'{sample} not available on this OS')
            continue
        else:
            with ctx.cd(full_path):
                last_directory = os.path.basename(os.path.dirname(full_path))
                full_name = full_path + last_directory + '.csproj'
                set_nuget_pkg_version(pathlib.Path(full_name), package=rewritePackage)

                ctx.run(f'dotnet build {sourceArgs}')


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
        # WebToPDF ships a macOS runtime package for arm64 only
        elif platform.system() == 'Darwin' and platform.machine() != 'arm64' and 'CreateDocFromWebPage' in sample:
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


def make_package_dir(name):
    """Returns an absolute, empty local feed directory named `name`."""
    packageDir = os.path.join(os.getcwd(), name)
    shutil.rmtree(packageDir, ignore_errors=True)
    os.makedirs(packageDir)
    return packageDir


def nupkgs_in(packagePath):
    """Absolute paths of the .nupkg files in packagePath.

    Filtered because these shares also hold symbol packages, checksums and
    subdirectories, and shutil.copy raises on anything that is not a file.
    """
    return [os.path.join(packagePath, item) for item in os.listdir(packagePath)
            if item.endswith('.nupkg')]


def copy_packages_locally(libraryPackages, packageDir):
    for package in libraryPackages:
        shutil.copy(package, packageDir)


def get_nightly_packages(packageDir):
    """Locations of nightly packages. Note: These paths will only work on the nuget-builder build machine"""
    if platform.system() == 'Darwin':
        libraryPackagePath = '/Volumes/raid/nuget-builder-samples-test'
    elif platform.system() == 'Windows':
        libraryPackagePath = '\\\\ivy\\raid\\nuget-builder-samples-test'
    else:
        libraryPackagePath = '/raid/nuget-builder-samples-test'

    # An empty result used to be harmless, because packages accumulated in the
    # repo root across runs and an earlier run's copy could satisfy the build.
    # The feed is emptied every run now, so an unmounted share has to be loud.
    packages = nupkgs_in(libraryPackagePath)
    if not packages:
        raise Exit(f'no .nupkg files found in {libraryPackagePath} -- '
                   f'is the raid share mounted on this node?')

    copy_packages_locally(packages, packageDir)
    print(f'... copied {len(packages)} packages into {packageDir}')


tasks = []
tasks.extend([v for v in locals().values() if isinstance(v, Task)])

ns = Collection(*tasks)

ns.configure({'run': {'echo': 'true'}})
