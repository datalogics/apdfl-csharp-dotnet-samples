using System;
using Datalogics.PDFL;

/*
 *
 * This sample shows how to add a QR barcode to a PDF page
 *
 * Copyright (c) 2024, Datalogics, Inc. All rights reserved.
 *
 */
namespace AddCollection
{
    class AddQRCode
    {
        static void Main(string[] args)
        {
            Console.WriteLine("AddQRCode Sample:");

            using (Library lib = new Library())
            {
                Console.WriteLine("Initialized the library.");

                String sInput = Library.ResourceDirectory + "Sample_Input/sample_links.pdf";
                String sOutput = "../AddQRCode-out.pdf";

                if (args.Length > 0)
                    sInput = args[0];

                using (Document doc = new Document(sInput))
                {
                    Page page = doc.GetPage(0);

                    page.AddQRBarcode("Datalogics", 72.0, page.CropBox.Top - 1.5 * 72.0, 72.0, 72.0);

                    doc.Save(SaveFlags.Full, sOutput);
                }
            }
        }
    }
}
