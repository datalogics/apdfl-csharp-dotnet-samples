using System;
using Datalogics.PDFL;

/*
 * 
 * This program creates a PDF file with an embedded hyperlink, which takes the viewer to the second page of the document.
 *
 * Copyright (c) 2007-2023, Datalogics, Inc. All rights reserved.
 *
 */
// ReSharper disable once CheckNamespace
namespace LinkAnnotations
{
    class LinkAnnotations
    {
        static void Main(string[] args)
        {
            Console.WriteLine("LinkAnnotations Sample:");


            // ReSharper disable once UnusedVariable
            using (Library lib = new Library())
            {
                Console.WriteLine("Initialized the library.");

                String sInput = Library.ResourceDirectory + "Sample_Input/sample.pdf";
                String sOutput = "LinkAnnotation-out.pdf";

                if (args.Length > 0)
                    sInput = args[0];

                if (args.Length > 1)
                    sOutput = args[1];

                Console.WriteLine("Input file: " + sInput + ". Writing to output " + sOutput);

                Document doc = new Document(sInput);

                Page docpage = doc.GetPage(0);

                LinkAnnotation newLink = new LinkAnnotation(docpage, new Rect(100, docpage.CropBox.Top - 25, 200, docpage.CropBox.Top - 50));

                // Test some link features
                newLink.NormalAppearance = newLink.GenerateAppearance();

                Console.WriteLine("Current Link Annotation version = " + newLink.AnnotationFeatureLevel);
                newLink.AnnotationFeatureLevel = 1.0;
                Console.WriteLine("New Link Annotation version = " + newLink.AnnotationFeatureLevel);

                // Test the destination setting
                ViewDestination dest = new ViewDestination(doc, 0, "XYZ", doc.GetPage(0).MediaBox, 1.5);

                dest.DestRect = new Rect(0.0, 0.0, 200.0, 200.0);
                Console.WriteLine("The new destination rectangle: " + dest.DestRect);

                dest.FitType = "FitV";
                Console.WriteLine("The new fit type: " + dest.FitType);

                dest.Zoom = 2.5;
                Console.WriteLine("The new zoom level: " + dest.Zoom);

                dest.PageNumber = 1;
                Console.WriteLine("The new page number: " + dest.PageNumber);

                newLink.Destination = dest;

                newLink.Highlight = HighlightStyle.Invert;

                if (newLink.Highlight == HighlightStyle.Invert)
                    Console.WriteLine("Invert highlighting.");

                doc.Save(SaveFlags.Full, sOutput);
            }
        }
    }
}
