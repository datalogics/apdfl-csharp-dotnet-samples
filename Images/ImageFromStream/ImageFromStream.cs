using System;
using System.Runtime.InteropServices;
using Datalogics.PDFL;
using SkiaSharp;

/*
 * 
 * This sample program searches through the PDF file that you select and identifies drawings,
 * diagrams and photographs from the System.IO.Streams.
 * 
 * A stream is a string of bytes of any length, embedded in a PDF document with a dictionary
 * that is used to interpret the values in the stream.
 * 
 * This program is similar to StreamIO.
 * 
 * Copyright (c) 2007-2023, Datalogics, Inc. All rights reserved.
 *
 */
namespace ImageFromStream
{
    class ImageFromStream
    {
        static void Main(string[] args)
        {

            Console.WriteLine("ImageFromStream Sample:");


            // ReSharper disable once UnusedVariable
            using (Library lib = new Library())
            {
                Console.WriteLine("Initialized the library.");

                String bitmapInput = Library.ResourceDirectory + "Sample_Input/Datalogics.bmp";
                String jpegInput = Library.ResourceDirectory + "Sample_Input/ducky.jpg";
                String docOutput = "ImageFromStream-out2.pdf";

                if (args.Length > 0)
                    bitmapInput = args[0];

                if (args.Length > 1)
                    jpegInput = args[1];

                if (args.Length > 2)
                    docOutput = args[2];

                Console.WriteLine("using bitmap input " + bitmapInput + " and jpeg input " + jpegInput +
                                  ". Writing to output " + docOutput);

                // Create a MemoryStream object.
                // A MemoryStream is used here for demonstration only, but the technique
                // works just as well for other streams which support seeking.
                using (System.IO.MemoryStream BitmapStream = new System.IO.MemoryStream())
                {
                    // Load a bitmap image into the MemoryStream.
                    using (SKBitmap BitmapImage = SKBitmap.FromImage(SKImage.FromEncodedData(bitmapInput)))
                    {
                        BitmapImage.Encode(SKEncodedImageFormat.Png, 100).SaveTo(BitmapStream);

                        // Reset the MemoryStream's seek position before handing it to the PDFL API,
                        // which expects the seek position to be at the beginning of the stream.
                        BitmapStream.Seek(0, System.IO.SeekOrigin.Begin);

                        // Create the PDFL Image object.
                        Datalogics.PDFL.Image PDFLBitmapImage = new Datalogics.PDFL.Image(BitmapStream);

                        // Save the image to a PNG file.
                        PDFLBitmapImage.Save("ImageFromStream-out.png", ImageType.PNG);

                        // The following demonstrates reading an image from a Stream and placing it into a document.
                        // First, create a new Document and add a Page to it.
                        using (Document doc = new Document())
                        {
                            doc.CreatePage(Document.BeforeFirstPage, new Rect(0, 0, 612, 792));

                            // Create a new MemoryStream for a new image file.
                            using (System.IO.MemoryStream outputImageStream = new System.IO.MemoryStream())
                            {
                                // Load a JPEG image into the MemoryStream.
                                using (SKImage inputImage = SKImage.FromEncodedData(jpegInput))
                                {
                                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                                    {
                                        inputImage.Encode(SKEncodedImageFormat.Jpeg, 100).SaveTo(outputImageStream);
                                    }
                                    else
                                    {
                                        //Known issue in JPEG encoding in SkiaSharp v2.88.6: https://github.com/mono/SkiaSharp/issues/2643
                                        //Previous versions have known vulnerability, so use PNG instead.
                                        inputImage.Encode(SKEncodedImageFormat.Png, 100).SaveTo(outputImageStream);
                                    }

                                    // An alternative method for resetting the MemoryStream's seek position.
                                    outputImageStream.Position = 0;

                                    // Create the PDFL Image object and put it in the Document.
                                    // Since the image will be placed in a Document, use the constructor with the optional
                                    // Document parameter to optimize data usage for this image within the Document.
                                    Datalogics.PDFL.Image PDFLJpegImage = new Datalogics.PDFL.Image(outputImageStream, doc);
                                    Page pg = doc.GetPage(0);
                                    pg.Content.AddElement(PDFLJpegImage);
                                    pg.UpdateContent();
                                    doc.Save(SaveFlags.Full, docOutput);
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}
