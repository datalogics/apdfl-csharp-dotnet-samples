using System;
using Datalogics.PDFL;

/*
 * 
 * This sample finds and describes the bookmarks included in a PDF document.
 * 
 * 
 * Copyright (c) 2007-2023, Datalogics, Inc. All rights reserved.
 *
 */

namespace ListBookmarks
{
    class ListBookmarks
    {
        static void EnumerateBookmarks(Bookmark b)
        {
            if (b != null)
            {
                Console.Write(b);
                Console.Write(": ");
                Console.Write(b.Title);
                ViewDestination v = b.ViewDestination;
                if (v != null)
                {
                    Console.Write(", page ");
                    Console.Write(v.PageNumber);
                    Console.Write(", fit ");
                    Console.Write(v.FitType);
                    Console.Write(", dest rect ");
                    Console.Write(v.DestRect);
                    Console.Write(", zoom ");
                    Console.Write(v.Zoom);
                }

                Console.WriteLine();
                EnumerateBookmarks(b.FirstChild);
                EnumerateBookmarks(b.Next);
            }
        }

        static void Main(string[] args)
        {
            Console.WriteLine("ListBookmarks Sample:");


            // ReSharper disable once UnusedVariable
            using (Library lib = new Library())
            {
                Console.WriteLine("Initialized the library.");

                String sInput = Library.ResourceDirectory + "Sample_Input/sample.pdf";

                if (args.Length > 0)
                    sInput = args[0];

                Console.WriteLine("Input file: " + sInput);

                Document doc = new Document(sInput);

                Bookmark rootBookmark = doc.BookmarkRoot;
                EnumerateBookmarks(rootBookmark);
            }
        }
    }
}
