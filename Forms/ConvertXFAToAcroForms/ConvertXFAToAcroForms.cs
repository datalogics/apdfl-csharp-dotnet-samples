using System;
using System.Collections.Generic;
using System.Text;
using Datalogics.PDFL;

/*
 *
 * The ConvertXFAToAcroForms sample demonstrates how to convert XFA into AcroForms.
 * Converts XFA (Dynamic or Static) fields to AcroForms fields and removes XFA fields.
 * Copyright (c) 2024-2025, Datalogics, Inc. All rights reserved.
 *
 */
namespace ConvertXFAToAcroForms
{
    class ConvertXFAToAcroForms
    {
        static void Main(string[] args)
        {
            Console.WriteLine("ConvertXFAToAcroForms Sample:");

            using (Library lib = new Library(LibraryFlags.InitFormsExtension))
            {
                if (!lib.IsFormsExtensionAvailable())
                {
                    System.Console.Out.WriteLine("Forms Plugins were not properly loaded!");
                    return;
                }

                lib.AllowOpeningXFA = true;

                Console.WriteLine("Initialized the library.");

                String sInput = Library.ResourceDirectory + "Sample_Input/DynamicXFA.pdf";
                String sOutput = "../ConvertXFAToAcroForms-out.pdf";

                if (args.Length > 0)
                {
                    sInput = args[0];
                }

                if (args.Length > 1)
                {
                    sOutput = args[1];
                }

                using (Document doc = new Document(sInput))
                {
                    UInt32 pagesOutput = doc.ConvertXFAFieldsToAcroFormFields();

                    Console.WriteLine("XFA document was converted into an AcroForms document with {0} pages.", pagesOutput);

                    doc.Save(SaveFlags.Full | SaveFlags.Linearized, sOutput);
                }
            }
        }
    }
}
