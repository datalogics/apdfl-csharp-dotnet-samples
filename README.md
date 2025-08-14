![Datalogics Adobe PDF Library](https://raw.github.com/datalogics/dl-icons/develop/DLBanner_Nuget.png)

[Documentation](https://dev.datalogics.com/adobe-pdf-library/dot-net/getting-started) &nbsp;| [API Reference](https://docs.datalogics.com/apdfl18/DotNet/index.html) &nbsp;|&nbsp; [Support](https://www.datalogics.com/tech-support-pdfs/) &nbsp; | &nbsp; [Release Notes](https://docs.datalogics.com/apdfl18/Release_Notes.html) &nbsp; | &nbsp;[Homepage](https://www.datalogics.com)

[![Download a Free Trial on NuGet](https://img.shields.io/nuget/dt/Adobe.PDF.Library.LM.NET?color=blue&label=APDFL%20.NET%20Free%20Trial&logo=NuGet&style=plastic)](https://www.nuget.org/packages/Adobe.PDF.Library.LM.NET)

# .NET Samples
## Introduction
Built upon Adobe source code used for Acrobat, Datalogics Adobe PDF Library SDK provides stable, reliable code and the flexibility to develop with C# or VB (VB.NET) (interfaces are also available for C++ and Java). APDFL is the most complete SDK for PDF creation, manipulation and management. Best for enterprise/larger organizations of developers and independent software vendors (ISVs) who need to incorporate Adobe's PDF functionality into their own internal or external applications.

## Preliminaries
Most of the code samples in APDFL are designed to demonstrate how an API works by completing a simple programming task.

We assume a basic level of technical understanding of the PDF file format, individual sample category directory markdown files go into more details.

Many of these sample programs generate an output file or set of files.  These output files, generally PDF or graphics files (JPG or BMP), are stored in the directory where the application has been run. If you run a sample program a second or third time, it will overwrite any output files that were created and stored earlier.  However, if you run a sample program, generate a PDF output file, and then open that PDF file and try to run that sample program again, you will see an error message.  The program will not be able to overwrite an existing output file if that file is currently open in another program.

*(Note: that the Forms Extension product is available by talking to Datalogics Sales.)*

## Building and Running Samples
*Samples can be built and run easily in an IDE such as Visual Studio 2022 or VS Code.*

## Free trial & license activation

To activate the free trial:
1. Visit [Free Trial](https://www.datalogics.com/pdf-sdk-free-trial) to obtain an activation key.
2. A prompt will appear on your console when executing Datalogics sample code.

Alternatively, to use an activation key in code, the <em>LicenseKey</em> member of the <em>Library</em> class can be set to
a valid activation key <b>prior</b> to instantiating the library.
```
Library.LicenseKey = "xxxx-xxxx-xxxx-xxxx";
using (Library lib = new Library())
{
    //APDFL Code
}
```

**Otherwise**, here are instructions for using **dotnet** instead:

Change to the directory of the program you want to work with. Here is an example:

```cd ./Images/RasterizePage```

Build the sample:

```dotnet build```

Run the application:

```dotnet run```

**Note**: Samples are setup to write their output files to their program's executable directory along with any other dependencies, such as SkiaSharp for Graphics.
