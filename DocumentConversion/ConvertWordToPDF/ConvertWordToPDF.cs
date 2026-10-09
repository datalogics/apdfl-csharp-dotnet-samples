using System;
using Datalogics.PDFL;

/*
 * This sample demonstrates the OfficeToPDF plug-in, which converts a Microsoft
 * Word (.docx) document into a PDF.
 *
 * By default the sample converts DOCXLink.docx from the Sample_Input
 * directory and writes the result to ConvertWordToPDF-out.pdf in the current
 * working directory. The first optional argument is the input .docx path and
 * the second is the output PDF path.
 *
 * Document.FromOfficeFile takes the input path, an OfficeToPDFConvertParams
 * and an OfficeConvertInfo. It returns the converted Document, and fills the
 * OfficeConvertInfo with the page count and the per-asset diagnostics.
 *
 * Copyright (c) Datalogics, Inc. All rights reserved.
 */

namespace ConvertWordToPDF
{
    class ConvertWordToPDF
    {
        static void Main(string[] args)
        {
            Console.WriteLine("ConvertWordToPDF sample:");

            using (Library lib = new Library())
            {
                String sInput = Library.ResourceDirectory + "Sample_Input/DOCXLink.docx";
                String sOutput = "ConvertWordToPDF-out.pdf";

                if (args.Length > 0)
                    sInput = args[0];

                if (args.Length > 1)
                    sOutput = args[1];

                Console.WriteLine("Converting " + sInput + " and saving as " + sOutput);

                // Configure the conversion. One OfficeToPDFConvertParams serves
                // any number of conversions.
                using (OfficeToPDFConvertParams convertParams = new OfficeToPDFConvertParams())
                {
                    // OfficeCommentRendering.Omit (default), Margin, or Annotations.
                    convertParams.Comments = OfficeCommentRendering.Margin;

                    // A fixed instant is stamped as /CreationDate and /ModDate and
                    // used for DATE and TIME fields, so every conversion carries
                    // the same dates. Leave it unset to stamp the system clock.
                    convertParams.SetConversionTime(2026, 1, 1, 0, 0, 0);

                    using (OfficeConvertInfo info = new OfficeConvertInfo())
                    {
                        Document doc;
                        try
                        {
                            doc = Document.FromOfficeFile(sInput, convertParams, info);
                        }
                        catch (Exception ex)
                        {
                            // The message names the reason: the source is missing,
                            // not a valid .docx, or password-protected, or the
                            // OfficeToPDF plug-in is not available.
                            Console.WriteLine("Conversion failed: " + ex.Message);
                            return;
                        }

                        using (doc)
                        {
                            Console.WriteLine("Successfully converted the document: " + info.PageCount + " page(s).");

                            // A successful conversion can still report diagnostics:
                            // a substituted font, an undecodable image, a hyperlink
                            // dropped as unsafe. They describe the rendered assets;
                            // they do not make the conversion fail.
                            foreach (OfficeDiagnostic diagnostic in info.Diagnostics)
                            {
                                Console.WriteLine("  [" + diagnostic.Kind + "] " + diagnostic.Asset + ": "
                                    + diagnostic.Message);
                            }

                            doc.Save(SaveFlags.Full, sOutput);
                            Console.WriteLine("Saved to " + sOutput);
                        }
                    }
                }
            }
        }
    }
}
