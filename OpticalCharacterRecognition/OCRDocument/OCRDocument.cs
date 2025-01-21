using System;
using System.Collections.Generic;
using Datalogics.PDFL;

/*
 * Runs OCR on the document recognizing text found on its rasterized pages.
 * 
 * Copyright (c) 2007-2025, Datalogics, Inc. All rights reserved.
 *
 */

namespace OCRDocument
{
    class OCRDocument
    {
        static void Main(string[] args)
        {
            Console.WriteLine("OCRDocument Sample:");

            using (Library lib = new Library())
            {
                Console.WriteLine("Initialized the library.");

                String sInput = Library.ResourceDirectory + "Sample_Input/scanned_images.pdf";
                String sOutput = "OCRDocument-out.pdf";

                if (args.Length > 0)
                    sInput = args[0];
                if (args.Length > 1)
                    sOutput = args[1];

                Console.WriteLine("Input file: " + sInput);
                Console.WriteLine("Writing output to: " + sOutput);

                OCRParams ocrParams = new OCRParams();
                //The OCRParams.Languages parameter controls which languages the OCR engine attempts
                //to detect. By default the OCR engine searches for English.
                List<LanguageSetting> langList = new List<LanguageSetting>();
                LanguageSetting languageOne = new LanguageSetting(Language.English, false);
                langList.Add(languageOne);

                //You could add additional languages for the OCR engine to detect by adding 
                //more entries to the LanguageSetting list. 

                //LanguageSetting languageTwo = new LanguageSetting(Language.Japanese, false);
                //langList.Add(languageTwo);
                ocrParams.Languages = langList;

                // If the resolution for the images in your document are not
                // 300 dpi, specify a default resolution here. Specifying a
                // correct resolution gives better results for OCR, especially
                // with automatic image preprocessing.
                // ocrParams.Resolution = 600;

                using (OCREngine ocrEngine = new OCREngine(ocrParams))
                {
                    //Create a document object using the input file
                    using (Document doc = new Document(sInput))
                    {
                        for (int numPage = 0; numPage < doc.NumPages; numPage++)
                        {
                            using (Page page = doc.GetPage(numPage))
                            {
                                page.RecognizePageContents(doc, ocrEngine);
                            }
                        }

                        doc.Save(SaveFlags.Full, sOutput);
                    }
                }
            }
        }
    }
}
