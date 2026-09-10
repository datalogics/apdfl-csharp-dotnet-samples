from invoke import Collection, Exit, task
from invoke.tasks import Task
import platform
import os
import pathlib
import xml.etree.ElementTree as ET
import json
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

# Checked by the Public pass. Every sample resolves this id -- most reference it
# directly, and the Forms Extension samples get it as a pinned dependency of
# Adobe.PDF.Library.FormsExtension.LM.NET -- so the version it restores to is
# the version of APDFL that nuget.org is actually serving.
publicPackageId = 'Adobe.PDF.Library.LM.NET'

# The version the Public pass requires nuget.org to be serving. Raise it when a
# release is approved: until it is raised the pass confirms that the previous
# release is still installable, and once it is raised the pass stays red until
# the new packages are genuinely live. It is a floor rather than an equality
# check so that a later patch release does not turn the nightly run red on its
# own. Override for a one-off check with `--expect-version`.
publicVersionFloor = '21.1.0'


def parse_version(text):
    """A NuGet version as a comparable tuple, ignoring any prerelease tag.

    Only the numeric release part is compared, which is all these packages
    publish, so '21.2.0-beta1' sorts equal to '21.2.0' rather than below it.
    """
    release = text.split('-', 1)[0].split('+', 1)[0]
    return tuple(int(part) for part in release.split('.') if part.isdigit())


def resolved_version(samplePath, packageId):
    """The version of packageId that this sample's restore settled on.

    Read out of obj/project.assets.json, whose `libraries` keys are
    "<id>/<version>" and cover transitively pulled packages as well as
    directly referenced ones. None when the package is not in the graph.
    """
    assets = os.path.join(samplePath, 'obj', 'project.assets.json')
    if not os.path.exists(assets):
        return None

    with open(assets, encoding='utf-8') as stream:
        libraries = json.load(stream).get('libraries', {})

    for entry in libraries:
        name, _, version = entry.partition('/')
        if name == packageId:
            return version
    return None


def check_public_version(resolved, expected):
    """Fail unless every sample restored publicPackageId at `expected` or newer.

    This is the check a silently missed publication fails. A build against
    nuget.org floats to whatever the newest published version happens to be
    (the samples reference Version="21.*"), so without it the pass goes green
    against the release *before* the one being validated -- which is the case
    it exists to catch.
    """
    # An empty result is the vacuous pass this check exists to prevent, so it
    # is an error rather than a silent success.
    if not resolved:
        raise Exit('no samples were built, so the public packages were never '
                   'exercised -- check the platform skips above.')

    missing = sorted(sample for sample, version in resolved.items() if version is None)
    if missing:
        raise Exit(f'{publicPackageId} is not in the restore graph of '
                   f'{len(missing)} of {len(resolved)} samples, so there is no '
                   f'version to check. First few: {", ".join(missing[:3])}')

    versions = sorted(set(resolved.values()), key=parse_version)
    print(f'... nuget.org served {publicPackageId} {", ".join(versions)}')

    floor = parse_version(expected)
    stale = [version for version in versions if parse_version(version) < floor]
    if stale:
        raise Exit(f'nuget.org is serving {publicPackageId} {", ".join(stale)}, '
                   f'but {expected} or newer was expected. Either the release '
                   f'was never pushed or it went up unlisted -- check the '
                   f'upload rather than trusting this run.')


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
@task(help={'pkg_source': f'Packages to build against: {" or ".join(package_sources)}',
            'expect_version': f'Public pass only: require nuget.org to serve '
                              f'{publicPackageId} at this version or newer '
                              f'(default {publicVersionFloor})'})
def build_samples(ctx, pkg_source='Nightly', expect_version=None):
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

    # Public pass only; sample -> the version of publicPackageId it restored.
    resolved = {}

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

                if pkg_source == 'Public':
                    resolved[sample] = resolved_version(full_path, publicPackageId)

    if pkg_source == 'Public':
        check_public_version(resolved, expect_version or publicVersionFloor)


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
