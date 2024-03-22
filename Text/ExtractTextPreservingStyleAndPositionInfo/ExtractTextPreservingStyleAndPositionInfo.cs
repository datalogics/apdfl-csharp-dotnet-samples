using System;
using System.Collections.Generic;
using System.Text.Json;
using System.IO;
using Datalogics.PDFL;
using ExtractTextNameSpace;

/*
 * 
 * This sample extracts text and details of that text in a PDF
 * document, prints to console, and saves the text to a JSON file.
 *
 * Copyright (c) 2022-2023, Datalogics, Inc. All rights reserved.
 *
 */
namespace ExtractTextPreservingStyleAndPositionInfo
{
    class ExtractTextPreservingStyleAndPositionInfo
    {
        // Set Defaults
        static String sInput = Library.ResourceDirectory + "Sample_Input/sample.pdf";
        static String sOutput = "ExtractTextPreservingStyleAndPositionInfo-out.json";

        static void Main(string[] args)
        {
            Console.WriteLine("ExtractTextPreservingStyleAndPositionInfo Sample:");


            using (new Library())
            {
                Console.WriteLine("Initialized the library.");

                Console.WriteLine("Input file: " + sInput);

                using (Document doc = new Document(sInput))
                {
                    using (ExtractText docText = new ExtractText(doc))
                    {
                        List<TextAndDetailsObject> result = docText.GetTextAndDetails();

                        // Save the output to a JSON file.
                        SaveJson(result);
                    }
                }
            }
        }

        static void SaveJson(List<TextAndDetailsObject> result)
        {
            using FileStream fs = File.Create(sOutput);
            var writerOptions = new JsonWriterOptions { Indented = true };
            using (var writer = new Utf8JsonWriter(fs, options: writerOptions))
            {
                writer.WriteStartArray();
                foreach (TextAndDetailsObject resultText in result)
                {
                    writer.WriteStartObject();
                    writer.WriteString("text", resultText.Text);
                    writer.WriteStartArray("quads");
                    foreach (Quad quad in resultText.Quads ?? Enumerable.Empty<Quad>())
                    {
                        writer.WriteStartObject();
                        writer.WriteString("top-left", quad.TopLeft.ToString());
                        writer.WriteString("top-right", quad.TopRight.ToString());
                        writer.WriteString("bottom-left", quad.BottomLeft.ToString());
                        writer.WriteString("bottom-right", quad.BottomRight.ToString());
                        writer.WriteEndObject();
                    }
                    writer.WriteEndArray();
                    writer.WriteStartArray("styles");
                    foreach (DLStyleTransition st in resultText.StyleList ?? Enumerable.Empty<DLStyleTransition>())
                    {
                        writer.WriteStartObject();
                        writer.WriteString("char-index", st.CharIndex.ToString());
                        string fontName = "";
                        if (st.Style != null && st.Style.FontName != null)
                        {
                            fontName = st.Style.FontName;
                        }
                        writer.WriteString("font-name", fontName);
                        double fontSize = 0;
                        if (st.Style != null)
                        {
                            fontSize = st.Style.FontSize;
                        }
                        writer.WriteString("font-size", Math.Round(fontSize, 2).ToString());
                        string colorSpaceName = "";
                        if (st.Style != null && st.Style.Color != null && st.Style.Color.Space != null && st.Style.Color.Space.Name != null)
                        {
                            colorSpaceName = st.Style.Color.Space.Name;
                        }
                        writer.WriteString("color-space", colorSpaceName);
                        writer.WriteStartArray("color-values");
                        if (st.Style != null)
                        {
                            DLColor? color = st.Style.Color;
                            if (color != null)
                            {
                                foreach (double cv in color.Value ?? Enumerable.Empty<double>())
                                {
                                    writer.WriteStringValue(Math.Round(cv, 3).ToString());
                                }
                            }
                        }
                        writer.WriteEndArray();
                        writer.WriteEndObject();
                    }
                    writer.WriteEndArray();
                    writer.WriteEndObject();
                }
                writer.WriteEndArray();
                writer.Flush();
            }
        }
    }
}
