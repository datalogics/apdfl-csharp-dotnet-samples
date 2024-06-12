using System;
using System.Collections.Generic;
using System.Text;
using Datalogics.PDFL;

/*
 * The ExportFormsData sample demonstrates how to Export forms data from XFA and AcroForms documents:
 *
 *  - Export data from a XFA (Dynamic or Static) document, the types supported include XDP, XML, or XFD
 *  - Export data from an AcroForms document, the types supported include XFDF, FDF, or XML
 *
 * Copyright (c) 2024, Datalogics, Inc. All rights reserved.
 *
 */
namespace ExportFormsData
{
    class ExportFormsData
    {
        static void Main(string[] args)
        {
            Console.WriteLine("ExportFormsData Sample:");

            using (Library lib = new Library(LibraryFlags.InitFormsExtension))
            {
                if (!lib.IsFormsExtensionAvailable())
                {
                    System.Console.Out.WriteLine("Forms Plugins were not properly loaded!");
                    return;
                }

                lib.AllowOpeningXFA = true;

                Console.WriteLine("Initialized the library.");

                //XFA document
                String sInput = Library.ResourceDirectory + "Sample_Input/DynamicXFA.pdf";
                String sOutput = "../ExportFormsDataXFA.xdp";

                if (args.Length > 0)
                {
                    sOutput = args[0];
                }

                using (Document doc = new Document(sInput))
                {
                    //Export the data while specifying the type, in this case XDP
                    bool result = doc.ExportXFAFormsData(sOutput, XFAFormExportType.XDP);

                    if (result)
                    {
                        Console.Out.WriteLine("Forms data was exported!");
                    }
                    else
                    {
                        Console.Out.WriteLine("Exporting of Forms data failed!");
                    }
                }

                //AcroForms document
                sInput = Library.ResourceDirectory + "Sample_Input/AcroForm.pdf";
                sOutput = "../ExportFormsDataAcroForms.xfdf";

                if (args.Length > 1)
                {
                    sOutput = args[1];
                }

                using (Document doc = new Document(sInput))
                {
                    //Export the data while specifying the type, in this case XFDF
                    bool result = doc.ExportAcroFormsData(sOutput, AcroFormExportType.XFDF);

                    if (result)
                    {
                        Console.Out.WriteLine("Forms data was exported!");
                    }
                    else
                    {
                        Console.Out.WriteLine("Exporting of Forms data failed!");
                    }
                }
            }
        }
    }
}
