using System;
using Datalogics.PDFL;

/*
 * This sample demonstrates converting a web page or local HTML file into
 * a PDF document using the WebToPDF plug-in.
 *
 * The first optional argument can be either a URL (http://, https://, or
 * file://) or a path to a local HTML file. The sample auto-detects which
 * based on the scheme prefix and routes to Document.FromWebUrl or
 * Document.FromHtmlFile accordingly. The second optional argument is the
 * output PDF path.
 *
 * Copyright (c) Datalogics, Inc. All rights reserved.
 */

namespace CreateDocFromWebPage
{
    class CreateDocFromWebPage
    {
        // Returns true if the source looks like a URL the conversion
        // plug-in handles natively (http/https/file).
        static bool LooksLikeUrl(string s)
        {
            return s.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                || s.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                || s.StartsWith("file://", StringComparison.OrdinalIgnoreCase);
        }

        static void Main(string[] args)
        {
            Console.WriteLine("CreateDocFromWebPage sample:");

            using (Library lib = new Library())
            {
                String sSource = "https://www.datalogics.com";
                String sOutput = "CreateDocFromWebPage-out.pdf";

                if (args.Length > 0)
                    sSource = args[0];

                if (args.Length > 1)
                    sOutput = args[1];

                // Create a WebToPDFConvertParams to specify conversion
                // parameters for creating the document.
                using (WebToPDFConvertParams webParams = new WebToPDFConvertParams())
                {
                    webParams.ViewportSize = WebViewportPreset.Desktop;
                    webParams.PageSize = WebPageSize.Letter;
                    webParams.PageOrientation = WebPageOrientation.Portrait;
                    webParams.SetMargins(0.5, 0.5, 0.5, 0.5);
                    webParams.PrintBackground = true;
                    webParams.TimeoutSeconds = 60;

                    // The conversion populates a WebConvertInfo with result
                    // metadata: page count, conversion time, document title,
                    // and resolved source URL.
                    using (WebConvertInfo info = new WebConvertInfo())
                    {
                        bool isUrl = LooksLikeUrl(sSource);
                        Console.WriteLine("Converting " + (isUrl ? "URL " : "HTML file ") + sSource + " ...");

                        using (Document doc = isUrl
                            ? Document.FromWebUrl(sSource, webParams, info)
                            : Document.FromHtmlFile(sSource, webParams, info))
                        {
                            Console.WriteLine("Converted " + info.PageCount + " pages in " + info.ConversionTimeMs + " ms");
                            if (!String.IsNullOrEmpty(info.Title))
                                Console.WriteLine("Title: " + info.Title);
                            if (!String.IsNullOrEmpty(info.SourceUrl) && info.SourceUrl != sSource)
                                Console.WriteLine("Resolved URL: " + info.SourceUrl);

                            // Save the document.
                            Console.WriteLine("Saving the document...");
                            doc.Save(SaveFlags.Full, sOutput);
                            Console.WriteLine("Saved to " + sOutput);
                        }
                    }
                }
            }
        }
    }
}
