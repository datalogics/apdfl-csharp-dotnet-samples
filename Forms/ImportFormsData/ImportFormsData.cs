using System;
using System.Collections.Generic;
using System.Text;
using Datalogics.PDFL;

/*
 * The ImportFormsData sample demonstrates how to Import forms data into XFA and AcroForms documents:
 *
 *  - Import data into a XFA (Dynamic or Static) document, the types supported include XDP, XML, or XFD
 *  - Import data into an AcroForms document, the types supported include XFDF, FDF, or XML
 *
 * Copyright (c) 2024-2025, Datalogics, Inc. All rights reserved.
 */
namespace ImportFormsData
{
    class ImportFormsData
    {
        static void Main(string[] args)
        {
            Console.WriteLine("ImportFormsData Sample:");

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
                String sInputData = Library.ResourceDirectory + "Sample_Input/DynamicXFA_data.xdp";
                String sOutput = "../ImportFormsDataXFA-out.pdf";

                if (args.Length > 0)
                {
                    sOutput = args[0];
                }

                using (Document doc = new Document(sInput))
                {
                    //Import the data, acceptable types include XDP, XML, and XFD
                    bool result = doc.ImportXFAFormsData(sInputData);

                    if (result)
                    {
                        Console.Out.WriteLine("Forms data was imported!");

                        doc.Save(SaveFlags.Full | SaveFlags.Linearized, sOutput);
                    }
                    else
                    {
                        Console.Out.WriteLine("Importing of Forms data failed!");
                    }
                }

                //AcroForms document
                sInput = Library.ResourceDirectory + "Sample_Input/AcroForm.pdf";
                sInputData = Library.ResourceDirectory + "Sample_Input/AcroForm_data.xfdf";
                sOutput = "../ImportFormsDataAcroForms-out.pdf";

                if (args.Length > 1)
                {
                    sOutput = args[1];
                }

                using (Document doc = new Document(sInput))
                {
                    //Import the data while specifying the type, in this case XFDF
                    bool result = doc.ImportAcroFormsData(sInputData, AcroFormImportType.XFDF);

                    if (result)
                    {
                        Console.Out.WriteLine("Forms data was imported!");

                        doc.Save(SaveFlags.Full | SaveFlags.Linearized, sOutput);
                    }
                    else
                    {
                        Console.Out.WriteLine("Importing of Forms data failed!");
                    }
                }
            }
        }
    }
}
