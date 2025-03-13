using System;
using Datalogics.PDFL;

/*
 * 
 * This sample demonstrates how to import an image into a PDF file.
 *
 * Copyright (c) 2007-2025, Datalogics, Inc. All rights reserved.
 *
 */

namespace ImageImport
{
// In this scenario the Image object is used alone to create a
// new PDF page with the image as the content.
    class ImageImport
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Import Images Sample:");


            // ReSharper disable once UnusedVariable
            using (Library lib = new Library())
            {
                Console.WriteLine("Initialized the library.");
                Document doc = new Document();

                String sInput = Library.ResourceDirectory + "Sample_Input/ducky.jpg";
                String sOutput = "ImageImport-out1.pdf";

                if (args.Length > 0)
                    sInput = args[0];

                if (args.Length > 1)
                    sOutput = args[1];

                Console.WriteLine("Reading image file" + sInput + " and writing " + sOutput);

                using (Image newimage = new Image(sInput, doc))
                {
                    // Create a PDF page which is one inch larger all around than this image
                    // The design width and height for the image are carried in the
                    // Matrix.A and Matrix.D fields, respectively.
                    // There are 72 PDF user space units in one inch.
                    Rect pageRect = new Rect(0, 0, newimage.Matrix.A + 144, newimage.Matrix.D + 144);
                    Page docpage = doc.CreatePage(Document.BeforeFirstPage, pageRect);
                    // Center the image on the page
                    newimage.Translate(72, 72);
                    docpage.Content.AddElement(newimage);
                    docpage.UpdateContent();
                }

                doc.Save(SaveFlags.Full, sOutput);
            }
        }
    }
}
