using Datalogics.PDFL;
using System;
using System.Collections.Generic;

/*
 * This sample demonstrates creating an Output Preview Image which is used during Soft Proofing prior to printing to visualize combining different Colorants.
 *
 * 
 * Copyright (c)2023-2024, Datalogics, Inc. All rights reserved.
 *
 */

namespace OutputPreview
{
    class OutputPreview
    {
        static string CreateOutputFileName(List<string> colorants)
        {
            string outputFileName = "OutputPreview_";

            foreach(string colorant in colorants)
            {
                outputFileName += colorant + "_";
            }

            outputFileName += ".tiff";

            return outputFileName;
        }

        static void Main(string[] args)
        {
            Console.WriteLine("OutputPreview Sample:");

            String sInput = Library.ResourceDirectory + "Sample_Input/spotcolors1.pdf";

            // Here you specify the Colorant names of interest
            List<String> colorantsToUse = new List<string>() { "Yellow", "Black" };

            List<String> colorantsToUse2 = new List<string>() { "PANTONE 554 CVC", "PANTONE 814 2X CVC", "PANTONE 185 2X CVC" };

            using (Library lib = new Library())
            {
                using (Document doc = new Document(sInput))
                {
                    using (Page pg = doc.GetPage(0))
                    {
                        // Get all inks that are present on the page
                        List<Ink> inks = (List<Ink>)pg.ListInks();

                        List<SeparationColorSpace> colorants = new List<SeparationColorSpace>();

                        foreach (Ink theInk in inks)
                        {
                            foreach (String theColorant in colorantsToUse)
                            {
                                if (theInk.ColorantName == theColorant)
                                {
                                    colorants.Add(new SeparationColorSpace(pg, theInk));
                                }
                            }
                        }

                        List<SeparationColorSpace> colorants2 = new List<SeparationColorSpace>();

                        foreach (Ink theInk in inks)
                        {
                            foreach (String theColorant in colorantsToUse2)
                            {
                                if (theInk.ColorantName == theColorant)
                                {
                                    colorants2.Add(new SeparationColorSpace(pg, theInk));
                                }
                            }
                        }

                        PageImageParams pip = new PageImageParams();
                        pip.PageDrawFlags = DrawFlags.UseAnnotFaces;
                        pip.HorizontalResolution = 300;
                        pip.VerticalResolution = 300;

                        ImageSaveParams sp = new ImageSaveParams();

                        // Create Output Preview images using the Specified Colorants
                        Datalogics.PDFL.Image image = pg.GetOutputPreviewImage(pg.CropBox, pip, colorants);

                        image.Save(CreateOutputFileName(colorantsToUse), ImageType.TIFF);

                        Datalogics.PDFL.Image image2 = pg.GetOutputPreviewImage(pg.CropBox, pip, colorants2);

                        image2.Save(CreateOutputFileName(colorantsToUse2), ImageType.TIFF);
                    }
                }
            }
        }
    }
}
