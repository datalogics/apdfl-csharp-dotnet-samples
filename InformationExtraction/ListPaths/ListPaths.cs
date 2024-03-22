using System;
using System.Collections.Generic;
using Datalogics.PDFL;

/*
 * This sample searches for and lists the contents of paths found in an existing PDF document.
 * Paths in PDF documents, or clipping paths, define the boundaries for art or graphics.
 * 
 * Copyright (c) 2007-2023, Datalogics, Inc. All rights reserved.
 *
 */

namespace ListPaths
{
    class ListPaths
    {
        static void Main(string[] args)
        {
            Console.WriteLine("ListLayers Sample:");


            // ReSharper disable once UnusedVariable
            using (Library lib = new Library())
            {
                Console.WriteLine("Initialized the library.");

                String sInput = Library.ResourceDirectory + "Sample_Input/sample.pdf";

                if (args.Length > 0)
                    sInput = args[0];

                Console.WriteLine("Input file: " + sInput);

                using (Document doc = new Document(sInput))
                {
                    ListPathsInDocument(doc);
                }
            }
        }

        private static void ListPathsInDocument(Document doc)
        {
            for (int pgno = 0; pgno < doc.NumPages; pgno++)
            {
                ListPathsInContent(doc.GetPage(pgno).Content, pgno);
            }
        }

        private static void ListPathsInContent(Content content, int pgno)
        {
            for (int i = 0; i < content.NumElements; i++)
            {
                Element e = content.GetElement(i);

                if (e is Datalogics.PDFL.Path)
                {
                    ListPath((Datalogics.PDFL.Path)e, pgno);
                }
                else if (e is Container)
                {
                    Console.WriteLine("Recurring through a Container");
                    ListPathsInContent(((Datalogics.PDFL.Container)e).Content, pgno);
                }
                else if (e is Group)
                {
                    Console.WriteLine("Recurring through a Group");
                    ListPathsInContent(((Datalogics.PDFL.Group)e).Content, pgno);
                }
                else if (e is Form)
                {
                    Console.WriteLine("Recurring through a Form");
                    ListPathsInContent(((Datalogics.PDFL.Form)e).Content, pgno);
                }
            }
        }

        private static void ListPath(Datalogics.PDFL.Path path, int pgno)
        {
            IList<Segment> segments = path.Segments;
            Console.WriteLine("Path on page {0}:", pgno);
            Console.WriteLine("Transformation matrix: {0}", path.Matrix);
            foreach (Segment segment in segments)
            {
                if (segment is MoveTo)
                {
                    MoveTo moveto = (MoveTo)segment;
                    Console.WriteLine("  MoveTo x={0}, y={1}", moveto.Point.H, moveto.Point.V);
                }
                else if (segment is LineTo)
                {
                    LineTo lineto = (LineTo)segment;
                    Console.WriteLine("  LineTo x={0}, y={1}",
                        lineto.Point.H, lineto.Point.V);
                }
                else if (segment is CurveTo)
                {
                    CurveTo curveto = (CurveTo)segment;
                    Console.WriteLine("  CurveTo x1={0}, y1={1}, x2={2}, y2={3}, x3={4}, y3={5}",
                        curveto.Point1.H, curveto.Point1.V,
                        curveto.Point2.H, curveto.Point2.V,
                        curveto.Point3.H, curveto.Point3.V);
                }
                else if (segment is CurveToV)
                {
                    CurveToV curveto = (CurveToV)segment;
                    Console.WriteLine("  CurveToV x2={0}, y2={1}, x3={2}, y3={3}",
                        curveto.Point2.H, curveto.Point2.V,
                        curveto.Point3.H, curveto.Point3.V);
                }
                else if (segment is CurveToY)
                {
                    CurveToY curveto = (CurveToY)segment;
                    Console.WriteLine("  CurveToV x1={0}, y1={1}, x3={2}, y3={3}",
                        curveto.Point1.H, curveto.Point1.V,
                        curveto.Point3.H, curveto.Point3.V);
                }
                else if (segment is RectSegment)
                {
                    RectSegment rect = (RectSegment)segment;
                    Console.WriteLine("  Rectangle x={0}, y={1}, width={2}, height={3}",
                        rect.Point.H, rect.Point.V,
                        rect.Width, rect.Height);
                }
                else if (segment is ClosePath)
                {
                    Console.WriteLine("  ClosePath");
                }
            }
        }
    }
}
