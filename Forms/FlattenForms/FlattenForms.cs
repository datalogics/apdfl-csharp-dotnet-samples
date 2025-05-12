using System;
using System.Collections.Generic;
using System.Text;
using Datalogics.PDFL;

/*
 *
 * The FlattenForms sample demonstrates how to Flatten XFA into AcroForms.
 *
 *  - Flatten XFA (Dynamic or Static) to regular page content which converts and expands XFA fields to regular PDF content and removes the XFA fields.
 *  - Flatten AcroForms to regular page content which converts AcroForm fields to regular page content and removes the AcroForm fields.
 * Copyright (c) 2024-2025, Datalogics, Inc. All rights reserved.
 *
 */
namespace FlattenForms
{
    class FlattenForms
    {
        static void Main(string[] args)
        {
            Console.WriteLine("FlattenForms Sample:");

            using (Library lib = new Library(LibraryFlags.InitFormsExtension))
            {
                if (!lib.IsFormsExtensionAvailable())
                {
                    System.Console.Out.WriteLine("Forms Plugins were not properly loaded!");
                    return;
                }

                //Must be set to true to prevent default legacy behavior of PDFL
                lib.AllowOpeningXFA = true;

                Console.WriteLine("Initialized the library.");

                //XFA document
                String sInput = Library.ResourceDirectory + "Sample_Input/DynamicXFA.pdf";
                String sOutput = "../FlattenXFA-out.pdf";

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
                    UInt32 pagesOutput = doc.FlattenXFAFormFields();

                    Console.WriteLine("XFA document was expanded into {0} Flattened pages.", pagesOutput);

                    doc.Save(SaveFlags.Full | SaveFlags.Linearized, sOutput);
                }

                //AcroForms document
                sInput = Library.ResourceDirectory + "Sample_Input/AcroForm.pdf";
                sOutput = "../FlattenAcroForms-out.pdf";

                using (Document doc = new Document(sInput))
                {
                    doc.FlattenAcroFormFields();

                    Console.WriteLine("AcroForms document was Flattened.");

                    doc.Save(SaveFlags.Full | SaveFlags.Linearized, sOutput);
                }
            }
        }
    }
}
